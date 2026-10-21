const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { spawnSync } = require('node:child_process');
const path = require('node:path');

module.exports = async function validateReports({request,until,token,Tokens,backend,sensor,community}) {
  await until(()=>fetch('http://127.0.0.1:15108/health/ready').then(r=>r.ok).catch(()=>false),'Report service did not start');
  const raw=(route,credential=token,method='GET',body)=>fetch('http://127.0.0.1:15180'+route,{
    method,headers:{Authorization:`Bearer ${credential}`,'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined});
  const operator=new Tokens().issue({id:'22222222-2222-4222-8222-222222222222',username:'operator',email:'operator@example.com',role:'Operator'}).accessToken;
  const viewer=new Tokens().issue({id:'33333333-3333-4333-8333-333333333333',username:'viewer',email:'viewer@example.com',role:'Viewer'}).accessToken;
  const jwt=require('jsonwebtoken');const claims=jwt.decode(token);
  for(const [change,expected] of [[{iss:'untrusted'},401],[{aud:'wrong-client'},401],[{exp:Math.floor(Date.now()/1000)-120},401],[{permissions:[]},403]]) {
    const credential=jwt.sign({...claims,...change},process.env.JWT_SIGNING_KEY,{algorithm:'HS256'});
    // Direct service checks ensure security is enforced beyond the Gateway.
    const response=await fetch('http://127.0.0.1:15108/api/v1/reports/types',{headers:{Authorization:`Bearer ${credential}`}});
    assert.equal(response.status,expected);
  }
  assert.equal((await fetch('http://127.0.0.1:15108/api/v1/reports/types')).status,401);
  assert.equal((await fetch('http://127.0.0.1:15108/api/v1/reports/types',{headers:{Authorization:'Bearer '+jwt.sign(claims,'untrusted-signature-key-at-least-32-characters',{algorithm:'HS256'})}})).status,401);
  const types=await request('/api/reports/types'); assert.equal(types.length,8);
  const filters={from:new Date(Date.now()-3600000).toISOString(),to:new Date(Date.now()+60000).toISOString(),timezone:'America/Guatemala'};
  async function completed(job) {
    let result;
    await until(async()=>{result=await request('/api/reports/'+job.id);return ['COMPLETED','FAILED'].includes(result.status);},'PDF generation timed out',210000);
    assert.equal(result.status,'COMPLETED',JSON.stringify(result));return result;
  }
  for (const type of types) {
    const job=await request('/api/reports/generate','POST',{type:type.id,filters});
    const result=await completed(job); const response=await raw(`/api/reports/${job.id}/download`);
    assert.equal(response.status,200);assert.match(response.headers.get('content-type'),/application\/pdf/);
    assert.match(response.headers.get('content-disposition'),/attachment/);
    const bytes=Buffer.from(await response.arrayBuffer());assert.equal(bytes.subarray(0,5).toString(),'%PDF-');
    assert.equal(createHash('sha256').update(bytes).digest('hex'),result.sha256);
    assert.equal(bytes.length,result.size);assert.ok((await request('/api/reports/'+job.id)).downloadedAt);
    assert.equal((await raw(`/api/reports/${job.id}/download`,operator)).status,403);
    console.log(`PDF verified: ${type.id}, ${result.rowCount} rows, ${bytes.length} bytes`);
  }
  assert.equal((await raw('/api/reports/generate',viewer,'POST',{type:'COMMUNITIES'})).status,403);
  assert.equal((await raw('/api/reports/generate',token,'POST',{type:'READINGS'})).status,422);
  assert.equal((await raw('/api/reports/generate',token,'POST',{type:'COMMUNITIES',filters:{timezone:'../../etc/passwd'}})).status,422);
  assert.equal((await raw('/api/reports/not-a-uuid/download')).status,422);
  assert.equal((await (await raw('/api/reports',viewer)).json()).total,0);
  const response=await raw('/api/reports/generate',operator,'POST',{type:'COMMUNITIES'});assert.equal(response.status,202);
  await completed(await response.json());
  assert.equal((await (await raw('/api/reports',operator)).json()).total,1);
  const empty=await completed(await request('/api/reports/generate','POST',{type:'READINGS',filters:{...filters,sensorId:'99999999-9999-4999-8999-999999999999'}}));
  assert.equal(empty.rowCount,0);
  assert.equal((await request('/api/reports?pageSize=100')).total,10);
  assert.equal((await request('/api/reports/statistics')).total,10);
  for(const [extra,expected] of [
    [{sensorId:sensor.id,communityId:community.id,sensorType:'Temperature'},1],
    [{sensorType:'Humidity'},0],
    [{communityId:'88888888-8888-4888-8888-888888888888'},0],
    [{from:'2000-01-01T00:00:00Z',to:'2000-01-02T00:00:00Z'},0]]) {
    const filtered=await completed(await request('/api/reports/generate','POST',{type:'READINGS',filters:{...filters,...extra}}));
    assert.equal(filtered.rowCount,expected,'Sensor/community/type/date filters must reach domain queries');
  }
  const sql = require(path.join(backend,'tools/Phase3Migration/node_modules/mssql'));
  const pool=await new sql.ConnectionPool({server:'127.0.0.1',port:15433,user:'sa',password:process.env.PHASE3_TEST_SQL_PASSWORD,
    database:'Phase3SystemMonitoring',options:{trustServerCertificate:true,encrypt:true}}).connect();
  const totalRows=Number(process.env.PHASE3_TEST_REPORT_ROWS||261);
  assert.ok(Number.isInteger(totalRows)&&totalRows>=261&&totalRows<=50000);
  try {
    await pool.request().input('recordedAt',sql.DateTimeOffset,new Date(Date.parse(filters.from)+60000)).input('count',sql.Int,totalRows-1).input('sensor',sql.UniqueIdentifier,sensor.id).input('community',sql.UniqueIdentifier,community.id).query(
      `INSERT INTO SensorReadings (Id,SensorId,CommunityId,SensorType,Value,Unit,RecordedAt,SensorWasActive)
       SELECT TOP (@count) NEWID(),@sensor,@community,N'Temperature',20,N'°C',@recordedAt,1 FROM sys.all_objects a CROSS JOIN sys.all_objects b`);
  } finally {await pool.close();}
  const started=Date.now();
  const large=await completed(await request('/api/reports/generate','POST',{type:'READINGS',filters}));
  assert.equal(large.rowCount,totalRows,'Reading pagination must preserve every source row');
  const largePdf=await raw(`/api/reports/${large.id}/download`);assert.equal(largePdf.status,200);
  const largeBytes=Buffer.from(await largePdf.arrayBuffer());assert.equal(largeBytes.subarray(0,5).toString(),'%PDF-');
  assert.equal(createHash('sha256').update(largeBytes).digest('hex'),large.sha256);
  const measurements={rows:totalRows,sourcePages:Math.ceil(totalRows/250),elapsedMs:Date.now()-started,pdfBytes:largeBytes.length,completed:true};
  console.log('Large report measured: '+JSON.stringify(measurements));
  const fs=require('node:fs');const artifacts=path.join(backend,'docs/validation');fs.mkdirSync(artifacts,{recursive:true});
  fs.writeFileSync(path.join(artifacts,'phase3-large-report.json'),JSON.stringify(measurements,null,2)+'\n');
  if(totalRows>261){
    const overflow=await new sql.ConnectionPool({server:'127.0.0.1',port:15433,user:'sa',password:process.env.PHASE3_TEST_SQL_PASSWORD,database:'Phase3SystemMonitoring',options:{trustServerCertificate:true,encrypt:true}}).connect();
    try{await overflow.request().input('recordedAt',sql.DateTimeOffset,new Date(Date.parse(filters.from)+60000)).input('sensor',sql.UniqueIdentifier,sensor.id).input('community',sql.UniqueIdentifier,community.id).query(`INSERT INTO SensorReadings (Id,SensorId,CommunityId,SensorType,Value,Unit,RecordedAt,SensorWasActive) VALUES (NEWID(),@sensor,@community,N'Temperature',20,N'°C',@recordedAt,1)`);}finally{await overflow.close();}
    const excessive=await request('/api/reports/generate','POST',{type:'READINGS',filters});let result;
    await until(async()=>{result=await request('/api/reports/'+excessive.id);return result.status==='FAILED';},'Oversized report did not fail explicitly',45000);
    assert.equal(result.error,'REPORT_LIMIT_EXCEEDED');assert.equal((await raw(`/api/reports/${excessive.id}/download`)).status,409);
  }
  const {MongoClient}=require(path.join(backend,'tools/Phase3Migration/node_modules/mongodb'));
  const mongo=new MongoClient(`mongodb://audit:${process.env.PHASE3_TEST_POSTGRES_PASSWORD}@127.0.0.1:15417/?authSource=admin`);
  try {
    await mongo.connect();const db=mongo.db('reports');
    const burst=await Promise.all(Array.from({length:20},()=>raw('/api/reports/generate',token,'POST',{type:'COMMUNITIES'})));
    const accepted=burst.filter(response=>response.status===202);
    assert.equal(accepted.length,3,'Concurrent submissions must reserve exactly three persistent slots');
    assert.ok(burst.every(response=>[202,429].includes(response.status)));
    for(const response of accepted) await completed(await response.json());
    assert.equal(await db.collection('reports').countDocuments({activeSlot:{$exists:true}}),0,'Completed jobs must release their quota');
    await db.collection('reports').updateOne({_id:large.id},{$set:{expiresAt:new Date(Date.now()-60000)}});
    assert.equal((await raw(`/api/reports/${large.id}/download`)).status,410);
    const rabbit=spawnSync('docker',['compose','-f',process.env.PHASE3_TEST_COMPOSE,'stop','rabbitmq'],{encoding:'utf8'});assert.equal(rabbit.status,0,rabbit.stderr);
    const offline=await completed(await request('/api/reports/generate','POST',{type:'COMMUNITIES'}));
    assert.equal((await db.collection('reports').findOne({_id:offline.id})).auditPublishedAt,null);
    const restart=spawnSync('docker',['compose','-f',process.env.PHASE3_TEST_COMPOSE,'start','rabbitmq'],{encoding:'utf8'});assert.equal(restart.status,0,restart.stderr);
    await until(async()=>Boolean((await db.collection('reports').findOne({_id:offline.id})).auditPublishedAt),'Report audit outbox failed to recover',45000);
    const stop=spawnSync('docker',['pause','climate-phase3-auth-validation-postgres-1'],{encoding:'utf8'});assert.equal(stop.status,0,stop.stderr);
    try {
      const failed=await request('/api/reports/generate','POST',{type:'COMMUNITIES'});let result;
      await until(async()=>{result=await request('/api/reports/'+failed.id);return result.status==='FAILED';},'Dependency failure did not become a failed job',60000);
      assert.equal(result.error,'DEPENDENCY_UNAVAILABLE');assert.equal((await raw(`/api/reports/${failed.id}/download`)).status,409);
    } finally {spawnSync('docker',['unpause','climate-phase3-auth-validation-postgres-1']);}
    await db.collection('reports').updateOne({_id:offline.id},{$set:{status:'PROCESSING',leaseOwner:'interrupted-worker',leaseUntil:new Date(Date.now()-60000),attempts:1}});
    const recovered=spawnSync('docker',['restart','climate-phase3-system-reports'],{encoding:'utf8'});assert.equal(recovered.status,0,recovered.stderr);
    await until(()=>fetch('http://127.0.0.1:15108/health/ready').then(r=>r.ok).catch(()=>false),'Report service failed restart');
    await completed(offline);assert.equal((await db.collection('reports').findOne({_id:offline.id})).attempts,2);
  } finally {await mongo.close();}
  const internal=await fetch('http://127.0.0.1:15101/api/v1/internal/auth/service-token',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({service:'reports'})});
  assert.equal(internal.status,401);
  fs.writeFileSync(path.join(artifacts,'phase3-report-integration.json'),JSON.stringify({passed:true,
    timestamp:new Date().toISOString(),reportTypes:8,filterCases:4,rows:totalRows,
    concurrentRequests:20,concurrentAccepted:3,concurrentRejected:17,
    overflowTested:totalRows>261,leaseRecoveredAfterRestart:true,jwtValidatedInsideService:true},null,2)+'\n');
  console.log(`Eight real PDFs, pagination (${totalRows} readings), digest/size, history, roles, empty results, expiry, broker recovery and dependency failure verified.`);
};
