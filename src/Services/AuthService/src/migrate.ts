import 'reflect-metadata';
import sql from 'mssql';
import { createHash } from 'node:crypto';
import { Database } from './database';
import { required } from './config';

/** Maintenance command. No DELETE/UPDATE is issued against the source database. */
async function migrate(): Promise<void> {
  const sourceConfig = JSON.parse(required('SOURCE_SQLSERVER_CONFIG')) as sql.config;
  const source = await new sql.ConnectionPool(sourceConfig).connect();
  const sourceTx = new sql.Transaction(source);
  const destination = new Database();
  let sourceStarted = false;
  try {
    await destination.initialize();
    await sourceTx.begin(sql.ISOLATION_LEVEL.SERIALIZABLE);
    sourceStarted = true;
    const summary = await destination.transaction(async client => {
      await client.query('SELECT pg_advisory_xact_lock(31002)');
      const sourceCount = Number((await new sql.Request(sourceTx).query('SELECT COUNT_BIG(*) AS count FROM dbo.Users')).recordset[0].count);
      const sourceIds = new Set<string>();
      const digest = createHash('sha256');
      for (let offset = 0; offset < sourceCount; offset += 250) {
        const batch = await new sql.Request(sourceTx).input('offset', sql.Int, offset).query(`SELECT * FROM dbo.Users ORDER BY Id OFFSET @offset ROWS FETCH NEXT 250 ROWS ONLY`);
        for (const user of batch.recordset) {
          const id = String(user.Id).toLowerCase();
          sourceIds.add(id);
          if (!['Administrator', 'Operator', 'Viewer'].includes(user.Role)) throw new Error('Unsupported source role; migration rolled back.');
          const values = [id, user.Username, user.NormalizedUsername, user.Email, user.NormalizedEmail, user.PasswordHash,
            user.Role, user.IsActive, user.CreatedAt, user.UpdatedAt, user.LastLoginAt ?? null];
          // Do not overwrite a destination identity that changed after an earlier import.
          await client.query(`INSERT INTO users(id,username,normalized_username,email,normalized_email,password_hash,role,is_active,
            created_at,updated_at,last_login_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11) ON CONFLICT(id) DO NOTHING`, values);
          const check = (await client.query('SELECT * FROM users WHERE id=$1', [id])).rows[0];
          const actual = [check.id, check.username, check.normalized_username, check.email, check.normalized_email,
            check.password_hash, check.role, check.is_active, check.created_at, check.updated_at, check.last_login_at];
          if (JSON.stringify(actual) !== JSON.stringify(values)) throw new Error('Destination differs from source; migration rolled back.');
          digest.update(JSON.stringify(values));
        }
      }
      const destinationIds = (await client.query<{ id: string }>('SELECT id FROM users')).rows;
      if (destinationIds.length !== sourceCount || destinationIds.some(row => !sourceIds.has(row.id))) throw new Error('Identity count or identifier mismatch; migration rolled back.');
      // Copy unconfirmed audit events with their original IDs. At-least-once replay is intentional.
      const pendingCount = Number((await new sql.Request(sourceTx).query('SELECT COUNT_BIG(*) AS count FROM dbo.AuditOutbox WHERE PublishedAt IS NULL')).recordset[0].count);
      for (let offset = 0; offset < pendingCount; offset += 250) {
        const pending = await new sql.Request(sourceTx).input('offset', sql.Int, offset).query(`SELECT Id,Payload,CreatedAt FROM dbo.AuditOutbox
          WHERE PublishedAt IS NULL ORDER BY Id OFFSET @offset ROWS FETCH NEXT 250 ROWS ONLY`);
        for (const event of pending.recordset) {
          const payload = JSON.parse(event.Payload);
          await client.query('INSERT INTO audit_outbox(id,payload,created_at) VALUES($1,$2,$3) ON CONFLICT(id) DO NOTHING', [event.Id, payload, event.CreatedAt]);
          const match = await client.query('SELECT 1 FROM audit_outbox WHERE id=$1 AND payload=$2::jsonb', [event.Id, payload]);
          if (!match.rowCount) throw new Error('Outbox identity collision; migration rolled back.');
        }
      }
      return { users: sourceCount, pendingAuditEvents: pendingCount, fingerprint: digest.digest('hex'), verified: true };
    });
    console.log(JSON.stringify(summary));
  } finally {
    if (sourceStarted) await sourceTx.rollback();
    await source.close();
    await destination.onModuleDestroy();
  }
}
migrate().catch(() => { console.error('Auth migration failed; destination transaction rolled back. Check connectivity, schema, collisions and source roles.'); process.exitCode = 1; });
