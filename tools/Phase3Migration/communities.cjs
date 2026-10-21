// Offline maintenance tool only; domain services never connect to another service's database.
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const { createHash } = require('node:crypto');
const sql = require('mssql');
const { Pool } = require('pg');

async function main() {
  if (!process.env.SOURCE_SQLSERVER_CONFIG || !process.env.COMMUNITY_DATABASE_URL) throw new Error('Missing migration configuration');
  const source = await new sql.ConnectionPool(JSON.parse(process.env.SOURCE_SQLSERVER_CONFIG)).connect();
  const target = new Pool({ connectionString: process.env.COMMUNITY_DATABASE_URL, connectionTimeoutMillis: 5000 });
  const sourceTx = new sql.Transaction(source);
  const client = await target.connect();
  let sourceStarted = false;
  try {
    await sourceTx.begin(sql.ISOLATION_LEVEL.SERIALIZABLE); sourceStarted = true;
    await client.query('BEGIN');
    await client.query('SELECT pg_advisory_xact_lock(31003)');
    const schema = readFileSync(resolve(__dirname, '../../src/Services/CommunityService/Climate.Communities.Api/Persistence/001_initial.sql'), 'utf8');
    await client.query(schema);
    const count = Number((await new sql.Request(sourceTx).query('SELECT COUNT_BIG(*) AS count FROM dbo.Communities')).recordset[0].count);
    const ids = new Set(); const fingerprint = createHash('sha256');
    for (let offset = 0; offset < count; offset += 250) {
      const batch = await new sql.Request(sourceTx).input('offset', sql.Int, offset)
        .query('SELECT * FROM dbo.Communities ORDER BY Id OFFSET @offset ROWS FETCH NEXT 250 ROWS ONLY');
      for (const row of batch.recordset) {
        const values = [row.Id.toLowerCase(), row.Name, row.Description, Number(row.Latitude), Number(row.Longitude), row.IsActive,
          row.CreatedAt, row.Municipality, row.Department, row.Country];
        ids.add(values[0]);
        await client.query(`INSERT INTO "Communities"("Id","Name","Description","Latitude","Longitude","IsActive","CreatedAt","Municipality","Department","Country")
          VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) ON CONFLICT("Id") DO NOTHING`, values);
        const stored = (await client.query('SELECT * FROM "Communities" WHERE "Id"=$1', [values[0]])).rows[0];
        const actual = [stored.Id, stored.Name, stored.Description, Number(stored.Latitude), Number(stored.Longitude), stored.IsActive,
          stored.CreatedAt, stored.Municipality, stored.Department, stored.Country];
        if (JSON.stringify(actual) !== JSON.stringify(values)) throw new Error('Destination differs from source; refusing overwrite');
        fingerprint.update(JSON.stringify(values));
      }
    }
    const destinationIds = (await client.query('SELECT "Id" FROM "Communities"')).rows;
    if (destinationIds.length !== count || destinationIds.some(row => !ids.has(row.Id))) throw new Error('Community identity/count mismatch');
    const references = (await new sql.Request(sourceTx).query('SELECT DISTINCT CommunityId FROM dbo.Sensors')).recordset;
    if (references.some(row => !ids.has(row.CommunityId.toLowerCase()))) throw new Error('Invalid sensor community reference');
    await client.query('COMMIT');
    console.log(JSON.stringify({ communities: count, validSensorCommunityReferences: references.length, verified: true, fingerprint: fingerprint.digest('hex') }));
  } catch (error) {
    await client.query('ROLLBACK'); throw error;
  } finally {
    client.release(); if (sourceStarted) await sourceTx.rollback(); await source.close(); await target.end();
  }
}
main().catch(() => { console.error('Community migration failed; check schema, connectivity, identity collisions and references. No source data modified.'); process.exitCode = 1; });
