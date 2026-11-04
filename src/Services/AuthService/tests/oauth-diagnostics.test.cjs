const { test } = require('node:test');
const assert = require('node:assert/strict');
require('reflect-metadata');
const { UnauthorizedException, Logger } = require('@nestjs/common');
const { oauthFailureCode } = require('../dist/oauth-diagnostics');
const { OAuthController } = require('../dist/oauth');

test('diagnostics distinguish session, linking, provider credentials and TLS errors using fixed labels', () => {
  assert.equal(oauthFailureCode(new UnauthorizedException('OAuth session missing.')), 'SESSION_MISSING');
  assert.equal(oauthFailureCode(new UnauthorizedException('Sign in to the existing account to link this provider.')), 'ACCOUNT_LINK_REQUIRED');
  assert.equal(oauthFailureCode({ code: 'OAUTH_RESPONSE_BODY_ERROR', cause: { error: 'incorrect_client_credentials' } }), 'PROVIDER_INCORRECT_CLIENT_CREDENTIALS');
  assert.equal(oauthFailureCode({ message: 'fetch failed', cause: { code: 'UNABLE_TO_VERIFY_LEAF_SIGNATURE' } }), 'UNABLE_TO_VERIFY_LEAF_SIGNATURE');
  assert.equal(oauthFailureCode({ message: 'secret-token', code: 'secret-token', cause: { error: 'secret-token' } }), 'CALLBACK_FAILED');
  assert.equal(oauthFailureCode({ message: 'constructor' }), 'CALLBACK_FAILED');
});

test('callback rejection exposes diagnostic headers and logs no URL, cookie, code or secret', async t => {
  const logs = [];
  t.mock.method(Logger.prototype, 'warn', message => logs.push(message));
  const controller = new OAuthController({ callback: async () => { throw new Error('secret-token'); } });
  const headers = {};
  const response = { setHeader(name, value) { headers[name] = value; }, status(value) { this.statusCode = value; return this; },
    type() { return this; }, send(value) { this.body = value; return this; } };
  await controller.callback('github', { code: 'secret-code' }, { headers: { cookie: 'secret-cookie' } }, response);
  assert.equal(response.statusCode, 401);
  assert.equal(headers['X-OAuth-Error'], 'CALLBACK_FAILED');
  assert.match(headers['X-OAuth-Reference'], /^[0-9a-f-]{36}$/);
  assert.equal(logs.length, 1);
  assert.doesNotMatch(JSON.stringify({ logs, headers, body: response.body }), /secret-token|secret-code|secret-cookie/);
});
