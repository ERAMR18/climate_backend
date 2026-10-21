import logging
import re
from uuid import uuid4

from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pymongo.errors import PyMongoError

logger = logging.getLogger('climate')


def configure(app: FastAPI) -> None:
    @app.middleware('http')
    async def correlation(request: Request, call_next):
        incoming = request.headers.get('X-Correlation-ID', '')
        request.state.correlation_id = incoming if re.fullmatch(r'[A-Za-z0-9._-]{1,128}', incoming) else str(uuid4())
        response = await call_next(request)
        response.headers['X-Correlation-ID'] = request.state.correlation_id
        return response

    def problem(request: Request, status: int, title: str, headers=None):
        return JSONResponse(status_code=status, media_type='application/problem+json', headers=headers,
                            content={'type': 'about:blank', 'status': status, 'title': title,
                                     'traceId': getattr(request.state, 'correlation_id', None)})

    @app.exception_handler(HTTPException)
    async def http_error(request: Request, error: HTTPException):
        return problem(request, error.status_code, str(error.detail), error.headers)

    @app.exception_handler(RequestValidationError)
    async def validation(request: Request, error: RequestValidationError):
        # Pydantic error inputs can contain credentials or private data. Do not echo them.
        return problem(request, 422, 'Invalid fields, date range or pagination')

    @app.exception_handler(PyMongoError)
    async def persistence(request: Request, error: PyMongoError):
        logger.warning('MongoDB unavailable correlation=%s', getattr(request.state, 'correlation_id', None))
        return problem(request, 503, 'Persistence temporarily unavailable')
