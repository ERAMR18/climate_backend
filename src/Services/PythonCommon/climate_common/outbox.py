import asyncio
import json
import logging
import os
from uuid import uuid4
from datetime import datetime, timezone

import aio_pika

from .security import Identity


def audit_event(identity: Identity, action: str, resource_id: str, correlation: str) -> dict:
    return {'SchemaVersion': 1, 'EventId': str(uuid4()), 'OccurredAt': datetime.now(timezone.utc).isoformat(),
            'CorrelationId': correlation, 'UserId': identity.id, 'UserName': identity.name, 'Action': action,
            'Resource': 'Report', 'ResourceId': resource_id, 'Description': action + ' report.', 'IpAddress': None}


async def dispatch_embedded(collections) -> None:
    """Each event is atomically inserted with its owning business document before broker I/O."""
    logger = logging.getLogger('climate.outbox')
    while True:
        connection = None
        try:
            connection = await aio_pika.connect_robust(os.environ['RABBITMQ_URL'], timeout=5)
            channel = await connection.channel(publisher_confirms=True, on_return_raises=True)
            exchange = await channel.declare_exchange('climate.audit', 'direct', durable=True)
            dead = await channel.declare_exchange('climate.audit.dead', 'direct', durable=True)
            dlq = await channel.declare_queue('climate.audit.events.dlq', durable=True)
            await dlq.bind(dead, 'audit.created')
            queue = await channel.declare_queue('climate.audit.events', durable=True, arguments={
                'x-dead-letter-exchange': 'climate.audit.dead', 'x-dead-letter-routing-key': 'audit.created'})
            await queue.bind(exchange, 'audit.created')
            while True:
                for collection in collections:
                    async for document in collection.find({'auditPublishedAt': None, 'auditEvent': {'$exists': True}}).limit(100):
                        event = document['auditEvent']
                        await exchange.publish(aio_pika.Message(json.dumps(event).encode(), content_type='application/json',
                            message_id=event['EventId'], correlation_id=event['CorrelationId'],
                            delivery_mode=aio_pika.DeliveryMode.PERSISTENT), routing_key='audit.created', mandatory=True, timeout=10)
                        await collection.update_one({'_id': document['_id']}, {'$set': {'auditPublishedAt': datetime.now(timezone.utc)}})
                await asyncio.sleep(5)
        except asyncio.CancelledError:
            raise
        except Exception as error:
            logger.warning('Embedded outbox retry error_type=%s', type(error).__name__)
            await asyncio.sleep(5)
        finally:
            if connection is not None:
                await connection.close()
