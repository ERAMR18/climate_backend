const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn, spawnSync } = require('node:child_process');
const { once } = require('node:events');
const { Pool } = require('pg');
const path = require('node:path');
const { Tokens } = require('../dist/tokens');
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

test('real Sensor → Monitoring → Java Alert → Event/SignalR through YARP, including dashboard and replay', { timeout: 360000 }, async () => {
  const keepAlive = setInterval(() => {}, 1000);
  const backend = process.env.PHASE3_TEST_BACKEND;
  const children = []; let socket;
  const token = new Tokens().issue({ id: '11111111-1111-4111-8111-111111111111', username: 'admin', email: 'admin@example.com', role: 'Administrator' }).accessToken;
  const pg = new Pool({ connectionString: `postgresql://auth:${process.env.PHASE3_TEST_POSTGRES_PASSWORD}@127.0.0.1:15432/auth` });
  const env = { ...process.env, Jwt__SigningKey: process.env.JWT_SIGNING_KEY, Jwt__Issuer: 'ClimateMonitoring.Identity', Jwt__Audience: 'ClimateMonitoring.Client',
    InternalApi__ApiKey: process.env.INTERNAL_API_KEY, RABBITMQ_HOST: '127.0.0.1', RABBITMQ_PORT: '15672', RABBITMQ_USER: 'test', RABBITMQ_PASSWORD: process.env.PHASE3_TEST_RABBIT_PASSWORD,
    SensorService__BaseUrl: 'http://127.0.0.1:15102/', SensorService__ApiKey: process.env.INTERNAL_API_KEY,
    AlertService__BaseUrl: 'http://127.0.0.1:15104/', AlertService__ApiKey: process.env.INTERNAL_API_KEY,
    EventService__BaseUrl: 'http://127.0.0.1:15105/', EventService__ApiKey: process.env.INTERNAL_API_KEY,
    CommunityService__BaseUrl: 'http://127.0.0.1:15107/', RealtimeService__BaseUrl: 'http://127.0.0.1:15103/', RealtimeService__ApiKey: process.env.INTERNAL_API_KEY,
    Logging__LogLevel__Default: 'Warning' };
  function dotnet(folder, assembly, extra) {
    const cwd = path.join(backend, folder);
    const child = spawn('dotnet', [path.join(cwd, 'bin/Debug/net10.0', assembly + '.dll')], { cwd, env: { ...env, ...extra }, stdio: ['ignore', 'pipe', 'pipe'] });
    let logs = ''; child.stdout.on('data', b => { logs = (logs + b).slice(-6000); }); child.stderr.on('data', b => { logs = (logs + b).slice(-6000); });
    child.logs = () => logs; children.push(child); return child;
  }
  async function until(check, message, limit = 45000) {
    const end = Date.now() + limit;
    while (Date.now() < end) { if (await check()) return; await delay(500); }
    assert.fail(message + children.map(c => c.logs()).join('\n'));
  }
  const sqlConnection = database => `Server=127.0.0.1,15433;Database=${database};User Id=sa;Password=${process.env.PHASE3_TEST_SQL_PASSWORD};TrustServerCertificate=True;Encrypt=True`;
  async function request(route, method = 'GET', body) {
    const response = await fetch('http://127.0.0.1:15180' + route, { method, headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
    assert.ok(response.ok, `${method} ${route}: ${response.status} ${await (!response.ok ? response.text() : Promise.resolve(''))}`);
    return response.status === 204 ? undefined : response.json();
  }
  try {
    await pg.query('CREATE DATABASE community'); await pg.query('CREATE DATABASE alerts_runtime');
    dotnet('src/Services/CommunityService/Climate.Communities.Api', 'Climate.Communities.Api', { ASPNETCORE_URLS: 'http://0.0.0.0:15107',
      ConnectionStrings__CommunityDb: `Host=127.0.0.1;Port=15432;Database=community;Username=auth;Password=${process.env.PHASE3_TEST_POSTGRES_PASSWORD}` });
    dotnet('src/Services/SensorService/Climate.Sensors.Api', 'Climate.Sensors.Api', { ASPNETCORE_URLS: 'http://0.0.0.0:15102',
      ConnectionStrings__SensorDb: sqlConnection('Phase3SystemSensors'), CommunityService__Enabled: 'true', DemoSeed__Enabled: 'false' });
    dotnet('src/Services/EventService/Climate.Events.Api', 'Climate.Events.Api', { ASPNETCORE_URLS: 'http://0.0.0.0:15105', ConnectionStrings__EventDb: sqlConnection('Phase3SystemEvents') });
    dotnet('src/Services/MonitoringService/Climate.Monitoring.Api', 'Climate.Monitoring.Api', { ASPNETCORE_URLS: 'http://0.0.0.0:15103',
      ConnectionStrings__MonitoringDb: sqlConnection('Phase3SystemMonitoring'), Simulation__Enabled: 'false' });
    const javaEnv = { ...process.env, ALERT_DATABASE_URL: 'jdbc:postgresql://postgres:5432/alerts_runtime', ALERT_DATABASE_USER: 'auth',
      ALERT_DATABASE_PASSWORD: process.env.PHASE3_TEST_POSTGRES_PASSWORD, RABBITMQ_PASSWORD: process.env.PHASE3_TEST_RABBIT_PASSWORD };
    const java = spawnSync('docker', ['run', '--rm', '-d', '--name', 'climate-phase3-system-alert', '--network', 'climate-phase3-auth-validation_default', '-p', '127.0.0.1:15104:8080',
      '-e','ALERT_DATABASE_URL','-e','ALERT_DATABASE_USER','-e','ALERT_DATABASE_PASSWORD','-e','JWT_SIGNING_KEY','-e','INTERNAL_API_KEY','-e','RABBITMQ_PASSWORD',
      '-e','RABBITMQ_HOST=rabbitmq','-e','RABBITMQ_USER=test','-e','SENSOR_SERVICE_URL=http://host.docker.internal:15102/',
      '-e','EVENT_SERVICE_URL=http://host.docker.internal:15105/','-e','REALTIME_SERVICE_URL=http://host.docker.internal:15103/', 'climate/alerts:phase3'], { env: javaEnv, encoding: 'utf8' });
    assert.equal(java.status, 0, java.stderr);
    const gatewayEnv = { ASPNETCORE_URLS: 'http://127.0.0.1:15180', ReverseProxy__Routes__communities__ClusterId: 'communities', RateLimit__PermitLimit:'10000' };
    if (process.env.PHASE3_TEST_REPORTS === 'true') {
      const auth = spawn(process.execPath, [path.join(__dirname, '../dist/main.js')], {env: {...process.env,
        PORT:'15101', AUTH_DATABASE_URL:`postgresql://auth:${process.env.PHASE3_TEST_POSTGRES_PASSWORD}@127.0.0.1:15432/auth`,
        RABBITMQ_URL:`amqp://test:${process.env.PHASE3_TEST_RABBIT_PASSWORD}@127.0.0.1:15672/`}, stdio:['ignore','pipe','pipe']});
      let logs=''; for (const stream of [auth.stdout,auth.stderr]) stream.on('data', b=>{logs=(logs+b).slice(-6000);});
      auth.logs=()=>logs; children.push(auth);
      const reportEnv={...process.env,MONGO_URL:`mongodb://audit:${process.env.PHASE3_TEST_POSTGRES_PASSWORD}@mongo:27017/?authSource=admin`,
        RABBITMQ_URL:`amqp://test:${process.env.PHASE3_TEST_RABBIT_PASSWORD}@rabbitmq:5672/`};
      const args=['run','--rm','-d','--name','climate-phase3-system-reports','--network','climate-phase3-auth-validation_default',
        '-p','127.0.0.1:15108:8080','-e','MONGO_URL','-e','RABBITMQ_URL','-e','JWT_SIGNING_KEY','-e','INTERNAL_API_KEY'];
      for (const [service,port] of [['AUTH',15101],['COMMUNITY',15107],['SENSOR',15102],['MONITORING',15103],['ALERT',15104],['EVENT',15105]])
        args.push('-e',`${service}_SERVICE_URL=http://host.docker.internal:${port}/`);
      args.push('climate/reports:phase3');
      if (Number(process.env.PHASE3_TEST_REPORT_ROWS)>261) args.splice(args.length-1,0,'-e',`REPORT_MAX_ROWS=${process.env.PHASE3_TEST_REPORT_ROWS}`);
      const report=spawnSync('docker',args,{env:reportEnv,encoding:'utf8'}); assert.equal(report.status,0,report.stderr);
      Object.assign(gatewayEnv,{
        ReverseProxy__Routes__reports__ClusterId:'reports',ReverseProxy__Routes__reports__AuthorizationPolicy:'Default',
        ReverseProxy__Routes__reports__Match__Path:'/api/reports/{**remainder}',
        ReverseProxy__Routes__reports__Transforms__0__PathRemovePrefix:'/api/reports',
        ReverseProxy__Routes__reports__Transforms__1__PathPrefix:'/api/v1/reports',
        ReverseProxy__Clusters__reports__Destinations__primary__Address:'http://127.0.0.1:15108/'});
    }
    for (const [cluster, port] of [['sensors',15102],['monitoring',15103],['alerts',15104],['events',15105],['communities',15107]]) gatewayEnv[`ReverseProxy__Clusters__${cluster}__Destinations__primary__Address`] = `http://127.0.0.1:${port}/`;
    dotnet('src/Gateway/Climate.Gateway', 'Climate.Gateway', gatewayEnv);
    for (const port of [15102,15103,15104,15105,15107,15180]) await until(() => fetch(`http://127.0.0.1:${port}/health/live`).then(r => r.ok).catch(() => false), `Service ${port} failed to start`);
    const community = await request('/api/communities', 'POST', { name: 'System community', latitude: 14, longitude: -90, municipality: 'Guatemala', department: 'Guatemala', country: 'Guatemala' });
    const sensor = await request('/api/sensors', 'POST', { name: 'System temperature', code: 'SYSTEM-TEMP', type: 'Temperature', unit: '°C', communityId: community.id, latitude: 14, longitude: -90 });
    await request('/api/alert-rules', 'POST', { name: 'System hot weather', sensorType: 'Temperature', maximumValue: 30, alertLevel: 'Red', riskType: 'ForestFire', message: 'High temperature', isActive: true });
    const negotiation = await request('/hubs/monitoring/negotiate?negotiateVersion=1', 'POST');
    const messages = []; let handshake = false;
    socket = new WebSocket(`ws://127.0.0.1:15180/hubs/monitoring?id=${encodeURIComponent(negotiation.connectionToken)}&access_token=${encodeURIComponent(token)}`);
    socket.addEventListener('open', () => socket.send('{"protocol":"json","version":1}\u001e'));
    socket.addEventListener('message', event => { for (const frame of String(event.data).split('\u001e').filter(Boolean)) {
      const message = JSON.parse(frame); if (message.type === undefined) handshake = true;
      if (message.type === 1) messages.push(message); if (message.type === 6) socket.send('{"type":6}\u001e');
    } });
    await until(() => handshake, 'SignalR handshake failed');
    const reading = await request('/api/monitoring/readings', 'POST', { sensorId: sensor.id, value: 45 });
    const alerts = await request('/api/alerts?sensorId=' + sensor.id); assert.equal(alerts.length, 1); const alert = alerts[0];
    await until(async () => (await request('/api/events?sensorId=' + sensor.id)).length === 1, 'Java event delivery failed');
    await until(() => messages.some(x => x.target === 'AlertGenerated') && messages.some(x => x.target === 'SensorReadingUpdated'), 'SignalR notifications missing');
    const summary = await request('/api/dashboard/summary?communityId=' + community.id); assert.equal(summary.communityCount, 1); assert.equal(summary.activeSensorCount, 1); assert.equal(summary.activeAlertCount, 1);
    assert.equal(summary.events.byRiskType.ForestFire,1);
    const statistic=summary.climateStatistics.find(item=>item.sensorType==='Temperature');
    assert.deepEqual({count:statistic.count,average:statistic.average,minimum:statistic.minimum,maximum:statistic.maximum},{count:1,average:45,minimum:45,maximum:45});
    await request(`/api/alerts/${alert.id}/attend`, 'PATCH'); await request(`/api/alerts/${alert.id}/close`, 'PATCH');
    await until(async () => (await request('/api/events?sensorId=' + sensor.id))[0].status === 'Closed', 'Event workflow not synchronized');
    await until(() => messages.some(x => x.target === 'AlertClosed'), 'AlertClosed notification missing');
    const internalReading = { eventId: reading.id, occurredAt: reading.recordedAt, correlationId: reading.id, readingId: reading.id, sensorId: sensor.id,
      communityId: community.id, sensorType: 0, value: 45, unit: '°C', recordedAt: reading.recordedAt };
    const replay = await fetch('http://127.0.0.1:15104/api/v1/internal/risk-evaluations', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Internal-Api-Key': process.env.INTERNAL_API_KEY }, body: JSON.stringify(internalReading) });
    assert.equal(replay.status, 200); assert.equal((await request('/api/alerts?sensorId=' + sensor.id)).length, 1);
    console.log('Verified real cross-language evaluation, event history/workflow, SignalR via YARP, dashboard ownership and idempotent reading replay.');
    if (process.env.PHASE3_TEST_REPORTS === 'true') await require('./reports.integration.cjs')({request,until,token,Tokens,backend,sensor,community});
  } finally {
    socket?.close();
    for (const child of children.reverse()) if (child.exitCode === null && child.signalCode === null) { child.kill('SIGTERM'); await once(child, 'exit'); }
    await pg.end(); clearInterval(keepAlive);
  }
});
