// Only emit fixed diagnostic labels. Provider errors may contain secrets or codes.
export function oauthFailureCode(error: unknown): string {
  const messages: Record<string, string> = {
    'OAuth session missing.': 'SESSION_MISSING',
    'Expired or replayed OAuth session.': 'SESSION_EXPIRED_OR_REPLAYED',
    'Sign in to the existing account to link this provider.': 'ACCOUNT_LINK_REQUIRED',
    'Identity already linked.': 'IDENTITY_ALREADY_LINKED',
    'Verified GitHub email required.': 'VERIFIED_EMAIL_REQUIRED',
    'Verified provider email required.': 'VERIFIED_EMAIL_REQUIRED',
    'GitHub identity unavailable.': 'GITHUB_IDENTITY_UNAVAILABLE',
    'Provider is not configured.': 'PROVIDER_NOT_CONFIGURED',
    'OAuth requires HTTPS.': 'HTTPS_REQUIRED',
  };
  const knownCodes = new Set(['OAUTH_INVALID_RESPONSE', 'OAUTH_RESPONSE_BODY_ERROR', 'OAUTH_AUTHORIZATION_RESPONSE_ERROR',
    'OAUTH_JWT_CLAIM_COMPARISON_FAILED', 'OAUTH_KEY_SELECTION_FAILED', 'ERR_JWT_EXPIRED', 'ERR_JWS_SIGNATURE_VERIFICATION_FAILED',
    'ECONNREFUSED', 'ENOTFOUND', 'ETIMEDOUT', 'UNABLE_TO_VERIFY_LEAF_SIGNATURE', 'CERT_HAS_EXPIRED', 'DEPTH_ZERO_SELF_SIGNED_CERT',
    'SELF_SIGNED_CERT_IN_CHAIN', 'ERR_TLS_CERT_ALTNAME_INVALID']);
  const providerCodes = new Set(['invalid_client', 'incorrect_client_credentials', 'bad_verification_code',
    'invalid_grant', 'redirect_uri_mismatch', 'access_denied']);
  let current: unknown = error;
  let fallback = 'CALLBACK_FAILED';
  for (let depth = 0; depth < 4 && current && typeof current === 'object'; depth++) {
    const value = current as { message?: unknown; code?: unknown; error?: unknown; cause?: unknown };
    if (typeof value.message === 'string' && Object.hasOwn(messages, value.message)) return messages[value.message];
    if (typeof value.error === 'string' && providerCodes.has(value.error)) return `PROVIDER_${value.error.toUpperCase()}`;
    if (typeof value.code === 'string' && knownCodes.has(value.code)) fallback = value.code;
    current = value.cause;
  }
  return fallback;
}
