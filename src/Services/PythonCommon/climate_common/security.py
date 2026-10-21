from dataclasses import dataclass
import os
from uuid import UUID

import jwt
from fastapi import Depends, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
bearer = HTTPBearer(auto_error=False)


@dataclass(frozen=True)
class Identity:
    id: str
    name: str
    role: str
    permissions: frozenset[str]


def decode_identity(token: str) -> Identity:
    secret = os.environ['JWT_SIGNING_KEY']
    if len(secret.encode()) < 32:
        raise RuntimeError('JWT signing key must contain at least 32 bytes')
    try:
        claims = jwt.decode(token, secret, algorithms=['HS256'],
                            issuer=os.getenv('JWT_ISSUER', 'ClimateMonitoring.Identity'),
                            audience=os.getenv('JWT_AUDIENCE', 'ClimateMonitoring.Client'),
                            leeway=30, options={'require': ['sub', 'iss', 'aud', 'exp']})
        subject = str(UUID(claims['sub']))
        role = claims.get(ROLE_CLAIM)
        if role not in {'Administrator', 'Operator', 'Viewer'}:
            raise ValueError('Invalid role')
        permissions = claims.get('permissions', [])
        if not isinstance(permissions, list) or not all(isinstance(item, str) for item in permissions):
            raise ValueError('Invalid permissions')
        return Identity(subject, claims.get('unique_name') or subject, role, frozenset(permissions))
    except (jwt.PyJWTError, ValueError, TypeError, KeyError) as error:
        raise HTTPException(401, 'Invalid or expired token', headers={'WWW-Authenticate': 'Bearer'}) from error


def authenticated(credentials: HTTPAuthorizationCredentials | None = Depends(bearer)) -> Identity:
    if credentials is None:
        raise HTTPException(401, 'Authentication required', headers={'WWW-Authenticate': 'Bearer'})
    return decode_identity(credentials.credentials)


def administrator(identity: Identity = Depends(authenticated)) -> Identity:
    if identity.role != 'Administrator':
        raise HTTPException(403, 'Administrator role required')
    return identity
