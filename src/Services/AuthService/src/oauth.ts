import { BadRequestException, Body, Controller, Get, Injectable, Logger, Param, Post, Query, Req, Res, ServiceUnavailableException, UnauthorizedException, UseGuards } from '@nestjs/common';
import { Request, Response } from 'express';
import { createHash, randomBytes, randomUUID, timingSafeEqual } from 'node:crypto';
import * as oidc from 'openid-client';
import { Database } from './database';
import { Tokens } from './tokens';
import { AuthGuard, AuthRequest } from './auth.guard';
import { UserRow } from './users.service';
import { IsString, Matches, MaxLength } from 'class-validator';
import { oauthFailureCode } from './oauth-diagnostics';

const digest = (value: string) => createHash('sha256').update(value).digest('base64url');
const random = () => randomBytes(32).toString('base64url');
interface Provider { name: string; issuer: string; scope: string; oidc: boolean; authorization?: string; token?: string; }
// Extend with reviewed metadata; never accept arbitrary issuer URLs from HTTP input.
const providers: Provider[] = [
  { name: 'google', issuer: 'https://accounts.google.com', scope: 'openid email profile', oidc: true },
  { name: 'github', issuer: 'https://github.com/login/oauth', scope: 'read:user user:email', oidc: false,
    authorization: 'https://github.com/login/oauth/authorize', token: 'https://github.com/login/oauth/access_token' },
];
interface Flow { provider: string; verifier: string; nonce: string; browser_hash: string; challenge: string; link_user_id: string | null; }
class ExchangeDto {
  @IsString() @MaxLength(128) code!: string;
  @IsString() @Matches(/^[A-Za-z0-9_-]{43,128}$/) verifier!: string;
}

@Injectable()
export class OAuthService {
  constructor(private readonly database: Database, private readonly tokens: Tokens) {}
  private provider(name: string): Provider {
    const provider = providers.find(value => value.name === name);
    if (!provider) throw new BadRequestException('Unsupported provider.');
    return provider;
  }
  private settings(name: string) {
    const prefix = `OAUTH_${name.toUpperCase()}`;
    const clientId = process.env[`${prefix}_CLIENT_ID`];
    const secret = process.env[`${prefix}_CLIENT_SECRET`];
    const callback = process.env[`${prefix}_CALLBACK_URL`];
    const frontend = process.env.OAUTH_FRONTEND_URL;
    if (!clientId || !secret || !callback || !frontend) throw new ServiceUnavailableException('Provider is not configured.');
    for (const value of [callback, frontend]) {
      const url = new URL(value);
      if (url.protocol !== 'https:' && !(process.env.OAUTH_ALLOW_LOCAL_HTTP === 'true' && url.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(url.hostname)))
        throw new ServiceUnavailableException('OAuth requires HTTPS.');
    }
    return { clientId, secret, callback, frontend };
  }
  private async configuration(provider: Provider): Promise<oidc.Configuration> {
    const settings = this.settings(provider.name);
    if (provider.oidc) {
      const configuration = await oidc.discovery(new URL(provider.issuer), settings.clientId, settings.secret);
      oidc.enableNonRepudiationChecks(configuration);
      return configuration;
    }
    return new oidc.Configuration({ issuer: provider.issuer, authorization_endpoint: provider.authorization,
      token_endpoint: provider.token }, settings.clientId, settings.secret, oidc.ClientSecretPost(settings.secret));
  }
  async start(name: string, challenge: string, response: Response, linkUserId?: string): Promise<string> {
    if (!/^[A-Za-z0-9_-]{43}$/.test(challenge)) throw new BadRequestException('Missing browser PKCE challenge.');
    const provider = this.provider(name);
    const config = await this.configuration(provider);
    const state = random(); const browser = random(); const verifier = oidc.randomPKCECodeVerifier(); const nonce = oidc.randomNonce();
    await this.database.pool.query('DELETE FROM oauth_flows WHERE expires_at < now()');
    await this.database.pool.query(`INSERT INTO oauth_flows(state_hash,provider,verifier,nonce,browser_hash,challenge,link_user_id,expires_at)
      VALUES($1,$2,$3,$4,$5,$6,$7,now()+interval '5 minutes')`, [digest(state), name, verifier, nonce, digest(browser), challenge, linkUserId ?? null]);
    const settings = this.settings(name);
    response.cookie('climate_oauth', browser, { httpOnly: true, secure: new URL(settings.callback).protocol === 'https:', sameSite: 'lax', maxAge: 300000, path: '/' });
    return oidc.buildAuthorizationUrl(config, { redirect_uri: settings.callback, scope: provider.scope,
      response_type: 'code', state, code_challenge: await oidc.calculatePKCECodeChallenge(verifier), code_challenge_method: 'S256',
      ...(provider.oidc ? { nonce } : {}) }).href;
  }
  async callback(name: string, query: Record<string, string>, request: Request, response: Response): Promise<string> {
    const provider = this.provider(name); const settings = this.settings(name);
    const browser = request.headers.cookie?.split(';').map(value => value.trim()).find(value => value.startsWith('climate_oauth='))?.slice(14);
    if (!browser || !query.state) throw new UnauthorizedException('OAuth session missing.');
    const result = await this.database.pool.query<Flow>(`DELETE FROM oauth_flows WHERE state_hash=$1 AND provider=$2
      AND browser_hash=$3 AND expires_at>now() RETURNING *`, [digest(query.state), name, digest(browser)]);
    const flow = result.rows[0];
    if (!flow) throw new UnauthorizedException('Expired or replayed OAuth session.');
    response.clearCookie('climate_oauth', { path: '/' });
    const config = await this.configuration(provider);
    const callback = new URL(settings.callback);
    for (const [key, value] of Object.entries(query)) if (typeof value === 'string') callback.searchParams.set(key, value);
    const tokens = await oidc.authorizationCodeGrant(config, callback, { pkceCodeVerifier: flow.verifier,
      expectedState: query.state, ...(provider.oidc ? { expectedNonce: flow.nonce, idTokenExpected: true } : {}) });
    let subject: string; let email: string;
    if (provider.oidc) {
      const claims = tokens.claims();
      if (!claims?.sub || claims.email_verified !== true || typeof claims.email !== 'string') throw new UnauthorizedException('Verified provider email required.');
      subject = claims.sub; email = claims.email;
    } else {
      const headers = { Authorization: `Bearer ${tokens.access_token}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28' };
      const identity = await fetch('https://api.github.com/user', { headers, signal: AbortSignal.timeout(10000) });
      const emails = await fetch('https://api.github.com/user/emails', { headers, signal: AbortSignal.timeout(10000) });
      if (!identity.ok || !emails.ok) throw new UnauthorizedException('GitHub identity unavailable.');
      const user = await identity.json() as { id: number };
      const addresses = await emails.json() as { email: string; primary: boolean; verified: boolean }[];
      const verified = addresses.find(value => value.primary && value.verified);
      if (!Number.isSafeInteger(user.id) || !verified) throw new UnauthorizedException('Verified GitHub email required.');
      subject = String(user.id); email = verified.email;
    }
    const handoff = random();
    await this.database.transaction(async client => {
      await client.query('SELECT pg_advisory_xact_lock(41004)');
      const identities = await client.query<{ user_id: string }>('SELECT user_id FROM external_identities WHERE provider=$1 AND subject=$2', [name, subject]);
      let userId = identities.rows[0]?.user_id;
      if (flow.link_user_id && userId && userId !== flow.link_user_id) throw new UnauthorizedException('Identity already linked.');
      if (!userId) {
        userId = flow.link_user_id ?? randomUUID();
        if (!flow.link_user_id) {
          // Email collisions require an authenticated explicit link; never merge accounts by email.
          if ((await client.query('SELECT 1 FROM users WHERE normalized_email=$1', [email.toUpperCase()])).rowCount)
            throw new UnauthorizedException('Sign in to the existing account to link this provider.');
          const username = `external_${randomBytes(12).toString('hex')}`;
          await client.query(`INSERT INTO users(id,username,normalized_username,email,normalized_email,password_hash,role,is_active,created_at,updated_at)
            VALUES($1,$2,$3,$4,$5,'EXTERNAL_ONLY','Viewer',true,now(),now())`, [userId, username, username.toUpperCase(), email, email.toUpperCase()]);
        }
        await client.query('INSERT INTO external_identities(provider,subject,user_id) VALUES($1,$2,$3)', [name, subject, userId]);
      }
      await client.query('DELETE FROM oauth_handoffs WHERE expires_at<now()');
      await client.query(`INSERT INTO oauth_handoffs(code_hash,user_id,challenge,expires_at) VALUES($1,$2,$3,now()+interval '60 seconds')`, [digest(handoff), userId, flow.challenge]);
    });
    return `${settings.frontend.replace(/\/$/, '')}/login#oauth_code=${handoff}`;
  }
  async exchange(code: string, verifier: string) {
    return this.database.transaction(async client => {
      const result = await client.query<{ user_id: string; challenge: string }>('DELETE FROM oauth_handoffs WHERE code_hash=$1 AND expires_at>now() RETURNING *', [digest(code)]);
      const handoff = result.rows[0];
      const actual = Buffer.from(digest(verifier)); const expected = Buffer.from(handoff?.challenge ?? '');
      if (!handoff || actual.length !== expected.length || !timingSafeEqual(actual, expected)) throw new UnauthorizedException('Invalid OAuth exchange.');
      const users = await client.query<UserRow>('UPDATE users SET last_login_at=now() WHERE id=$1 AND is_active=true RETURNING *', [handoff.user_id]);
      const user = users.rows[0];
      if (!user) throw new UnauthorizedException('Account unavailable.');
      const eventId = randomUUID();
      await client.query('INSERT INTO audit_outbox(id,payload) VALUES($1,$2)', [eventId, JSON.stringify({ SchemaVersion: 1, EventId: eventId, OccurredAt: new Date().toISOString(), CorrelationId: eventId, UserId: user.id, UserName: user.username, Action: 'Login', Resource: 'Authentication', ResourceId: user.id, Description: 'External provider login.', IpAddress: null })]);
      return { ...this.tokens.issue(user), user: { id: user.id, username: user.username, email: user.email, role: user.role, isActive: user.is_active } };
    });
  }
}

@Controller('api/v1/auth/oauth')
export class OAuthController {
  private readonly logger = new Logger(OAuthController.name);
  constructor(private readonly oauth: OAuthService) {}
  @Get(':provider/start') async start(@Param('provider') provider: string, @Query('challenge') challenge: string, @Res() response: Response) {
    response.setHeader('Cache-Control', 'no-store');
    response.redirect(await this.oauth.start(provider, challenge, response));
  }
  @Post(':provider/link') @UseGuards(AuthGuard)
  async link(@Param('provider') provider: string, @Body('challenge') challenge: string, @Req() request: AuthRequest, @Res() response: Response) {
    response.json({ url: await this.oauth.start(provider, challenge, response, request.identity.sub) });
  }
  @Get(':provider/callback') async callback(@Param('provider') provider: string, @Query() query: Record<string, string>, @Req() request: Request, @Res() response: Response) {
    response.setHeader('Cache-Control', 'no-store'); response.setHeader('Referrer-Policy', 'no-referrer');
    try { response.redirect(await this.oauth.callback(provider, query, request, response)); }
    catch (error: unknown) {
      const reason = oauthFailureCode(error);
      const reference = randomUUID();
      const safeProvider = providers.some(value => value.name === provider) ? provider : 'unknown';
      this.logger.warn(`OAuth callback rejected provider=${safeProvider} reason=${reason} reference=${reference}`);
      response.setHeader('X-OAuth-Error', reason);
      response.setHeader('X-OAuth-Reference', reference);
      response.status(401).type('text/plain').send(`No fue posible validar el proveedor. Diagnóstico: ${reason}. Referencia: ${reference}. Vuelve al inicio de sesión e inténtalo nuevamente.`);
    }
  }
  @Post('exchange') async exchange(@Body() input: ExchangeDto, @Res() response: Response) {
    response.setHeader('Cache-Control', 'no-store'); response.json(await this.oauth.exchange(input.code, input.verifier));
  }
}
