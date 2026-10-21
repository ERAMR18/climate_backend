from datetime import datetime, timezone
from uuid import uuid4

import jwt
from fastapi import HTTPException
from pydantic import ValidationError
import pytest

from audit_service.models import AuditEvent
from climate_common.security import ROLE_CLAIM, decode_identity


def event():
    return {'EventId': str(uuid4()), 'OccurredAt': '2026-10-21T12:00:00Z', 'UserId': str(uuid4()),
            'CorrelationId': 'contract-test', 'UserName': 'operator', 'Action': 'Update',
            'Resource': 'Sensor', 'ResourceId': str(uuid4()), 'Description': 'Sensor updated', 'IpAddress': None}


def test_dotnet_and_other_language_events_normalize_to_same_document():
    original = event()
    camel = {key[0].lower() + key[1:]: value for key, value in original.items()}
    assert AuditEvent.model_validate(original).document() == AuditEvent.model_validate(camel).document()
    assert AuditEvent.model_validate(original).schema_version == 1


@pytest.mark.parametrize('field,value', [('EventId', str(__import__('uuid').UUID(int=0))),
    ('Description', ' '), ('UserName', 'a' * 101), ('SchemaVersion', 2), ('OccurredAt', '2026-10-21T12:00:00')])
def test_poison_events_are_rejected(field, value):
    with pytest.raises(ValidationError):
        AuditEvent.model_validate({**event(), field: value})


def test_jwt_requires_signature_issuer_audience_and_expiration(monkeypatch):
    secret = 'test-signing-key-at-least-32-bytes-long'
    monkeypatch.setenv('JWT_SIGNING_KEY', secret)
    now = datetime.now(timezone.utc).timestamp()
    claims = {'sub': str(uuid4()), 'iss': 'ClimateMonitoring.Identity', 'aud': 'ClimateMonitoring.Client',
              'exp': now + 600, ROLE_CLAIM: 'Administrator', 'permissions': ['reports:read']}
    assert decode_identity(jwt.encode(claims, secret, algorithm='HS256')).role == 'Administrator'
    for bad in ({**claims, 'iss': 'wrong'}, {**claims, 'aud': 'wrong'}, {**claims, 'exp': now - 120}):
        with pytest.raises(HTTPException):
            decode_identity(jwt.encode(bad, secret, algorithm='HS256'))
