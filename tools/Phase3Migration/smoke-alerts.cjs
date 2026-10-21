const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const { Pool } = require('pg');
const { Tokens } = require('../../src/Services/AuthService/dist/tokens');
const amqp = require('../../src/Services/AuthService/node_modules/amqplib');
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
const api = 'http://127.0.0.1:15104';
async function eventually(check, message, limit = 60000) {
  const end = Date.now() + limit;
  while (Date.now() < end) { if (await check()) return; await wait(500); }
  assert.fail(message);
}
function compose(...args) {
  const result = spawnSync('docker', ['compose', '-f', process.env.PHASE3_TEST_COMPOSE, ...args], { encoding: 'utf8' });
  assert.equal(result.status, 0, 'Test fixture operation failed');
}
async function main() {
  const keepAlive = setInterval(() => {}, 1000);
  console.log('Starting Java runtime smoke.');
  const pg = new Pool({ connectionString: process.env.ALERT_POSTGRES_URL });
  let rabbit;
  try {
    await eventually(() => fetch(api + '/health/ready', { signal: AbortSignal.timeout(3000) }).then(r => r.ok).catch(() => false), 'Java container must become ready');
    console.log('Java runtime readiness verified.');
    assert.equal((await fetch(api + '/api/v1/alerts')).status, 401);
    const token = new Tokens().issue({ id: '11111111-1111-4111-8111-111111111111', username: 'admin', email: 'test@example.com', role: 'Administrator' }).accessToken;
    const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', 'X-Correlation-ID': 'java-runtime-smoke' };
    async function create(name) {
      const response = await fetch(api + '/api/v1/alert-rules', { method: 'POST', headers,
        body: JSON.stringify({ name, sensorType: 'Temperature', maximumValue: 30, alertLevel: 'Red', riskType: 'ForestFire', message: 'High temperature' }) });
      assert.equal(response.status, 201, 'NestJS JWT must be accepted by Java');
      return response.json();
    }
    const rule = await create('Runtime rule');
    console.log('Java accepted JWT emitted by NestJS and persisted a rule.');
    await eventually(async () => (await pg.query("SELECT count(*)::int AS count FROM delivery_outbox WHERE published_at IS NULL")).rows[0].count === 0,
      'Java audit event must be confirmed by real RabbitMQ');
    rabbit = await amqp.connect(process.env.RABBITMQ_URL);
    const channel = await rabbit.createChannel();
    const message = await channel.get('climate.audit.events', { noAck: false }); assert.ok(message);
    const event = JSON.parse(message.content.toString()); assert.equal(event.ResourceId, rule.id); assert.equal(event.SchemaVersion, 1);
    assert.equal(event.CorrelationId, 'java-runtime-smoke'); assert.equal(event.Action, 'CreateAlertRule'); channel.ack(message);
    await channel.close(); await rabbit.close(); rabbit = undefined;
    compose('stop', 'rabbitmq');
    await create('Broker outage rule');
    assert.ok((await pg.query('SELECT count(*)::int AS count FROM delivery_outbox WHERE published_at IS NULL')).rows[0].count > 0);
    compose('start', 'rabbitmq');
    await eventually(async () => (await pg.query('SELECT count(*)::int AS count FROM delivery_outbox WHERE published_at IS NULL')).rows[0].count === 0,
      'Java producer must reconnect and confirm pending audit after broker restart');
    console.log('Java container smoke passed: readiness, NestJS JWT, real RabbitMQ event contract, successful business writes during broker outage and durable recovery.');
  } finally { clearInterval(keepAlive); if (rabbit) await rabbit.close(); await pg.end(); }
}
require('node:test')('Java container, NestJS JWT and real RabbitMQ outage recovery', { timeout: 120000 }, main);
