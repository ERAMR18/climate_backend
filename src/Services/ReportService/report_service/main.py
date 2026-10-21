import asyncio
from contextlib import asynccontextmanager
from datetime import timedelta
import os
from pathlib import Path
from uuid import UUID, uuid4

from fastapi import Depends, FastAPI, HTTPException, Query, Request
from fastapi.responses import FileResponse
from pymongo import AsyncMongoClient
from pymongo.errors import DuplicateKeyError
from pymongo.write_concern import WriteConcern

from climate_common.security import Identity, authenticated
from climate_common.outbox import audit_event, dispatch_embedded
from climate_common.web import configure
from .models import Filters, GenerateRequest, ReportType, TITLES, metadata, utcnow
from .worker import Worker


@asynccontextmanager
async def lifespan(app: FastAPI):
    client = AsyncMongoClient(os.environ['MONGO_URL'], serverSelectionTimeoutMS=3000, tz_aware=True)
    db = client.get_database(os.getenv('MONGO_DATABASE','reports'), write_concern=WriteConcern(w=1,j=True))
    directory = Path(os.getenv('REPORT_STORAGE_DIRECTORY','/data/reports')).resolve()
    directory.mkdir(parents=True, exist_ok=True)
    await db.reports.create_index([('userId',1),('generatedAt',-1)])
    await db.reports.create_index([('status',1),('leaseUntil',1),('generatedAt',1)])
    await db.reports.create_index('expiresAt')
    await db.reports.create_index([('userId',1),('activeSlot',1)],unique=True,
        partialFilterExpression={'activeSlot':{'$type':'int'}},name='one_active_job_per_user_slot')
    await db.downloads.create_index([('reportId',1),('downloadedAt',-1)])
    app.state.client, app.state.db, app.state.directory = client, db, directory
    tasks=[asyncio.create_task(Worker(db,directory).run()),asyncio.create_task(dispatch_embedded([db.reports,db.downloads]))]
    try:
        yield
    finally:
        for task in tasks: task.cancel()
        await asyncio.gather(*tasks,return_exceptions=True)
        await client.close()


app=FastAPI(title='Climate Report Service',version='3.0.0',lifespan=lifespan)
configure(app)


def authorize(identity: Identity, permission: str):
    if permission not in identity.permissions:
        raise HTTPException(403,'Explicit report permission required')


def scope(identity: Identity) -> dict:
    authorize(identity,'reports:read')
    return {} if identity.role=='Administrator' else {'userId':identity.id}


@app.get('/health/live')
async def live(): return {'status':'Healthy'}


@app.get('/health')
@app.get('/health/ready')
async def ready():
    await app.state.client.admin.command('ping')
    return {'status':'Healthy'}


@app.get('/api/v1/reports/types')
async def types(identity: Identity=Depends(authenticated)):
    authorize(identity,'reports:read')
    return [{'id':item.value,'name':TITLES[item]} for item in ReportType]


@app.post('/api/v1/reports/generate',status_code=202)
async def generate(body: GenerateRequest, request: Request, identity: Identity=Depends(authenticated)):
    authorize(identity,'reports:generate')
    if identity.role not in {'Administrator','Operator'}:
        raise HTTPException(403,'Role cannot generate reports')
    pending=await app.state.db.reports.count_documents({'userId':identity.id,'status':{'$in':['PENDING','PROCESSING']}})
    if pending>=int(os.getenv('REPORT_MAX_PENDING_PER_USER','3')):
        raise HTTPException(429,'Report generation limit reached; wait for pending jobs')
    filters=body.filters
    if body.type==ReportType.EXECUTIVE:
        end=filters.to or utcnow()
        try:
            filters=Filters.model_validate({**filters.model_dump(by_alias=True),'from':filters.from_ or end-timedelta(days=1),'to':end})
        except ValueError as error:
            raise HTTPException(422,'Invalid executive report period') from error
    id_=str(uuid4());now=utcnow()
    document={'_id':id_,'id':id_,'type':body.type.value,'userId':identity.id,'userName':identity.name,'generatedAt':now,
        'parameters':filters.model_dump(mode='json',by_alias=True,exclude_none=True),'status':'PENDING','progress':0,'attempts':0,
        'fileName':body.type.value.lower()+'-'+id_+'.pdf','fileReference':None,'downloadedAt':None,'error':None,
        'expiresAt':now+timedelta(days=int(os.getenv('REPORT_RETENTION_DAYS','30'))),'correlationId':request.state.correlation_id,
        'auditEvent':audit_event(identity,'GenerateReport',id_,request.state.correlation_id),'auditPublishedAt':None}
    # A unique persistent slot prevents concurrent requests/replicas bypassing the limit.
    for slot in range(int(os.getenv('REPORT_MAX_PENDING_PER_USER','3'))):
        try:
            await app.state.db.reports.insert_one({**document,'activeSlot':slot})
            return metadata(document)
        except DuplicateKeyError:
            continue
    raise HTTPException(429,'Report generation limit reached; wait for pending jobs')


@app.get('/api/v1/reports')
async def history(page: int=Query(default=1,ge=1,le=1000000),pageSize: int=Query(default=25,ge=1,le=100),identity: Identity=Depends(authenticated)):
    query=scope(identity)
    total=await app.state.db.reports.count_documents(query)
    cursor=app.state.db.reports.find(query).sort([('generatedAt',-1),('_id',1)]).skip((page-1)*pageSize).limit(pageSize)
    return {'items':[metadata(item) async for item in cursor],'total':total,'page':page,'pageSize':pageSize}


@app.get('/api/v1/reports/statistics')
async def statistics(identity: Identity=Depends(authenticated)):
    counts={status:0 for status in ('PENDING','PROCESSING','COMPLETED','FAILED')}
    async for item in await app.state.db.reports.aggregate([{'$match':scope(identity)},{'$group':{'_id':'$status','count':{'$sum':1}}}]):
        if item['_id'] in counts: counts[item['_id']]=item['count']
    return {'total':sum(counts.values()),'byStatus':counts}


async def find(id_: UUID,identity: Identity):
    document=await app.state.db.reports.find_one({'_id':str(id_),**scope(identity)})
    if document is None: raise HTTPException(404,'Report not found')
    return document


@app.get('/api/v1/reports/{id}')
async def detail(id: UUID,identity: Identity=Depends(authenticated)):
    return metadata(await find(id,identity))


@app.get('/api/v1/reports/{id}/download')
async def download(id: UUID,request: Request,identity: Identity=Depends(authenticated)):
    authorize(identity,'reports:download')
    if identity.role!='Administrator': raise HTTPException(403,'Only administrators can download PDFs')
    document=await find(id,identity)
    if document['status']!='COMPLETED': raise HTTPException(409,'Report is not completed')
    if document['expiresAt']<utcnow(): raise HTTPException(410,'Report file expired')
    file=(app.state.directory/(str(id)+'.pdf')).resolve()
    if file.parent!=app.state.directory or not file.is_file(): raise HTTPException(404,'Report file unavailable')
    now=utcnow()
    await app.state.db.downloads.insert_one({'_id':str(uuid4()),'reportId':str(id),'userId':identity.id,'downloadedAt':now,
        'auditEvent':audit_event(identity,'DownloadReport',str(id),request.state.correlation_id),'auditPublishedAt':None})
    await app.state.db.reports.update_one({'_id':str(id)},{'$set':{'downloadedAt':now}})
    return FileResponse(file,media_type='application/pdf',filename=document['fileName'],headers={'Cache-Control':'private, no-store'})
