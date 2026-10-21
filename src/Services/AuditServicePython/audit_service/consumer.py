import asyncio
import logging
import os

import aio_pika
from pydantic import ValidationError
from pymongo.errors import DuplicateKeyError, PyMongoError

from .models import AuditEvent

logger = logging.getLogger('climate.audit')
EXCHANGE = 'climate.audit'
QUEUE = 'climate.audit.events'
ROUTING_KEY = 'audit.created'


async def initialize(collection) -> None:
    await collection.create_index('eventId', unique=True)
    await collection.create_index([('timestamp', -1)])
    await collection.create_index([('userId', 1), ('timestamp', -1)])
    await collection.create_index([('action', 1), ('resource', 1), ('timestamp', -1)])


async def persist(collection, event: AuditEvent) -> bool:
    try:
        result = await collection.update_one({'_id': str(event.event_id)}, {'$setOnInsert': event.document()}, upsert=True)
        if not result.acknowledged:
            raise RuntimeError('MongoDB did not acknowledge audit persistence')
        return result.upserted_id is not None
    except DuplicateKeyError:
        # Another consumer persisted this same event before ACK/replay.
        return False


async def consume(collection) -> None:
    while True:
        connection = None
        try:
            await initialize(collection)
            connection = await aio_pika.connect_robust(os.environ['RABBITMQ_URL'], timeout=5)
            channel = await connection.channel(publisher_confirms=True)
            await channel.set_qos(prefetch_count=20)
            exchange = await channel.declare_exchange(EXCHANGE, 'direct', durable=True)
            dead = await channel.declare_exchange('climate.audit.dead', 'direct', durable=True)
            dlq = await channel.declare_queue(QUEUE + '.dlq', durable=True)
            await dlq.bind(dead, ROUTING_KEY)
            queue = await channel.declare_queue(QUEUE, durable=True, arguments={
                'x-dead-letter-exchange': 'climate.audit.dead', 'x-dead-letter-routing-key': ROUTING_KEY})
            await queue.bind(exchange, ROUTING_KEY)
            async with queue.iterator() as messages:
                async for message in messages:
                    try:
                        if len(message.body) > 65536:
                            raise ValueError('Oversized audit event')
                        event = AuditEvent.model_validate_json(message.body)
                    except (ValidationError, ValueError):
                        logger.warning('Invalid audit event rejected message_id=%s', message.message_id)
                        await message.reject(requeue=False)
                        continue
                    try:
                        await persist(collection, event)
                        # This ACK must follow the acknowledged durable MongoDB write.
                        await message.ack()
                    except PyMongoError:
                        logger.warning('Audit storage unavailable event_id=%s; requeueing', event.event_id)
                        await message.nack(requeue=True)
                        await asyncio.sleep(5)
        except asyncio.CancelledError:
            raise
        except Exception as error:
            logger.warning('Audit consumer reconnecting error_type=%s', type(error).__name__)
            await asyncio.sleep(5)
        finally:
            if connection is not None:
                await connection.close()
