const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const { createHash } = require('node:crypto');
const sql = require('mssql');
const { Pool } = require('pg');
const sensorTypes = ['Temperature','Humidity','WindSpeed','Rainfall','WaterLevel','RiverLevel','ReservoirLevel','Smoke','Other'];
const risks = ['Flood','Drought','Storm','Frost','ForestFire'];
const levels = ['Green','Yellow','Orange','Red'];
const statuses = ['Active','Attended','Closed'];
const ruleFields = ['Id','Name','SensorType','MinimumValue','MaximumValue','AlertLevel','RiskType','Message','IsActive','CreatedAt','UpdatedAt'];
const alertFields = ['Id','SensorId','CommunityId','AlertType','Level','Title','Description','SensorValue','ThresholdValue','GeneratedAt','IsActive','ResolvedAt',
  'Status','AttendedByUserId','AttendedAt','ClosedByUserId','ClosedAt','RuleId','RuleName','MinimumValueSnapshot','MaximumValueSnapshot'];
function convert(row, fields) {
  const payload = {};
  for (const field of fields) {
    let value = row[field] ?? null;
    if (field === 'SensorType' && typeof value === 'number') value = sensorTypes[value];
    if (['Id','SensorId','CommunityId','RuleId','AttendedByUserId','ClosedByUserId'].includes(field) && value) value=value.toLowerCase();
    if (value instanceof Date) value=value.toISOString();
    payload[field[0].toLowerCase()+field.slice(1)] = value;
  }
  if (fields === ruleFields && (!sensorTypes.includes(payload.sensorType) || !risks.includes(payload.riskType) || !levels.includes(payload.alertLevel))) throw new Error('Unsupported rule enums');
  if (fields === alertFields && (!risks.includes(payload.alertType) || !levels.includes(payload.level) || !statuses.includes(payload.status))) throw new Error('Unsupported alert enums');
  return payload;
}
async function main() {
  if (!process.env.SOURCE_SQLSERVER_CONFIG || !process.env.ALERT_POSTGRES_URL) throw new Error('Missing migration configuration');
  const source = await new sql.ConnectionPool(JSON.parse(process.env.SOURCE_SQLSERVER_CONFIG)).connect();
  const target = new Pool({ connectionString: process.env.ALERT_POSTGRES_URL, connectionTimeoutMillis: 5000 });
  const tx = new sql.Transaction(source);const client=await target.connect();let started=false;
  try {
    await tx.begin(sql.ISOLATION_LEVEL.SERIALIZABLE);started=true;
    await client.query('BEGIN');await client.query('SELECT pg_advisory_xact_lock(31004)');
    await client.query(readFileSync(resolve(__dirname,'../../src/Services/AlertServiceJava/src/main/resources/schema.sql'),'utf8'));
    const result={};const fingerprint=createHash('sha256');
    for (const [sourceTable,destination,fields] of [['AlertRules','alert_rules',ruleFields],['Alerts','alerts',alertFields]]) {
      const count=Number((await new sql.Request(tx).query(`SELECT COUNT_BIG(*) AS count FROM dbo.${sourceTable}`)).recordset[0].count);
      const ids=new Set();
      for(let offset=0;offset<count;offset+=250) {
        const batch=await new sql.Request(tx).input('offset',sql.Int,offset).query(`SELECT * FROM dbo.${sourceTable} ORDER BY Id OFFSET @offset ROWS FETCH NEXT 250 ROWS ONLY`);
        for(const row of batch.recordset) {
          const payload=convert(row,fields);ids.add(payload.id);
          await client.query(`INSERT INTO ${destination}(id,payload) VALUES($1,$2) ON CONFLICT(id) DO NOTHING`,[payload.id,payload]);
          if(!(await client.query(`SELECT 1 FROM ${destination} WHERE id=$1 AND payload=$2::jsonb`,[payload.id,payload])).rowCount)throw new Error('Destination data collision');
          fingerprint.update(JSON.stringify(payload));
        }
      }
      const existing=(await client.query(`SELECT id FROM ${destination}`)).rows;
      if(existing.length!==count||existing.some(row=>!ids.has(row.id)))throw new Error('Count or ID mismatch');
      result[destination]=count;
    }
    const invalid=await client.query("SELECT 1 FROM alerts a WHERE a.payload->>'ruleId' IS NOT NULL AND NOT EXISTS(SELECT 1 FROM alert_rules r WHERE r.id=(a.payload->>'ruleId')::uuid) LIMIT 1");
    if(invalid.rowCount)throw new Error('Alert rule reference is invalid');
    let auditEvents=0;
    const countPending=Number((await new sql.Request(tx).query('SELECT COUNT_BIG(*) AS count FROM dbo.AuditOutbox WHERE PublishedAt IS NULL')).recordset[0].count);
    for(let offset=0;offset<countPending;offset+=250){
      const batch=await new sql.Request(tx).input('offset',sql.Int,offset).query('SELECT * FROM dbo.AuditOutbox WHERE PublishedAt IS NULL ORDER BY Id OFFSET @offset ROWS FETCH NEXT 250 ROWS ONLY');
      for(const event of batch.recordset){
        const payload=JSON.parse(event.Payload);
        await client.query("INSERT INTO delivery_outbox(id,kind,payload,correlation_id,created_at) VALUES($1,'AUDIT',$2,$3,$4) ON CONFLICT(id) DO NOTHING",[event.Id,payload,payload.CorrelationId,event.CreatedAt]);
        if(!(await client.query("SELECT 1 FROM delivery_outbox WHERE id=$1 AND kind='AUDIT' AND payload=$2::jsonb",[event.Id,payload])).rowCount)throw new Error('Audit event collision');
        auditEvents++;
      }
    }
    await client.query('COMMIT');console.log(JSON.stringify({...result,pendingAuditEvents:auditEvents,verified:true,fingerprint:fingerprint.digest('hex')}));
  }catch(error){await client.query('ROLLBACK');throw error;}
  finally{client.release();if(started)await tx.rollback();await source.close();await target.end();}
}
main().catch(()=>{console.error('Alert migration failed; destination transaction rolled back. Check schemas, enum values, references and identity collisions.');process.exitCode=1;});
