const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn, spawnSync } = require('node:child_process');
const { once } = require('node:events');
const { randomUUID } = require('node:crypto');
const sql = require('mssql');
const { Pool } = require('pg');
const amqp = require('amqplib');
const path = require('node:path');
const { hashPassword } = require('../dist/passwords');
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
function interop(input) {
  const result = spawnSync('dotnet', [process.env.PHASE3_INTEROP_DLL], { input: JSON.stringify(input), encoding: 'utf8' });
  assert.equal(result.status, 0, 'ASP.NET interop command failed');
  return JSON.parse(result.stdout);
}
function compose(...args) {
  const result = spawnSync('docker', ['compose', '-f', process.env.PHASE3_TEST_COMPOSE, ...args], { encoding: 'utf8' });
  assert.equal(result.status, 0, `Fixture Docker operation failed: ${args[0]}`);
}
async function eventually(check, description, milliseconds = 30000) {
  const end = Date.now() + milliseconds;
  while (Date.now() < end) { if (await check()) return; await delay(500); }
  assert.fail(description);
}
test('real SQL Server/PostgreSQL migration, NestJS authorization, ASP.NET interoperability and RabbitMQ recovery', { timeout: 150000 }, async () => {
  const pg = new Pool({ connectionString: process.env.AUTH_DATABASE_URL });
  const config = JSON.parse(process.env.SOURCE_SQLSERVER_CONFIG);
  const master = await new sql.ConnectionPool({ ...config, database: 'master' }).connect();
  await master.request().query("IF DB_ID('Phase3AuthMigrationTest') IS NULL CREATE DATABASE Phase3AuthMigrationTest");
  await master.close();
  const source = await new sql.ConnectionPool(config).connect();
  let app;
  let rabbit;
  let gateway;
  try {
    // Only these explicitly isolated fixture tables are reset.
    await source.request().query(`IF OBJECT_ID('dbo.AuditOutbox') IS NOT NULL DROP TABLE dbo.AuditOutbox;
      IF OBJECT_ID('dbo.Users') IS NOT NULL DROP TABLE dbo.Users;
      CREATE TABLE dbo.Users(Id uniqueidentifier PRIMARY KEY,Username nvarchar(50),NormalizedUsername nvarchar(50),
      Email nvarchar(254),NormalizedEmail nvarchar(254),PasswordHash nvarchar(512),Role nvarchar(32),IsActive bit,
      CreatedAt datetimeoffset(3),UpdatedAt datetimeoffset(3),LastLoginAt datetimeoffset(3));
      CREATE TABLE dbo.AuditOutbox(Id uniqueidentifier PRIMARY KEY,Payload nvarchar(max),CreatedAt datetime2,PublishedAt datetime2 NULL);`);
    await pg.query('DROP TABLE IF EXISTS external_identities,revoked_tokens,audit_outbox,users');
    const password = 'Existing-User-Passw0rd';
    const legacyHash = interop({ operation: 'hash', password }).hash;
    const ids = {};
    for (const role of ['Administrator', 'Operator', 'Viewer']) {
      ids[role] = randomUUID();
      await source.request().input('id', sql.UniqueIdentifier, ids[role]).input('name', sql.NVarChar, role)
        .input('email', sql.NVarChar, `${role}@example.com`).input('hash', sql.NVarChar, legacyHash)
        .query(`INSERT dbo.Users VALUES(@id,@name,UPPER(@name),@email,UPPER(@email),@hash,@name,1,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z',NULL)`);
    }
    const eventId = randomUUID();
    const originalEvent = { EventId: eventId, OccurredAt: new Date().toISOString(), CorrelationId: 'migration-fixture',
      UserId: ids.Administrator, UserName: 'Administrator', Action: 'Create', Resource: 'User',
      ResourceId: ids.Viewer, Description: 'Migrated event', IpAddress: null };
    await source.request().input('id', sql.UniqueIdentifier, eventId).input('payload', sql.NVarChar, JSON.stringify(originalEvent))
      .query('INSERT dbo.AuditOutbox VALUES(@id,@payload,SYSUTCDATETIME(),NULL)');
    for (let run = 0; run < 2; run++) {
      const migrated = spawnSync(process.execPath, ['dist/migrate.js'], { encoding: 'utf8' });
      assert.equal(migrated.status, 0, 'Migration must succeed and be idempotent');
      const summary = JSON.parse(migrated.stdout);
      assert.equal(summary.users, 3); assert.equal(summary.pendingAuditEvents, 1); assert.equal(summary.verified, true);
    }
    assert.equal((await pg.query('SELECT COUNT(*)::int AS count FROM users')).rows[0].count, 3);
    assert.equal((await pg.query('SELECT COUNT(*)::int AS count FROM audit_outbox')).rows[0].count, 1);
    for (const row of (await pg.query('SELECT id,password_hash FROM users')).rows) {
      assert.ok(Object.values(ids).includes(row.id)); assert.equal(row.password_hash, legacyHash);
    }
    // A conflicting target must fail without silently overwriting it.
    await pg.query("UPDATE users SET email='changed@example.com' WHERE id=$1", [ids.Viewer]);
    assert.notEqual(spawnSync(process.execPath, ['dist/migrate.js'], { encoding: 'utf8' }).status, 0);
    await pg.query('UPDATE users SET email=$2 WHERE id=$1', [ids.Viewer, 'Viewer@example.com']);
    assert.equal(interop({ operation: 'verifyHash', hash: await hashPassword(password), password }).valid, true);
    app = spawn(process.execPath, ['dist/main.js'], { env: { ...process.env, PORT: '15101' }, stdio: ['ignore', 'pipe', 'pipe'] });
    app.stdout.on('data', () => {}); app.stderr.on('data', () => {});
    const api = 'http://127.0.0.1:15101';
    await eventually(async () => fetch(api + '/health/ready').then(r => r.ok).catch(() => false), 'NestJS must become ready');
    async function request(path, token, method = 'GET', body) {
      return fetch(api + path, { method, headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}),
        'Content-Type': 'application/json', 'X-Correlation-ID': 'auth-integration' }, body: body === undefined ? undefined : JSON.stringify(body) });
    }
    const tokens = {};
    for (const role of Object.keys(ids)) {
      const response = await request('/api/v1/auth/login', null, 'POST', { login: role, password });
      assert.equal(response.status, 200);
      const session = await response.json(); assert.equal(session.user.id, ids[role]); assert.ok(session.user.lastLoginAt);
      assert.equal(session.user.passwordHash, undefined); tokens[role] = session.accessToken;
      assert.equal((await request('/api/v1/users', session.accessToken)).status, role === 'Administrator' ? 200 : 403);
    }
    assert.deepEqual(interop({ operation: 'verifyToken', token: tokens.Administrator, secret: process.env.JWT_SIGNING_KEY }), { valid: true, administrator: true });
    const gatewayPath = path.join(process.env.PHASE3_TEST_BACKEND, 'src/Gateway/Climate.Gateway');
    gateway = spawn('dotnet', [path.join(gatewayPath, 'bin/Debug/net10.0/Climate.Gateway.dll')], {
      cwd: gatewayPath, env: { ...process.env, ASPNETCORE_URLS: 'http://127.0.0.1:15180',
        Jwt__SigningKey: process.env.JWT_SIGNING_KEY,
        ReverseProxy__Clusters__identity__Destinations__primary__Address: api + '/' }, stdio: ['ignore', 'pipe', 'pipe'] });
    gateway.stdout.on('data', () => {}); gateway.stderr.on('data', () => {});
    const publicApi = 'http://127.0.0.1:15180';
    await eventually(async () => fetch(publicApi + '/health/ready').then(r => r.ok).catch(() => false), 'Gateway must become ready independently of unavailable Audit');
    assert.equal((await fetch(publicApi + '/api/users')).status, 401, 'Gateway rejects unauthenticated requests');
    assert.equal((await fetch(publicApi + '/api/users', { headers: { Authorization: 'Bearer invalid' } })).status, 401);
    const publicLogin = await fetch(publicApi + '/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ login: 'Administrator', password }) });
    assert.equal(publicLogin.status, 200, 'Public login route passes through Gateway');
    const publicUsers = await fetch(publicApi + '/api/users', { headers: { Authorization: `Bearer ${tokens.Administrator}`, 'X-Correlation-ID': 'gateway-auth-test' } });
    assert.equal(publicUsers.status, 200, 'Gateway and NestJS agree on JWT signature and claims');
    assert.equal(publicUsers.headers.get('X-Correlation-ID'), 'gateway-auth-test');
    assert.equal((await publicUsers.json()).length, 3);
    const preflight = await fetch(publicApi + '/api/users', { method: 'OPTIONS', headers: {
      Origin: 'http://localhost:4200', 'Access-Control-Request-Method': 'GET' } });
    assert.equal(preflight.headers.get('Access-Control-Allow-Origin'), 'http://localhost:4200');
    assert.equal((await request('/api/v1/users')).status, 401);
    assert.equal((await request('/api/v1/auth/login', null, 'POST', { login: 'Administrator', password: 'wrong' })).status, 401);
    assert.equal((await request('/api/v1/auth/register', null, 'POST', { username: 'attacker', email: 'a@example.com', password, role: 'Administrator' })).status, 422);
    const newUser = await request('/api/v1/users', tokens.Administrator, 'POST', { username: 'new_operator', email: 'new@example.com', password, role: 'Operator' });
    assert.equal(newUser.status, 201); const created = await newUser.json();
    assert.equal((await request('/api/v1/users', tokens.Administrator, 'POST', { username: 'new_operator', email: 'new@example.com', password, role: 'Operator' })).status, 409);
    assert.equal((await request(`/api/v1/users/${created.id}/status`, tokens.Administrator, 'PATCH', { isActive: false })).status, 204);
    assert.equal((await request('/api/v1/auth/login', null, 'POST', { login: 'new_operator', password })).status, 403);
    assert.equal((await request('/api/v1/auth/logout', tokens.Viewer, 'POST')).status, 204);
    assert.equal((await request('/api/v1/users/me', tokens.Viewer)).status, 401);
    rabbit = await amqp.connect(process.env.RABBITMQ_URL);
    const channel = await rabbit.createChannel();
    await eventually(async () => (await pg.query('SELECT COUNT(*)::int AS count FROM audit_outbox WHERE published_at IS NULL')).rows[0].count === 0, 'Existing audit events must be confirmed');
    const firstMessage = await channel.get('climate.audit.events', { noAck: false });
    assert.ok(firstMessage); const payload = JSON.parse(firstMessage.content.toString());
    assert.ok(payload.EventId); assert.ok(payload.CorrelationId); channel.nack(firstMessage, false, true);
    await rabbit.close(); rabbit = undefined;
    compose('stop', 'rabbitmq');
    const duringOutage = await request('/api/v1/users', tokens.Administrator, 'POST', { username: 'outage_user', email: 'outage@example.com', password, role: 'Viewer' });
    assert.equal(duringOutage.status, 201, 'Business operation must succeed with broker down');
    assert.ok((await pg.query('SELECT COUNT(*)::int AS count FROM audit_outbox WHERE published_at IS NULL')).rows[0].count > 0);
    // Restart Auth as well: durability must survive the producer process restarting.
    app.kill('SIGTERM'); await once(app, 'exit');
    compose('start', 'rabbitmq');
    app = spawn(process.execPath, ['dist/main.js'], { env: { ...process.env, PORT: '15101' }, stdio: ['ignore', 'pipe', 'pipe'] });
    app.stdout.on('data', () => {}); app.stderr.on('data', () => {});
    await eventually(async () => (await pg.query('SELECT COUNT(*)::int AS count FROM audit_outbox WHERE published_at IS NULL')).rows[0].count === 0, 'Outbox must recover after broker and producer restart', 60000);
    assert.equal((await source.request().query('SELECT COUNT(*) AS count FROM dbo.Users')).recordset[0].count, 3, 'Source identities must remain intact');
    console.log('Verified: preserved IDs/hashes, repeated migration, collision rollback, real ASP.NET interoperability, roles, disabled users, broker outage and durable recovery.');
  } finally {
    if (rabbit) await rabbit.close();
    if (gateway && gateway.exitCode === null) { gateway.kill('SIGTERM'); await once(gateway, 'exit'); }
    if (app && app.exitCode === null) { app.kill('SIGTERM'); await once(app, 'exit'); }
    await source.close(); await pg.end();
  }
});
