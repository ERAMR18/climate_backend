const { test } = require('node:test');
const assert = require('node:assert/strict');
require('reflect-metadata');
const { OAuthService } = require('../dist/oauth');

test('GitHub callback accepts its official issuer and rejects a different issuer before token exchange', async t => {
  Object.assign(process.env, { OAUTH_GITHUB_CLIENT_ID: 'test-id', OAUTH_GITHUB_CLIENT_SECRET: 'test-secret',
    OAUTH_GITHUB_CALLBACK_URL: 'https://example.test/api/auth/oauth/github/callback', OAUTH_FRONTEND_URL: 'https://example.test' });
  let flow, browser, tokenCalls = 0;
  const query = async (sql, args) => {
    if (sql.startsWith('INSERT INTO oauth_flows')) flow = { provider: args[1], verifier: args[2], nonce: args[3], browser_hash: args[4], challenge: args[5], link_user_id: null };
    if (sql.startsWith('DELETE FROM oauth_flows WHERE state_hash')) return { rows: [flow] };
    return { rows: [], rowCount: 0 };
  };
  const service = new OAuthService({ pool: { query }, transaction: action => action({ query }) }, {});
  const originalFetch = global.fetch;
  t.after(() => { global.fetch = originalFetch; });
  global.fetch = async (input, options) => {
    const url = String(input instanceof Request ? input.url : input);
    if (url === 'https://github.com/login/oauth/access_token') {
      tokenCalls++;
      assert.equal(new URLSearchParams(String(options.body)).get('code_verifier'), flow.verifier);
      return Response.json({ access_token: 'test-access-token', token_type: 'Bearer', scope: 'read:user user:email' });
    }
    if (url === 'https://api.github.com/user') return Response.json({ id: 123 });
    if (url === 'https://api.github.com/user/emails') return Response.json([{ email: 'external@example.test', primary: true, verified: true }]);
    throw new Error('Unexpected identity request');
  };
  async function callback(issuer) {
    const start = new URL(await service.start('github', 'c'.repeat(43), { cookie: (name, value) => { browser = value; } }));
    return service.callback('github', { code: 'test-code', state: start.searchParams.get('state'), iss: issuer },
      { headers: { cookie: `climate_oauth=${browser}` } }, { clearCookie() {} });
  }
  const redirect = await callback('https://github.com/login/oauth');
  assert.match(redirect, /^https:\/\/example.test\/login#oauth_code=/);
  assert.equal(tokenCalls, 1);
  await assert.rejects(callback('https://attacker.example'));
  assert.equal(tokenCalls, 1, 'Issuer mismatch must be rejected before using the authorization code');
});
