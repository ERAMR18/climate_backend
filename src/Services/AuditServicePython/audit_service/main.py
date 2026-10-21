import asyncio
from contextlib import asynccontextmanager
from datetime import datetime, timezone
import os
from uuid import UUID

from fastapi import Depends, FastAPI, HTTPException, Query
from pymongo import AsyncMongoClient
from pymongo.write_concern import WriteConcern

from climate_common.security import administrator
from climate_common.web import configure
from .consumer import consume
from .models import response


@asynccontextmanager
async def lifespan(app: FastAPI):
    client = AsyncMongoClient(os.environ['MONGO_URL'], serverSelectionTimeoutMS=3000, tz_aware=True)
    database = client.get_database(os.getenv('MONGO_DATABASE', 'audit'), write_concern=WriteConcern(w=1, j=True))
    app.state.collection = database.records
    app.state.client = client
    task = asyncio.create_task(consume(database.records))
    try:
        yield
    finally:
        task.cancel()
        await asyncio.gather(task, return_exceptions=True)
        await client.close()


app = FastAPI(title='Climate Audit Service', version='3.0.0', lifespan=lifespan)
configure(app)


@app.get('/health/live')
async def live():
    return {'status': 'Healthy'}


@app.get('/health')
@app.get('/health/ready')
async def ready():
    await app.state.client.admin.command('ping')
    return {'status': 'Healthy'}


@app.get('/api/v1/audit', dependencies=[Depends(administrator)])
async def list_audit(userId: UUID | None = None, action: str | None = Query(default=None, max_length=64),
                     resource: str | None = Query(default=None, max_length=100),
                     from_: datetime | None = Query(default=None, alias='from'), to: datetime | None = None,
                     page: int = Query(default=1, ge=1, le=1000000), pageSize: int = Query(default=250, ge=1, le=500)):
    if any(value is not None and value.tzinfo is None for value in (from_, to)) or (from_ and to and from_ > to):
        raise HTTPException(422, 'Invalid date range; timezone required')
    query: dict = {}
    for key, value in (('userId', str(userId) if userId else None), ('action', action), ('resource', resource)):
        if value is not None:
            query[key] = value
    if from_ or to:
        query['timestamp'] = {**({'$gte': from_.astimezone(timezone.utc)} if from_ else {}),
                              **({'$lte': to.astimezone(timezone.utc)} if to else {})}
    cursor = app.state.collection.find(query).sort([('timestamp', -1), ('_id', 1)]).skip((page - 1) * pageSize).limit(pageSize)
    return [response(document) async for document in cursor]


@app.get('/api/v1/audit/{id}', dependencies=[Depends(administrator)])
async def get_audit(id: UUID):
    document = await app.state.collection.find_one({'_id': str(id)})
    if document is None:
        raise HTTPException(404, 'Audit record not found')
    return response(document)
