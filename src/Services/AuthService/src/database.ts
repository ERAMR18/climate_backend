import { Injectable, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { Pool, PoolClient } from 'pg';
import { required } from './config';

export const schema = `
CREATE TABLE IF NOT EXISTS users (
 id uuid PRIMARY KEY, username varchar(50) NOT NULL, normalized_username varchar(50) NOT NULL UNIQUE,
 email varchar(254) NOT NULL, normalized_email varchar(254) NOT NULL UNIQUE,
 password_hash varchar(512) NOT NULL, role varchar(32) NOT NULL CHECK(role IN ('Administrator','Operator','Viewer')),
 is_active boolean NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, last_login_at timestamptz
);
CREATE INDEX IF NOT EXISTS ix_users_state_role ON users(is_active, role);
CREATE TABLE IF NOT EXISTS audit_outbox (
 id uuid PRIMARY KEY, payload jsonb NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), published_at timestamptz
);
CREATE INDEX IF NOT EXISTS ix_outbox_pending ON audit_outbox(created_at) WHERE published_at IS NULL;
CREATE TABLE IF NOT EXISTS revoked_tokens (id uuid PRIMARY KEY, expires_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS ix_revoked_expiry ON revoked_tokens(expires_at);
CREATE TABLE IF NOT EXISTS external_identities (
 provider varchar(64) NOT NULL, subject varchar(255) NOT NULL,
 user_id uuid NOT NULL REFERENCES users(id), PRIMARY KEY(provider, subject)
);`;

@Injectable()
export class Database implements OnModuleInit, OnModuleDestroy {
  private initialization?: Promise<void>;
  readonly pool = new Pool({ connectionString: required('AUTH_DATABASE_URL'), max: 10,
    connectionTimeoutMillis: 5000, statement_timeout: 15000 });
  async onModuleInit(): Promise<void> { await this.initialize(); }
  initialize(): Promise<void> {
    this.initialization ??= this.transaction(async client => {
      await client.query('SELECT pg_advisory_xact_lock(31001)');
      await client.query(schema);
    });
    return this.initialization;
  }
  async onModuleDestroy(): Promise<void> { await this.pool.end(); }
  async transaction<T>(run: (client: PoolClient) => Promise<T>): Promise<T> {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      const result = await run(client);
      await client.query('COMMIT');
      return result;
    } catch (error) {
      await client.query('ROLLBACK');
      throw error;
    } finally { client.release(); }
  }
}
