import { ForbiddenException, Injectable, NotFoundException, OnModuleInit, UnauthorizedException } from '@nestjs/common';
import { PoolClient } from 'pg';
import { randomUUID } from 'node:crypto';
import { Database } from './database';
import { Role } from './config';
import { CreateUserDto, LoginDto, RegisterDto, UpdateUserDto } from './dtos';
import { hashPassword, verifyPassword } from './passwords';
import { Identity, Tokens } from './tokens';

export interface UserRow {
  id: string; username: string; email: string; password_hash: string; role: Role;
  is_active: boolean; created_at: Date; updated_at: Date; last_login_at: Date | null;
}
export interface Actor { sub: string; unique_name: string; }
export interface AuditContext { actor?: Actor; correlationId: string; ipAddress?: string; }
function view(row: UserRow) {
  return { id: row.id, username: row.username, email: row.email, role: row.role, isActive: row.is_active,
    createdAt: row.created_at, updatedAt: row.updated_at, lastLoginAt: row.last_login_at };
}
const normalize = (value: string) => value.trim().toUpperCase();

@Injectable()
export class UsersService implements OnModuleInit {
  constructor(private readonly database: Database, private readonly tokens: Tokens) {}
  async onModuleInit(): Promise<void> {
    await this.database.initialize();
    const password = process.env.ADMIN_SEED_PASSWORD;
    if (!password) return;
    // Seed only an empty destination. Migration should run before first production startup.
    await this.database.transaction(async client => {
      await client.query('SELECT pg_advisory_xact_lock(31002)');
      if ((await client.query('SELECT 1 FROM users LIMIT 1')).rowCount) return;
      const input = { username: process.env.ADMIN_SEED_USERNAME ?? 'admin', email: process.env.ADMIN_SEED_EMAIL ?? 'admin@example.com', password };
      if (password.length < 12 || !/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/[0-9]/.test(password)) throw new Error('Invalid administrator seed password');
      await this.insert(client, input, 'Administrator', { correlationId: randomUUID() });
    });
  }
  private async audit(client: PoolClient, action: string, resourceId: string, context: AuditContext): Promise<void> {
    const eventId = randomUUID();
    const payload = { SchemaVersion: 1, EventId: eventId, OccurredAt: new Date().toISOString(), CorrelationId: context.correlationId,
      UserId: context.actor?.sub ?? 'ffffffff-ffff-ffff-ffff-ffffffffffff', UserName: context.actor?.unique_name ?? 'system',
      Action: action, Resource: action === 'Login' || action === 'Logout' ? 'Authentication' : 'User',
      ResourceId: resourceId, Description: `${action} user.`, IpAddress: context.ipAddress ?? null };
    await client.query('INSERT INTO audit_outbox(id,payload) VALUES($1,$2)', [eventId, JSON.stringify(payload)]);
  }
  private async insert(client: PoolClient, input: RegisterDto, role: Role, context: AuditContext) {
    const result = await client.query<UserRow>(`INSERT INTO users(id,username,normalized_username,email,normalized_email,password_hash,
      role,is_active,created_at,updated_at) VALUES($1,$2,$3,$4,$5,$6,$7,true,now(),now()) RETURNING *`,
    [randomUUID(), input.username.trim(), normalize(input.username), input.email.trim(), normalize(input.email), await hashPassword(input.password), role]);
    const user = result.rows[0];
    await this.audit(client, 'Create', user.id, { ...context, actor: context.actor ?? { sub: user.id, unique_name: user.username } });
    return view(user);
  }
  async create(input: RegisterDto | CreateUserDto, role: Role, context: AuditContext) {
    return this.database.transaction(client => this.insert(client, input, role, context));
  }
  async login(input: LoginDto, context: AuditContext) {
    return this.database.transaction(async client => {
      const result = await client.query<UserRow>('SELECT * FROM users WHERE normalized_username=$1 OR normalized_email=$1 FOR UPDATE', [normalize(input.login)]);
      const user = result.rows[0];
      if (!user || !(await verifyPassword(input.password, user.password_hash))) throw new UnauthorizedException('The supplied credentials are invalid.');
      if (!user.is_active) throw new ForbiddenException('The user account is inactive.');
      const updated = await client.query<UserRow>('UPDATE users SET last_login_at=now() WHERE id=$1 RETURNING *', [user.id]);
      await this.audit(client, 'Login', user.id, { ...context, actor: { sub: user.id, unique_name: user.username } });
      return { ...this.tokens.issue(user), user: view(updated.rows[0]) };
    });
  }
  async logout(identity: Identity, context: AuditContext): Promise<void> {
    await this.database.transaction(async client => {
      if (identity.jti && identity.exp) await client.query('INSERT INTO revoked_tokens(id,expires_at) VALUES($1,$2) ON CONFLICT DO NOTHING', [identity.jti, new Date(identity.exp * 1000)]);
      await this.audit(client, 'Logout', identity.sub, context);
    });
  }
  async list(search?: string, role?: string, active?: boolean) {
    const result = await this.database.pool.query<UserRow>(`SELECT * FROM users WHERE
      ($1::text IS NULL OR username ILIKE '%' || $1 || '%' OR email ILIKE '%' || $1 || '%')
      AND ($2::text IS NULL OR role=$2) AND ($3::boolean IS NULL OR is_active=$3) ORDER BY username`, [search ?? null, role ?? null, active ?? null]);
    return result.rows.map(view);
  }
  async get(id: string) {
    const result = await this.database.pool.query<UserRow>('SELECT * FROM users WHERE id=$1', [id]);
    if (!result.rows[0]) throw new NotFoundException('The requested user does not exist.');
    return view(result.rows[0]);
  }
  async update(id: string, input: UpdateUserDto, context: AuditContext) {
    return this.database.transaction(async client => {
      const result = await client.query<UserRow>(`UPDATE users SET username=$2, normalized_username=$3,email=$4,
        normalized_email=$5,role=$6,updated_at=now() WHERE id=$1 RETURNING *`,
      [id, input.username.trim(), normalize(input.username), input.email.trim(), normalize(input.email), input.role]);
      if (!result.rows[0]) throw new NotFoundException();
      await this.audit(client, 'Update', id, context);
      return view(result.rows[0]);
    });
  }
  async status(id: string, active: boolean, context: AuditContext): Promise<void> {
    await this.database.transaction(async client => {
      const result = await client.query('UPDATE users SET is_active=$2, updated_at=now() WHERE id=$1', [id, active]);
      if (!result.rowCount) throw new NotFoundException();
      await this.audit(client, active ? 'Activate' : 'Deactivate', id, context);
    });
  }
}
