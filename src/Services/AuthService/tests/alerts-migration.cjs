const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn, spawnSync } = require('node:child_process');
const { once } = require('node:events');
const { randomUUID } = require('node:crypto');
const sql = require('mssql');
const { Pool } = require('pg');
const path = require('node:path');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

test('Alert migration preserves rules, states, responsibility, identifiers and pending audit, with repeat execution', { timeout: 90000 }, async () => {
  const backend = process.env.PHASE3_TEST_BACKEND;
  const config = JSON.parse(process.env.SOURCE_SQLSERVER_CONFIG);
  const sourceName = 'Phase3AlertMigrationTest';
  const master = await new sql.ConnectionPool({ ...config, database: 'master' }).connect();
  await master.request().query(`IF DB_ID('${sourceName}') IS NULL CREATE DATABASE ${sourceName}`); await master.close();
  const sourceConfig = { ...config, database: sourceName };
  const source = await new sql.ConnectionPool(sourceConfig).connect();
  const pgAdmin = new Pool({ connectionString: process.env.AUTH_DATABASE_URL });
  await pgAdmin.query('CREATE DATABASE alerts_migration_test'); await pgAdmin.end();
  const destinationUrl = process.env.AUTH_DATABASE_URL.replace(/\/auth$/, '/alerts_migration_test');
  const pg = new Pool({ connectionString: destinationUrl });
  const internalKey = randomUUID() + randomUUID();
  const cwd = path.join(backend, 'src/Services/AlertService/Climate.Alerts.Api');
  const app = spawn('dotnet', [path.join(cwd, 'bin/Debug/net10.0/Climate.Alerts.Api.dll')], { cwd, env: {
    ...process.env, ASPNETCORE_URLS: 'http://127.0.0.1:15114',
    ConnectionStrings__AlertDb: `Server=127.0.0.1,15433;Database=${sourceName};User Id=sa;Password=${config.password};TrustServerCertificate=True;Encrypt=True`,
    Jwt__SigningKey: process.env.JWT_SIGNING_KEY, InternalApi__ApiKey: internalKey,
    SensorService__BaseUrl: 'http://127.0.0.1:15102/', SensorService__ApiKey: internalKey,
    EventService__ApiKey: internalKey, RealtimeService__ApiKey: internalKey,
    RABBITMQ_HOST: '127.0.0.1', RABBITMQ_PORT: '15672', RABBITMQ_USER: 'test', RABBITMQ_PASSWORD: process.env.PHASE3_TEST_RABBIT_PASSWORD,
    Logging__LogLevel__Default: 'Warning' }, stdio: ['ignore', 'pipe', 'pipe'] });
  let log = ''; app.stdout.on('data', b => { log = (log + b).slice(-4000); }); app.stderr.on('data', b => { log = (log + b).slice(-4000); });
  const migrate = () => spawnSync(process.execPath, [path.join(backend, 'tools/Phase3Migration/alerts.cjs')], {
    env: { ...process.env, SOURCE_SQLSERVER_CONFIG: JSON.stringify(sourceConfig), ALERT_POSTGRES_URL: destinationUrl }, encoding: 'utf8' });
  try {
    for (let attempt = 0; attempt < 60; attempt++) {
      if (app.exitCode !== null) assert.fail(log);
      if (await fetch('http://127.0.0.1:15114/health').then(r => r.ok).catch(() => false)) break;
      await pause(500);
    }
    assert.equal((await fetch('http://127.0.0.1:15114/health')).status, 200);
    app.kill('SIGTERM'); await once(app, 'exit');
    const rules = (await source.request().query('SELECT * FROM dbo.AlertRules ORDER BY Id')).recordset;
    assert.ok(rules.length > 0);
    const rule = rules[0];const alertId = randomUUID(), userId = randomUUID(), sensorId = randomUUID(), communityId = randomUUID();
    await source.request().input('id', sql.UniqueIdentifier, alertId).input('rule', sql.UniqueIdentifier, rule.Id)
      .input('user', sql.UniqueIdentifier, userId).input('sensor', sql.UniqueIdentifier, sensorId).input('community', sql.UniqueIdentifier, communityId)
      .input('risk', sql.NVarChar, rule.RiskType).input('level', sql.NVarChar, rule.AlertLevel).input('ruleName', sql.NVarChar, rule.Name)
      .query(`INSERT INTO dbo.Alerts(Id,SensorId,CommunityId,AlertType,Level,Title,Description,SensorValue,ThresholdValue,GeneratedAt,IsActive,
        Status,AttendedByUserId,AttendedAt,RuleId,RuleName,MinimumValueSnapshot,MaximumValueSnapshot)
        VALUES(@id,@sensor,@community,@risk,@level,'Historical alert','Preserved history',40,30,'2026-10-01T12:00:00Z',1,
        'Attended',@user,'2026-10-01T13:00:00Z',@rule,@ruleName,NULL,30)`);
    const eventId = randomUUID();
    const event = { EventId: eventId, OccurredAt: new Date().toISOString(), CorrelationId: 'migration-alert-test', UserId: userId,
      UserName: 'operator', Action: 'AttendAlert', Resource: 'Alert', ResourceId: alertId, Description: 'Historical audit', IpAddress: null };
    await source.request().input('id', sql.UniqueIdentifier, eventId).input('payload', sql.NVarChar, JSON.stringify(event))
      .query('INSERT dbo.AuditOutbox(Id,Payload,CreatedAt) VALUES(@id,@payload,SYSUTCDATETIME())');
    let fingerprint;
    for (let run = 0; run < 2; run++) {
      const result = migrate();assert.equal(result.status, 0, result.stderr);
      const summary = JSON.parse(result.stdout);assert.equal(summary.alert_rules, rules.length);assert.equal(summary.alerts, 1);assert.equal(summary.pendingAuditEvents, 1);
      if (fingerprint) assert.equal(summary.fingerprint, fingerprint); fingerprint = summary.fingerprint;
    }
    const alert = (await pg.query('SELECT payload FROM alerts WHERE id=$1', [alertId])).rows[0].payload;
    assert.equal(alert.ruleId, rule.Id.toLowerCase());assert.equal(alert.status, 'Attended');assert.equal(alert.attendedByUserId, userId);
    assert.equal(alert.sensorId, sensorId);assert.equal(alert.communityId, communityId);assert.equal(alert.sensorValue, 40);
    const originalRule = (await pg.query('SELECT payload FROM alert_rules WHERE id=$1', [rule.Id])).rows[0].payload;
    assert.equal(typeof originalRule.sensorType, 'string');assert.equal(originalRule.name, rule.Name);
    await pg.query("UPDATE alerts SET payload=jsonb_set(payload,'{title}','\"Changed locally\"') WHERE id=$1", [alertId]);
    assert.notEqual(migrate().status, 0, 'Destination conflicts must not be silently overwritten');
    assert.equal((await source.request().query('SELECT COUNT(*) AS count FROM dbo.Alerts')).recordset[0].count, 1);
    console.log('Verified: rules/alerts migrate twice, IDs and attended state/responsible user preserved, sensor enums converted, pending audit retained, collisions rejected and source preserved.');
  } finally {
    if (app.exitCode === null && app.signalCode === null) { app.kill('SIGTERM'); await once(app, 'exit'); }
    await source.close();await pg.end();
  }
});
