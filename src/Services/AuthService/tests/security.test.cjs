const { test } = require('node:test');
const assert = require('node:assert/strict');
const { pbkdf2Sync, randomBytes } = require('node:crypto');
const jwt = require('jsonwebtoken');
const { hashPassword, verifyPassword } = require('../dist/passwords');
const { Tokens } = require('../dist/tokens');
const { roleClaim } = require('../dist/config');

test('ASP.NET Identity V2 and all V3 PRFs accept correct passwords and reject incorrect ones', async () => {
  const password = 'Legacy-Test-Passw0rd';
  const salt = randomBytes(16);
  const v2 = Buffer.concat([Buffer.from([0]), salt, pbkdf2Sync(password, salt, 1000, 32, 'sha1')]);
  assert.equal(await verifyPassword(password, v2.toString('base64')), true);
  assert.equal(await verifyPassword('wrong', v2.toString('base64')), false);
  for (const [prf, algorithm] of ['sha1', 'sha256', 'sha512'].entries()) {
    const header = Buffer.alloc(13);
    header[0] = 1; header.writeUInt32BE(prf, 1); header.writeUInt32BE(100000, 5); header.writeUInt32BE(16, 9);
    const encoded = Buffer.concat([header, salt, pbkdf2Sync(password, salt, 100000, 32, algorithm)]).toString('base64');
    assert.equal(await verifyPassword(password, encoded), true);
    assert.equal(await verifyPassword('wrong', encoded), false);
  }
});
test('new passwords use salted ASP.NET-compatible V3 hashes', async () => {
  const first = await hashPassword('New-Test-Passw0rd');
  const second = await hashPassword('New-Test-Passw0rd');
  assert.notEqual(first, second);
  assert.equal(Buffer.from(first, 'base64')[0], 1);
  assert.equal(await verifyPassword('New-Test-Passw0rd', first), true);
});
test('malformed password hashes are rejected without throwing', async () => {
  for (const input of ['', '!!!!', 'AQ==', 'a'.repeat(513), Buffer.alloc(60, 255).toString('base64')]) {
    assert.equal(await verifyPassword('password', input), false);
  }
});
test('JWT interoperable role claims and permissions for all three roles', () => {
  process.env.JWT_SIGNING_KEY = 'test-only-signing-key-with-at-least-32-bytes';
  const tokens = new Tokens();
  for (const role of ['Administrator', 'Operator', 'Viewer']) {
    const result = tokens.issue({ id: '11111111-1111-4111-8111-111111111111', username: 'test', email: 'test@example.com', role });
    const payload = tokens.verify(result.accessToken);
    assert.deepEqual(payload.roles, [role]);
    assert.equal(payload[roleClaim], role);
    assert.equal(payload.iss, 'ClimateMonitoring.Identity');
    assert.equal(payload.aud, 'ClimateMonitoring.Client');
    assert.equal(payload.permissions.includes('reports:download'), role === 'Administrator');
    assert.ok(Date.parse(result.expiresAt) > Date.now());
  }
});
test('JWT rejects wrong issuer, audience, algorithm, signature, expiration and malformed subject', () => {
  const secret = process.env.JWT_SIGNING_KEY = 'test-only-signing-key-with-at-least-32-bytes';
  const tokens = new Tokens();
  const claims = { sub: '11111111-1111-4111-8111-111111111111', [roleClaim]: 'Administrator' };
  const options = { algorithm: 'HS256', issuer: 'ClimateMonitoring.Identity', audience: 'ClimateMonitoring.Client', expiresIn: '1h' };
  for (const bad of [
    jwt.sign(claims, secret, { ...options, issuer: 'wrong' }),
    jwt.sign(claims, secret, { ...options, audience: 'wrong' }),
    jwt.sign(claims, secret, { ...options, algorithm: 'HS384' }),
    jwt.sign(claims, secret + 'wrong', options),
    jwt.sign(claims, secret, { ...options, expiresIn: -60 }),
    jwt.sign({ ...claims, sub: 'not-a-uuid' }, secret, options),
  ]) assert.throws(() => tokens.verify(bad));
});
