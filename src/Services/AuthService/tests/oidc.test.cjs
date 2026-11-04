const { test } = require('node:test');
const assert = require('node:assert/strict');
const { generateKeyPairSync } = require('node:crypto');
const jwt = require('jsonwebtoken');
require('reflect-metadata');
const { OAuthService } = require('../dist/oauth');

test('Google OIDC callback validates nonce and creates a Viewer identity without linking by email', async t => {
  Object.assign(process.env, { OAUTH_GOOGLE_CLIENT_ID: 'google-test', OAUTH_GOOGLE_CLIENT_SECRET: 'test-secret',
    OAUTH_GOOGLE_CALLBACK_URL: 'https://example.test/api/auth/oauth/google/callback', OAUTH_FRONTEND_URL: 'https://example.test' });
  const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  const jwk = publicKey.export({ format: 'jwk' });
  let flow, browser, state, invalid = '';
  const writes = [];
  const query = async (sql, args) => {
    if (sql.startsWith('INSERT INTO oauth_flows')) {
      flow = { provider: args[1], verifier: args[2], nonce: args[3], browser_hash: args[4], challenge: args[5], link_user_id: args[6] };
    }
    if (sql.startsWith('DELETE FROM oauth_flows WHERE state_hash')) return { rows: [flow] };
    if (sql.startsWith('INSERT INTO users')) writes.push({ sql, args });
    return { rows: [], rowCount: 0 };
  };
  const service = new OAuthService({ pool: { query }, transaction: action => action({ query }) }, {});
  const originalFetch = global.fetch;
  t.after(() => { global.fetch = originalFetch; });
  global.fetch = async input => {
    const url = String(input instanceof Request ? input.url : input);
    if (url.includes('.well-known')) return Response.json({ issuer: 'https://accounts.google.com',
      authorization_endpoint: 'https://accounts.google.com/auth', token_endpoint: 'https://accounts.google.com/token',
      jwks_uri: 'https://accounts.google.com/keys', response_types_supported: ['code'], subject_types_supported: ['public'], id_token_signing_alg_values_supported: ['RS256'] });
    if (url.endsWith('/keys')) return Response.json({ keys: [{ ...jwk, kid: 'test-key', alg: 'RS256', use: 'sig' }] });
    if (url.endsWith('/token')) return Response.json({ access_token: 'provider-access-token', token_type: 'Bearer', expires_in: 3600,
      id_token: jwt.sign({ sub: 'google-subject', email: 'external@example.test', email_verified: true,
        nonce: invalid === 'nonce' ? 'wrong' : flow.nonce }, invalid === 'signature' ? generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey : privateKey,
        { algorithm: 'RS256', keyid: 'test-key', issuer: invalid === 'issuer' ? 'https://attacker.test' : 'https://accounts.google.com',
          audience: invalid === 'audience' ? 'different-client' : 'google-test', expiresIn: invalid === 'expired' ? -300 : 300 }) });
    throw new Error(`Unexpected identity request ${url}`);
  };
  const begin = async () => {
    const url = new URL(await service.start('google', 'x'.repeat(43), { cookie: (name, value) => { browser = value; } }));
    state = url.searchParams.get('state');
    assert.equal(url.searchParams.get('nonce'), flow.nonce);
    assert.equal(url.searchParams.get('code_challenge_method'), 'S256');
  };
  await begin();
  const redirect = await service.callback('google', { code: 'provider-code', state }, { headers: { cookie: `climate_oauth=${browser}` } }, { clearCookie() {} });
  assert.match(redirect, /^https:\/\/example.test\/login#oauth_code=/);
  assert.equal(writes.length, 1);
  assert.match(writes[0].sql, /'Viewer'/);
  assert.equal(writes[0].args[3], 'external@example.test');
  for (invalid of ['nonce', 'issuer', 'audience', 'expired', 'signature']) {
    await begin();
    await assert.rejects(service.callback('google', { code: 'provider-code', state }, { headers: { cookie: `climate_oauth=${browser}` } }, { clearCookie() {} }));
  }
  assert.equal(writes.length, 1, 'Invalid identity cannot create an account');
});
