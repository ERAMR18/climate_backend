const sql = require('mssql');
const { MongoClient } = require('mongodb');
const { createHash } = require('node:crypto');

async function main() {
  if (!process.env.SOURCE_SQLSERVER_CONFIG || !process.env.AUDIT_MONGO_URL) throw new Error('Missing migration settings');
  const source = await new sql.ConnectionPool(JSON.parse(process.env.SOURCE_SQLSERVER_CONFIG)).connect();
  const mongo = new MongoClient(process.env.AUDIT_MONGO_URL, { serverSelectionTimeoutMS: 5000 });
  const transaction = new sql.Transaction(source); let started = false;
  try {
    await mongo.connect();
    const collection = mongo.db(process.env.AUDIT_MONGO_DATABASE || 'audit').collection('records');
    await collection.createIndex({ eventId: 1 }, { unique: true });
    await transaction.begin(sql.ISOLATION_LEVEL.SERIALIZABLE); started = true;
    const count = Number((await new sql.Request(transaction).query('SELECT COUNT_BIG(*) AS count FROM dbo.AuditLogs')).recordset[0].count);
    const fingerprint = createHash('sha256'); const ids = new Set();
    for (let offset = 0; offset < count; offset += 250) {
      const batch = await new sql.Request(transaction).input('offset', sql.Int, offset)
        .query('SELECT * FROM dbo.AuditLogs ORDER BY Id OFFSET @offset ROWS FETCH NEXT 250 ROWS ONLY');
      for (const row of batch.recordset) {
        const id = row.Id.toLowerCase(); ids.add(id);
        const document = { _id: id, eventId: id, userId: row.UserId.toLowerCase(), userName: row.UserName, action: row.Action,
          resource: row.Resource, resourceId: row.ResourceId, description: row.Description, ipAddress: row.IpAddress,
          timestamp: row.Timestamp, correlationId: row.CorrelationId ?? null, schemaVersion: 1 };
        await collection.updateOne({ _id: id }, { $setOnInsert: document }, { upsert: true, writeConcern: { w: 1, j: true } });
        const actual = await collection.findOne({ _id: id });
        for (const key of Object.keys(document)) if (JSON.stringify(document[key]) !== JSON.stringify(actual[key])) throw new Error('Audit identity collision');
        fingerprint.update(JSON.stringify(document));
      }
    }
    if (await collection.countDocuments() !== count) throw new Error('Audit count mismatch');
    for await (const row of collection.find({}, { projection: { _id: 1 } })) if (!ids.has(row._id)) throw new Error('Unexpected destination identity');
    console.log(JSON.stringify({ auditRecords: count, verified: true, fingerprint: fingerprint.digest('hex') }));
  } finally {
    if (started) await transaction.rollback(); await source.close(); await mongo.close();
  }
}
main().catch(() => { console.error('Audit migration not verified. Source is unchanged; destination writes already imported remain for idempotent resume. Inspect counts and collisions before cutover.'); process.exitCode = 1; });
