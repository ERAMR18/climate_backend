const { test } = require('node:test');
const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
require('reflect-metadata');
const { OAuthService } = require('../dist/oauth');
const hash = value => createHash('sha256').update(value).digest('base64url');
test('unknown providers and malformed browser PKCE are rejected before database access', async () => {
  const service = new OAuthService({}, {});
  await assert.rejects(service.start('google', 'bad', {}), /PKCE/);
  await assert.rejects(service.start('unknown', 'x'.repeat(43), {}), /Unsupported/);
});
test('missing provider credentials produce a controlled configuration error', async () => {
  delete process.env.OAUTH_GOOGLE_CLIENT_ID;
  const service = new OAuthService({}, {});
  await assert.rejects(service.start('google', 'x'.repeat(43), {}), /not configured/);
});
test('callback requires browser cookie and cannot redeem a replayed state', async () => {
  Object.assign(process.env, { OAUTH_GITHUB_CLIENT_ID: 'test-id', OAUTH_GITHUB_CLIENT_SECRET: 'test-secret',
    OAUTH_GITHUB_CALLBACK_URL: 'https://example.test/api/auth/oauth/github/callback', OAUTH_FRONTEND_URL: 'https://example.test' });
  let calls = 0;
  const service = new OAuthService({ pool: { query: async () => { calls++; return { rows: [] }; } } }, {});
  await assert.rejects(service.callback('github', { state: 'test' }, { headers: {} }, {}), /session missing/);
  assert.equal(calls, 0);
  await assert.rejects(service.callback('github', { state: 'test' }, { headers: { cookie: 'climate_oauth=browser' } }, {}), /replayed/);
  assert.equal(calls, 1);
});
test('handoff exchange validates browser verifier and active account; returns existing JWT roles', async () => {
  const verifier = 'v'.repeat(43);
  const user = { id: 'test-user', username: 'viewer', email: 'v@example.test', role: 'Viewer' };
  let calls = 0;
  const database = { transaction: action => action({ query: async sql => {
    calls++;
    if (sql.startsWith('DELETE')) return { rows: [{ user_id: user.id, challenge: hash(verifier) }] };
    if (sql.startsWith('UPDATE')) return { rows: [user] };
    return { rows: [] };
  } }) };
  const service = new OAuthService(database, { issue: value => { assert.equal(value.role, 'Viewer'); return { accessToken: 'signed-jwt' }; } });
  await assert.rejects(service.exchange('handoff', 'wrong'), /Invalid OAuth exchange/);
  calls = 0;
  const result = await service.exchange('handoff', verifier);
  assert.equal(result.accessToken, 'signed-jwt'); assert.equal(result.user.role, 'Viewer'); assert.equal(calls, 3);
});
test('GitHub authorization uses S256 PKCE, state and HttpOnly browser binding without OIDC nonce', async () => {
  Object.assign(process.env, { OAUTH_GITHUB_CLIENT_ID: 'test-id', OAUTH_GITHUB_CLIENT_SECRET: 'test-secret',
    OAUTH_GITHUB_CALLBACK_URL: 'https://example.test/api/auth/oauth/github/callback', OAUTH_FRONTEND_URL: 'https://example.test' });
  const writes = [];
  const service = new OAuthService({ pool: { query: async (sql, args) => { writes.push({sql, args}); return {}; } } }, {});
  let cookie;
  const url = new URL(await service.start('github', 'c'.repeat(43), { cookie: (...args) => { cookie = args; } }));
  assert.equal(url.origin, 'https://github.com'); assert.equal(url.searchParams.get('code_challenge_method'), 'S256');
  assert.equal(url.searchParams.get('state').length, 43); assert.equal(url.searchParams.has('nonce'), false);
  assert.equal(cookie[2].httpOnly, true); assert.equal(cookie[2].secure, true); assert.equal(cookie[2].sameSite, 'lax');
  assert.equal(writes[1].args[0], hash(url.searchParams.get('state')));
  assert.equal(writes[1].args[4], hash(cookie[1]));
});
