import { CanActivate, ExecutionContext, ForbiddenException, Injectable, UnauthorizedException } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { Request } from 'express';
import { Database } from './database';
import { Identity, Tokens } from './tokens';

export interface AuthRequest extends Request { identity: Identity; correlationId: string; }

@Injectable()
export class AuthGuard implements CanActivate {
  constructor(private readonly tokens: Tokens, private readonly database: Database, private readonly reflector: Reflector) {}
  async canActivate(context: ExecutionContext): Promise<boolean> {
    const req = context.switchToHttp().getRequest<AuthRequest>();
    const header = req.headers.authorization;
    if (!header?.startsWith('Bearer ')) throw new UnauthorizedException();
    req.identity = this.tokens.verify(header.slice(7));
    const result = await this.database.pool.query('SELECT role, is_active FROM users WHERE id=$1', [req.identity.sub]);
    const user = result.rows[0];
    if (!user || !user.is_active) throw new UnauthorizedException('Account unavailable.');
    const revoked = await this.database.pool.query('SELECT 1 FROM revoked_tokens WHERE id=$1', [req.identity.jti]);
    if (revoked.rowCount) throw new UnauthorizedException('Session logged out.');
    const admin = this.reflector.getAllAndOverride<boolean>('admin', [context.getHandler(), context.getClass()]);
    if (admin && (user.role !== 'Administrator' || !req.identity.roles.includes('Administrator'))) throw new ForbiddenException();
    return true;
  }
}
