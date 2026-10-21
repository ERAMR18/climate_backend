import asyncio
import json
import os

import httpx


class DependencyFailure(Exception):
    pass


class ReportLimit(Exception):
    pass


class DomainClients:
    """Read APIs only. Service identity comes from Auth, never from browser-supplied identity headers."""
    def __init__(self):
        self.http = httpx.AsyncClient(timeout=httpx.Timeout(15, connect=5), follow_redirects=False)
        self.token: str | None = None
        self.maximum_rows = int(os.getenv('REPORT_MAX_ROWS', '50000'))

    async def authenticate(self):
        try:
            response = await self.http.post(os.environ['AUTH_SERVICE_URL'].rstrip('/') + '/api/v1/internal/auth/service-token',
                headers={'X-Internal-Api-Key': os.environ['INTERNAL_API_KEY']}, json={'service': 'reports'})
            response.raise_for_status()
            self.token = response.json()['accessToken']
        except (httpx.HTTPError, ValueError, KeyError) as error:
            raise DependencyFailure('Authentication service unavailable') from error

    async def get(self, service: str, path: str, parameters: dict, correlation: str):
        url = os.environ[service + '_SERVICE_URL'].rstrip('/') + '/' + path
        for attempt in range(2):
            try:
                async with self.http.stream('GET', url, params=parameters, headers={
                    'Authorization': 'Bearer ' + self.token, 'X-Internal-Api-Key': os.environ['INTERNAL_API_KEY'],
                    'X-Correlation-ID': correlation}) as response:
                    if response.status_code in {429, 502, 503, 504} and attempt == 0:
                        await asyncio.sleep(.5)
                        continue
                    response.raise_for_status()
                    data = bytearray()
                    async for chunk in response.aiter_bytes():
                        data.extend(chunk)
                        if len(data) > 8 * 1024 * 1024:
                            raise ReportLimit('Domain response exceeds configured page budget')
                    return json.loads(data)
            except (httpx.HTTPError, ValueError) as error:
                if attempt == 1:
                    raise DependencyFailure('A domain API is temporarily unavailable') from error
                await asyncio.sleep(.5)
        raise DependencyFailure('Domain API unavailable')

    async def pages(self, service: str, path: str, parameters: dict, correlation: str):
        count = 0
        for page in range(1, (self.maximum_rows // 250) + 2):
            data = await self.get(service, path, {**parameters, 'page': page, 'pageSize': 250}, correlation)
            rows = data['items'] if isinstance(data, dict) else data
            if not isinstance(rows, list):
                raise DependencyFailure('Unexpected domain response contract')
            for row in rows:
                count += 1
                if count > self.maximum_rows:
                    raise ReportLimit('Report exceeds configured row limit; narrow filters')
                yield row
            if len(rows) < 250:
                return
        raise ReportLimit('Report pagination limit reached')

    async def close(self):
        await self.http.aclose()
