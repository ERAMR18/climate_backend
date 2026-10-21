const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn, spawnSync } = require('node:child_process');
const { once } = require('node:events');
const { randomUUID } = require('node:crypto');
const { Pool } = require('pg');
const sql = require('mssql');
const path = require('node:path');
const { Tokens } = require('../dist/tokens');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

test('community migration, private PostgreSQL, remote sensor references, counts, permissions and fail-closed behavior', { timeout: 120000 }, async () => {
  const backend = process.env.PHASE3_TEST_BACKEND;
  const config = JSON.parse(process.env.SOURCE_SQLSERVER_CONFIG);
  const master = await new sql.ConnectionPool({ ...config, database: 'master' }).connect();
  const pgAdmin = new Pool({ connectionString: process.env.AUTH_DATABASE_URL });
  const sourceName = 'Phase3CommunityMigrationTest';
  await master.request().query(`IF DB_ID('${sourceName}') IS NULL CREATE DATABASE ${sourceName}`);
  await master.close();
  if (!(await pgAdmin.query("SELECT 1 FROM pg_database WHERE datname='community'")).rowCount) await pgAdmin.query('CREATE DATABASE community');
  await pgAdmin.end();
  const sourceConfig = { ...config, database: sourceName };
  const source = await new sql.ConnectionPool(sourceConfig).connect();
  const pg = new Pool({ connectionString: process.env.COMMUNITY_DATABASE_URL });
  const processes = [];
  const internalKey = randomUUID() + randomUUID();
  const actor = { id: randomUUID(), username: 'admin', email: 'admin@example.com', role: 'Administrator' };
  const adminToken = new Tokens().issue(actor).accessToken;
  const viewerToken = new Tokens().issue({ ...actor, role: 'Viewer' }).accessToken;
  const communityId = randomUUID();
  const seedSensorId = randomUUID();
  const sensorApi = 'http://127.0.0.1:15102';
  const communityApi = 'http://127.0.0.1:15107';
  const env = { ...process.env, Jwt__SigningKey: process.env.JWT_SIGNING_KEY, Jwt__Issuer: 'ClimateMonitoring.Identity',
    Jwt__Audience: 'ClimateMonitoring.Client', InternalApi__ApiKey: internalKey,
    RABBITMQ_HOST: '127.0.0.1', RABBITMQ_PORT: '15672', RABBITMQ_USER: 'test', RABBITMQ_PASSWORD: process.env.PHASE3_TEST_RABBIT_PASSWORD,
    Logging__LogLevel__Default: 'Warning' };
  function start(relative, dll, overrides) {
    const cwd = path.join(backend, relative);
    const child = spawn('dotnet', [path.join(cwd, 'bin/Debug/net10.0', dll)], { cwd, env: { ...env, ...overrides }, stdio: ['ignore', 'pipe', 'pipe'] });
    let log = ''; child.stdout.on('data', chunk => { log = (log + chunk.toString()).slice(-6000); });
    child.stderr.on('data', chunk => { log = (log + chunk.toString()).slice(-6000); });
    child.diagnostic = () => log;
    processes.push(child); return child;
  }
  async function ready(url, child) {
    for (let attempt = 0; attempt < 60; attempt++) {
      if (child.exitCode !== null) assert.fail('Service exited during startup: ' + child.diagnostic());
      if (await fetch(url + '/health/ready').then(r => r.ok).catch(() => false)) return;
      await pause(500);
    }
    assert.fail('Service did not become ready: ' + child.diagnostic());
  }
  async function request(base, route, method = 'GET', body, token = adminToken) {
    return fetch(base + route, { method, headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body) });
  }
  const migrate = () => spawnSync(process.execPath, [path.join(backend, 'tools/Phase3Migration/communities.cjs')], {
    env: { ...process.env, SOURCE_SQLSERVER_CONFIG: JSON.stringify(sourceConfig) }, encoding: 'utf8' });
  try {
    // Initialize the SOURCE fixture using the actual Sensor EF migrations and local mode first.
    const connection = `Server=127.0.0.1,15433;Database=${sourceName};User Id=sa;Password=${config.password};TrustServerCertificate=True;Encrypt=True`;
    const localSensor = start('src/Services/SensorService/Climate.Sensors.Api', 'Climate.Sensors.Api.dll', {
      ASPNETCORE_URLS: sensorApi, ConnectionStrings__SensorDb: connection, DemoSeed__Enabled: 'false',
      CommunityService__Enabled: 'false', RealtimeService__BaseUrl: 'http://127.0.0.1:15103/', RealtimeService__ApiKey: internalKey });
    // Legacy Sensor has /health rather than /health/ready.
    for (let attempt = 0; attempt < 60; attempt++) {
      if (localSensor.exitCode !== null) assert.fail(localSensor.diagnostic());
      if (await fetch(sensorApi + '/health').then(r => r.ok).catch(() => false)) break;
      await pause(500);
    }
    assert.equal((await fetch(sensorApi + '/health')).status, 200);
    localSensor.kill('SIGTERM'); await once(localSensor, 'exit');
    await source.request().input('id', sql.UniqueIdentifier, communityId).query(`INSERT INTO dbo.Communities
      (Id,Name,Description,Latitude,Longitude,IsActive,CreatedAt,Municipality,Department,Country)
      VALUES(@id,'Existing community','Historical location',14.6349,-90.5069,1,'2026-01-01T00:00:00Z','Guatemala','Guatemala','Guatemala')`);
    await source.request().input('id', sql.UniqueIdentifier, seedSensorId).input('community', sql.UniqueIdentifier, communityId)
      .query(`INSERT INTO dbo.Sensors(Id,Name,Code,NormalizedCode,Description,Type,Unit,CommunityId,Latitude,Longitude,IsActive,CreatedAt,UpdatedAt)
      VALUES(@id,'Existing sensor','EXISTING-01','EXISTING-01',NULL,'Temperature','°C',@community,14,-90,1,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z')`);
    for (let run = 0; run < 2; run++) {
      const result = migrate(); assert.equal(result.status, 0, result.stderr);
      assert.equal(JSON.parse(result.stdout).communities, 1);
      assert.equal(JSON.parse(result.stdout).validSensorCommunityReferences, 1);
    }
    const pgPassword = process.env.PHASE3_TEST_POSTGRES_PASSWORD;
    const community = start('src/Services/CommunityService/Climate.Communities.Api', 'Climate.Communities.Api.dll', {
      ASPNETCORE_URLS: communityApi, ConnectionStrings__CommunityDb: `Host=127.0.0.1;Port=15432;Database=community;Username=auth;Password=${pgPassword}`,
      SensorService__BaseUrl: sensorApi + '/' });
    await ready(communityApi, community);
    const remoteSensor = start('src/Services/SensorService/Climate.Sensors.Api', 'Climate.Sensors.Api.dll', {
      ASPNETCORE_URLS: sensorApi, ConnectionStrings__SensorDb: connection, DemoSeed__Enabled: 'false',
      CommunityService__Enabled: 'true', CommunityService__BaseUrl: communityApi + '/',
      RealtimeService__BaseUrl: 'http://127.0.0.1:15103/', RealtimeService__ApiKey: internalKey });
    for (let attempt = 0; attempt < 60; attempt++) {
      if (remoteSensor.exitCode !== null) assert.fail(remoteSensor.diagnostic());
      if (await fetch(sensorApi + '/health').then(r => r.ok).catch(() => false)) break;
      await pause(500);
    }
    const listResponse = await request(communityApi, '/api/v1/communities'); assert.equal(listResponse.status, 200);
    const list = await listResponse.json(); assert.equal(list[0].id, communityId); assert.equal(list[0].sensorCount, 1);
    assert.equal((await request(communityApi, '/api/v1/communities?municipality=Missing')).status, 200);
    assert.equal((await (await request(communityApi, '/api/v1/communities?municipality=Missing')).json()).length, 0);
    assert.equal((await fetch(communityApi + '/api/v1/internal/communities')).status, 401);
    const internal = await fetch(communityApi + '/api/v1/internal/communities', { headers: { 'X-Internal-Api-Key': internalKey } });
    assert.equal(internal.status, 200);
    assert.equal((await request(sensorApi, '/api/v1/communities')).status, 410, 'Legacy owner must not accept community operations');
    const input = { name: 'New private community', description: 'New', latitude: 15, longitude: -89, municipality: 'Izabal', department: 'Izabal', country: 'Guatemala' };
    assert.equal((await request(communityApi, '/api/v1/communities', 'POST', input, viewerToken)).status, 403);
    assert.equal((await request(communityApi, '/api/v1/communities', 'POST', { ...input, latitude: 91 })).status, 422);
    const newResponse = await request(communityApi, '/api/v1/communities', 'POST', input); assert.equal(newResponse.status, 201);
    const created = await newResponse.json();
    assert.equal((await request(communityApi, '/api/v1/communities', 'POST', input)).status, 409);
    const newSensor = await request(sensorApi, '/api/v1/sensors', 'POST', { name: 'Remote Sensor', code: 'REMOTE-01', type: 'Temperature', unit: '°C', communityId: created.id,
      latitude: 15, longitude: -89, description: 'HTTP reference' });
    assert.equal(newSensor.status, 201, 'Sensor must accept a community present ONLY in PostgreSQL');
    const sensor = await newSensor.json(); assert.equal(sensor.communityName, input.name);
    assert.equal((await source.request().input('id', sql.UniqueIdentifier, created.id).query('SELECT COUNT(*) AS count FROM dbo.Communities WHERE Id=@id')).recordset[0].count, 0,
      'Sensor must not copy the community catalog into SQL Server');
    const counts = await request(communityApi, `/api/v1/communities/${created.id}`); assert.equal((await counts.json()).sensorCount, 1);
    assert.equal((await request(communityApi, `/api/v1/communities/${created.id}/deactivate`, 'PATCH')).status, 204);
    const inactive = await fetch(`${sensorApi}/api/v1/internal/sensors/${sensor.id}/active`, { headers: { 'X-Internal-Api-Key': internalKey } });
    assert.equal(await inactive.json(), false);
    assert.equal((await request(communityApi, `/api/v1/communities/${created.id}/activate`, 'PATCH')).status, 204);
    const sensorList = await request(sensorApi, '/api/v1/sensors'); assert.equal(sensorList.status, 200);
    assert.equal((await sensorList.json()).find(x => x.id === sensor.id).communityName, input.name);
    community.kill('SIGTERM'); await once(community, 'exit');
    assert.equal((await request(sensorApi, `/api/v1/sensors/${sensor.id}`)).status, 503, 'Community failure must not fall back to stale SQL data');
    assert.ok((await pg.query('SELECT COUNT(*)::int AS count FROM "AuditOutbox"')).rows[0].count >= 3);
    console.log('Verified: preserved community IDs, repeat migration, sensor references without SQL catalog copies, counts via API, validation, roles, activation and dependency isolation.');
  } finally {
    for (const child of processes.reverse()) if (child.exitCode === null && child.signalCode === null) { child.kill('SIGTERM'); await once(child, 'exit'); }
    await source.close(); await pg.end();
  }
});
