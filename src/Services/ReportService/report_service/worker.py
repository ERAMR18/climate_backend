import asyncio
from datetime import timedelta
import hashlib
import json
import logging
import os
from pathlib import Path
import sys
from uuid import uuid4

from jinja2 import Environment, FileSystemLoader, select_autoescape
from pymongo import ReturnDocument

from .clients import DependencyFailure, DomainClients, ReportLimit
from .data import ReportData
from .models import Filters, ReportType, TITLES, utcnow

logger = logging.getLogger('climate.reports')
templates = Environment(loader=FileSystemLoader(Path(__file__).parent / 'templates'), autoescape=select_autoescape(['html']))


class Worker:
    def __init__(self, database, directory: Path):
        self.db, self.directory = database, directory
        self.id = str(uuid4())

    async def heartbeat(self, id_):
        while True:
            await asyncio.sleep(15)
            result = await self.db.reports.update_one({'_id': id_, 'leaseOwner': self.id, 'status': 'PROCESSING'},
                {'$set': {'leaseUntil': utcnow() + timedelta(seconds=60)}})
            if not result.matched_count:
                return

    async def execute(self, job):
        clients = DomainClients()
        html_path = self.directory / (job['id'] + '.' + self.id + '.html')
        partial = self.directory / (job['id'] + '.' + self.id + '.partial.pdf')
        final = self.directory / (job['id'] + '.pdf')
        heartbeat = asyncio.create_task(self.heartbeat(job['_id']))
        try:
            await clients.authenticate()
            filters = Filters.model_validate(job['parameters'])
            data = ReportData(clients, filters, job['correlationId'])
            sections = await data.sections(ReportType(job['type']))
            count = 0
            with html_path.open('w', encoding='utf-8') as output:
                output.write(templates.get_template('header.html').render(title=TITLES[ReportType(job['type'])],
                    generated_at=data.date(job['generatedAt'].isoformat()), user_name=job['userName'],
                    timezone=filters.timezone, parameters=json.dumps(job['parameters'], ensure_ascii=False)))
                for section in sections:
                    output.write(templates.get_template('section.html').render(title=section.title, columns=section.columns))
                    section_count = 0
                    async for values in section.rows:
                        count += 1; section_count += 1
                        if count > clients.maximum_rows:
                            raise ReportLimit('Report exceeds configured row limit')
                        output.write(templates.get_template('row.html').render(values=values))
                    if section_count == 0:
                        output.write(f'<tr><td colspan="{len(section.columns)}">Sin resultados para los filtros indicados.</td></tr>')
                    output.write('</tbody></table>')
                output.write(templates.get_template('footer.html').render(communication_timeout=os.getenv('SENSOR_COMMUNICATION_TIMEOUT_SECONDS','900')))
            await self.db.reports.update_one({'_id': job['_id'], 'leaseOwner': self.id}, {'$set': {'progress': 70}})
            process = await asyncio.create_subprocess_exec(sys.executable, '-m', 'report_service.render', str(html_path), str(partial),
                stdout=asyncio.subprocess.DEVNULL, stderr=asyncio.subprocess.DEVNULL)
            try:
                await asyncio.wait_for(process.wait(), timeout=int(os.getenv('REPORT_RENDER_TIMEOUT_SECONDS','180')))
                if process.returncode != 0:
                    raise RuntimeError('PDF renderer failed')
            except BaseException:
                if process.returncode is None:
                    process.kill(); await process.wait()
                raise
            size = partial.stat().st_size
            if size > int(os.getenv('REPORT_MAX_PDF_BYTES', str(25 * 1024 * 1024))):
                raise ReportLimit('Generated document exceeds PDF limit')
            digest = hashlib.sha256()
            with partial.open('rb') as stream:
                if stream.read(5) != b'%PDF-':
                    raise RuntimeError('Invalid PDF output')
                stream.seek(0)
                for chunk in iter(lambda: stream.read(65536), b''):
                    digest.update(chunk)
            ownership = await self.db.reports.find_one({'_id': job['_id'], 'leaseOwner': self.id, 'status': 'PROCESSING'})
            if ownership is None:
                raise RuntimeError('Report lease lost')
            partial.replace(final)
            await self.db.reports.update_one({'_id': job['_id'], 'leaseOwner': self.id}, {'$set': {
                'status': 'COMPLETED', 'progress': 100, 'completedAt': utcnow(), 'rowCount': count,
                'size': size, 'mime': 'application/pdf', 'sha256': digest.hexdigest(), 'fileReference': job['id'], 'error': None},'$unset':{'activeSlot':''}})
        except asyncio.CancelledError:
            raise
        except Exception as error:
            code = 'DEPENDENCY_UNAVAILABLE' if isinstance(error, DependencyFailure) else 'REPORT_LIMIT_EXCEEDED' if isinstance(error, ReportLimit) else 'GENERATION_FAILED'
            logger.warning('Report failed report_id=%s error_type=%s', job['id'], type(error).__name__)
            await self.db.reports.update_one({'_id': job['_id'], 'leaseOwner': self.id}, {'$set': {'status': 'FAILED', 'error': code, 'completedAt': utcnow()},'$unset':{'activeSlot':''}})
        finally:
            heartbeat.cancel(); await asyncio.gather(heartbeat, return_exceptions=True)
            await clients.close(); html_path.unlink(missing_ok=True); partial.unlink(missing_ok=True)

    async def cleanup(self):
        async for job in self.db.reports.find({'expiresAt': {'$lt': utcnow()}, 'fileExpired': {'$ne': True}}).limit(100):
            (self.directory / (str(job['_id']) + '.pdf')).unlink(missing_ok=True)
            await self.db.reports.update_one({'_id': job['_id']}, {'$set': {'fileExpired': True}})
        # Only abandoned generated temporary files are eligible for cleanup.
        cutoff = utcnow().timestamp() - 86400
        for pattern in ('*.html', '*.partial.pdf'):
            for path in self.directory.glob(pattern):
                if path.stat().st_mtime < cutoff:
                    path.unlink(missing_ok=True)

    async def run(self):
        while True:
            try:
                await self.cleanup()
                await self.db.reports.update_many({'status': 'PROCESSING', 'leaseUntil': {'$lt': utcnow()}, 'attempts': {'$gte': 3}},
                    {'$set': {'status': 'FAILED', 'error': 'INTERRUPTED_TOO_MANY_TIMES'},'$unset':{'activeSlot':''}})
                job = await self.db.reports.find_one_and_update({'$or': [{'status': 'PENDING'},
                    {'status': 'PROCESSING', 'leaseUntil': {'$lt': utcnow()}, 'attempts': {'$lt': 3}}]},
                    {'$set': {'status': 'PROCESSING', 'progress': 10, 'leaseOwner': self.id, 'leaseUntil': utcnow() + timedelta(seconds=60)},
                     '$inc': {'attempts': 1}}, sort=[('generatedAt', 1)], return_document=ReturnDocument.AFTER)
                if job:
                    await self.execute(job)
                else:
                    await asyncio.sleep(1)
            except asyncio.CancelledError:
                raise
            except Exception as error:
                logger.warning('Report worker retry error_type=%s', type(error).__name__)
                await asyncio.sleep(5)
