import { Injectable, UnauthorizedException } from '@nestjs/common';
import jwt, { JwtPayload } from 'jsonwebtoken';
import { randomUUID } from 'node:crypto';
import { permissions, positiveInteger, required, Role, roleClaim } from './config';

export interface Identity extends JwtPayload { sub: string; unique_name: string; roles: Role[]; permissions: string[]; }
export interface ExternalIdentityProvider {
  readonly name: string;
  validateCallback(code: string, state: string): Promise<{ subject: string; email?: string }>;
}

@Injectable()
export class Tokens {
  private readonly secret = required('JWT_SIGNING_KEY');
  private readonly issuer = process.env.JWT_ISSUER ?? 'ClimateMonitoring.Identity';
  private readonly audience = process.env.JWT_AUDIENCE ?? 'ClimateMonitoring.Client';
  private readonly duration = positiveInteger('JWT_EXPIRATION_MINUTES', 60);
  constructor() { if (Buffer.byteLength(this.secret) < 32) throw new Error('JWT_SIGNING_KEY must contain at least 32 bytes'); }
  issue(user: { id: string; username: string; email: string; role: Role }, durationMinutes = this.duration): { accessToken: string; expiresAt: string } {
    const now = Math.floor(Date.now() / 1000);
    const exp = now + durationMinutes * 60;
    const accessToken = jwt.sign({ sub: user.id, unique_name: user.username, email: user.email,
      [roleClaim]: user.role, roles: [user.role], permissions: permissions(user.role),
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': user.id,
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': user.username,
      jti: randomUUID(), iat: now, nbf: now, exp }, this.secret,
    { algorithm: 'HS256', issuer: this.issuer, audience: this.audience });
    return { accessToken, expiresAt: new Date(exp * 1000).toISOString() };
  }
  verify(value: string): Identity {
    try {
      const payload = jwt.verify(value, this.secret, { algorithms: ['HS256'], issuer: this.issuer,
        audience: this.audience, clockTolerance: 30 });
      if (typeof payload === 'string' || typeof payload.sub !== 'string'
        || !/^[0-9a-f-]{36}$/i.test(payload.sub) || !payload.exp) throw new Error('Invalid subject');
      const role = payload[roleClaim];
      if (!['Administrator', 'Operator', 'Viewer'].includes(role)) throw new Error('Invalid role');
      return { ...payload, sub: payload.sub, unique_name: payload.unique_name,
        roles: [role], permissions: Array.isArray(payload.permissions) ? payload.permissions : [] } as Identity;
    } catch { throw new UnauthorizedException('Invalid or expired token.'); }
  }
}
