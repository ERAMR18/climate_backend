const { test } = require('node:test');
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { spawn, spawnSync } = require('node:child_process');
const { once } = require('node:events');
const path = require('node:path');
const { Pool } = require('pg');
const sql = require('mssql');
const amqp = require('amqplib');
const { Tokens } = require('../dist/tokens');
const { MongoClient } = require('../../../../tools/Phase3Migration/node_modules/mongodb');
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

test('SQL audit migration and Python consumer: real MongoDB, deduplication, DLQ, dependency outages and durable recovery', { timeout: 180000 }, async () => {
  const keepAlive = setInterval(() => {}, 1000); const backend = process.env.PHASE3_TEST_BACKEND;
  const pg = new Pool({ connectionString: process.env.AUTH_DATABASE_URL });
  const mongo = new MongoClient(process.env.AUDIT_MONGO_URL, { serverSelectionTimeoutMS: 2000 });
  const children = []; let rabbit;
  async function until(check, message, limit = 45000) {
    const end = Date.now() + limit;
    while (Date.now() < end) { try { if (await check()) return; } catch {} await wait(500); }
    assert.fail(message);
  }
  function docker(...args) { const result = spawnSync('docker', args, { encoding: 'utf8' }); assert.equal(result.status, 0, result.stderr); return result; }
  function compose(...args) { return docker('compose','-f', process.env.PHASE3_TEST_COMPOSE,'--profile','audit',...args); }
  function start(command,args,cwd,env) { const child = spawn(command,args,{cwd,env:{...process.env,...env},stdio:['ignore','pipe','pipe']});
    child.stdout.on('data',()=>{});child.stderr.on('data',()=>{});children.push(child);return child; }
  const config = JSON.parse(process.env.SOURCE_SQLSERVER_CONFIG);
  let source;
  try {
    const master = await new sql.ConnectionPool({...config,database:'master'}).connect();
    await master.request().query('CREATE DATABASE Phase3AuditMigrationTest'); await master.close();
    source = await new sql.ConnectionPool(config).connect();
    const legacyDir = path.join(backend,'src/Services/AuditService/Climate.Audit.Api');
    const legacy = start('dotnet',[path.join(legacyDir,'bin/Debug/net10.0/Climate.Audit.Api.dll')],legacyDir,{
      ASPNETCORE_URLS:'http://127.0.0.1:15116',ConnectionStrings__AuditDb:`Server=127.0.0.1,15433;Database=Phase3AuditMigrationTest;User Id=sa;Password=${config.password};TrustServerCertificate=True;Encrypt=True`,
      Jwt__SigningKey:process.env.JWT_SIGNING_KEY,InternalApi__ApiKey:process.env.INTERNAL_API_KEY,
      RABBITMQ_HOST:'127.0.0.1',RABBITMQ_PORT:'15672',RABBITMQ_USER:'test',RABBITMQ_PASSWORD:process.env.PHASE3_TEST_RABBIT_PASSWORD});
    await until(()=>fetch('http://127.0.0.1:15116/health').then(r=>r.ok).catch(()=>false),'Legacy audit fixture failed');
    legacy.kill('SIGTERM');await once(legacy,'exit');
    const historicalId = randomUUID(), actorId = randomUUID();
    await source.request().input('id',sql.UniqueIdentifier,historicalId).input('user',sql.UniqueIdentifier,actorId)
      .query("INSERT dbo.AuditLogs(Id,UserId,UserName,Action,Resource,ResourceId,Description,IpAddress,Timestamp,CorrelationId) VALUES(@id,@user,'legacy','Update','Sensor','historical','Historical audit',NULL,'2026-01-01T00:00:00Z',NULL)");
    for(let run=0;run<2;run++) { const result=spawnSync(process.execPath,[path.join(backend,'tools/Phase3Migration/audit.cjs')],{encoding:'utf8'});
      assert.equal(result.status,0,result.stderr);assert.equal(JSON.parse(result.stdout).auditRecords,1); }
    await mongo.connect();const records=mongo.db('audit').collection('records');
    assert.equal((await records.findOne({_id:historicalId})).correlationId,null);
    const runtimeEnv={...process.env,MONGO_URL:`mongodb://audit:${process.env.PHASE3_TEST_POSTGRES_PASSWORD}@mongo:27017/?authSource=admin`,
      RABBITMQ_URL:`amqp://test:${process.env.PHASE3_TEST_RABBIT_PASSWORD}@rabbitmq:5672/`};
    const run=spawnSync('docker',['run','-d','--name','climate-phase3-audit-runtime','--network','climate-phase3-auth-validation_default','-p','127.0.0.1:15106:8080',
      '-e','MONGO_URL','-e','MONGO_DATABASE=audit','-e','RABBITMQ_URL','-e','JWT_SIGNING_KEY','climate/audit:phase3'],{env:runtimeEnv,encoding:'utf8'});
    assert.equal(run.status,0,run.stderr);
    await until(()=>fetch('http://127.0.0.1:15106/health/ready').then(r=>r.ok).catch(()=>false),'Python Audit readiness failed');
    const tokens=new Tokens();const actor={id:actorId,username:'operator',email:'test@example.com',role:'Administrator'};
    const admin=tokens.issue(actor).accessToken,viewer=tokens.issue({...actor,role:'Viewer'}).accessToken;
    const endpoint='http://127.0.0.1:15106/api/v1/audit';
    assert.equal((await fetch(endpoint)).status,401);assert.equal((await fetch(endpoint,{headers:{Authorization:`Bearer ${viewer}`}})).status,403);
    assert.equal((await fetch(endpoint+'/'+historicalId,{headers:{Authorization:`Bearer ${admin}`}})).status,200);
    rabbit=await amqp.connect(process.env.RABBITMQ_URL);const channel=await rabbit.createConfirmChannel();
    await until(async()=> (await channel.checkQueue('climate.audit.events')).consumerCount>=1,'Python consumer not attached');
    const event={EventId:randomUUID(),OccurredAt:new Date().toISOString(),CorrelationId:'python-audit-test',UserId:actorId,UserName:'operator',Action:'Update',Resource:'Sensor',ResourceId:randomUUID(),Description:'Updated sensor',IpAddress:null};
    const publish=payload=>new Promise((resolve,reject)=>channel.publish('climate.audit','audit.created',Buffer.from(JSON.stringify(payload)),{persistent:true,contentType:'application/json'},error=>error?reject(error):resolve()));
    await publish(event);await publish(event);
    await until(async()=>await records.countDocuments({_id:event.EventId})===1,'Audit persistence/deduplication failed');
    await publish({bad:'event'});await until(async()=> (await channel.checkQueue('climate.audit.events.dlq')).messageCount===1,'Malformed event did not reach DLQ');
    docker('stop','climate-phase3-audit-runtime');const pending={...event,EventId:randomUUID()};await publish(pending);assert.equal(await records.countDocuments({_id:pending.EventId}),0);
    docker('start','climate-phase3-audit-runtime');await until(async()=>await records.countDocuments({_id:pending.EventId})===1,'Consumer restart did not recover pending event');
    const authDir=path.join(backend,'src/Services/AuthService');
    start(process.execPath,['dist/main.js'],authDir,{PORT:'15101',ADMIN_SEED_PASSWORD:'Fixture-Only-Aa12345',ADMIN_SEED_USERNAME:'admin',ADMIN_SEED_EMAIL:'admin@example.com'});
    await until(()=>fetch('http://127.0.0.1:15101/health/ready').then(r=>r.ok).catch(()=>false),'Auth fixture failed');
    const login=await fetch('http://127.0.0.1:15101/api/v1/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({login:'admin',password:'Fixture-Only-Aa12345'})});assert.equal(login.status,200);
    const session=await login.json();
    const create=name=>fetch('http://127.0.0.1:15101/api/v1/users',{method:'POST',headers:{'Content-Type':'application/json',Authorization:`Bearer ${session.accessToken}`},body:JSON.stringify({username:name,email:name+'@example.com',password:'Fixture-Only-Aa12345',role:'Viewer'})});
    compose('stop','mongo');assert.equal((await create('mongo_outage')).status,201,'Business operations must survive MongoDB outage');
    const duringMongo={...event,EventId:randomUUID()};await publish(duringMongo);await wait(1500);compose('start','mongo');
    await until(async()=>await records.countDocuments({_id:duringMongo.EventId})===1,'MongoDB recovery lost pending event');
    await channel.close();await rabbit.close();rabbit=undefined;
    compose('stop','rabbitmq');assert.equal((await create('rabbit_outage')).status,201);
    assert.ok((await pg.query('SELECT count(*)::int AS count FROM audit_outbox WHERE published_at IS NULL')).rows[0].count>0);
    compose('start','rabbitmq');await until(async()=> (await pg.query('SELECT count(*)::int AS count FROM audit_outbox WHERE published_at IS NULL')).rows[0].count===0,'Outbox did not recover after broker outage');
    await until(async()=>await records.countDocuments({action:'Create',resource:'User'})>=3,'NestJS events were not persisted by Python');
    console.log('Verified SQL→Mongo repeat migration, legacy audit availability, permissions, duplicate EventId, DLQ, consumer restart, Mongo/broker outages and NestJS Outbox recovery.');
  } finally {
    if(rabbit)await rabbit.close().catch(()=>{});
    for(const child of children.reverse())if(child.exitCode===null&&child.signalCode===null){child.kill('SIGTERM');await once(child,'exit');}
    if(source)await source.close();await pg.end();await mongo.close();clearInterval(keepAlive);
  }
});
