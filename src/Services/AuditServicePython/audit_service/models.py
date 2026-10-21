from datetime import timezone
from uuid import UUID

from pydantic import AliasChoices, AwareDatetime, BaseModel, ConfigDict, Field, field_validator


class AuditEvent(BaseModel):
    model_config = ConfigDict(extra='ignore', populate_by_name=True)
    schema_version: int = Field(default=1, validation_alias=AliasChoices('SchemaVersion', 'schemaVersion'), ge=1, le=1)
    event_id: UUID = Field(validation_alias=AliasChoices('EventId', 'eventId'))
    occurred_at: AwareDatetime = Field(validation_alias=AliasChoices('OccurredAt', 'occurredAt'))
    correlation_id: str = Field(validation_alias=AliasChoices('CorrelationId', 'correlationId'), min_length=1, max_length=128)
    user_id: UUID = Field(validation_alias=AliasChoices('UserId', 'userId'))
    user_name: str = Field(validation_alias=AliasChoices('UserName', 'userName'), min_length=1, max_length=100)
    action: str = Field(validation_alias=AliasChoices('Action', 'action'), min_length=1, max_length=64)
    resource: str = Field(validation_alias=AliasChoices('Resource', 'resource'), min_length=1, max_length=100)
    resource_id: str | None = Field(default=None, validation_alias=AliasChoices('ResourceId', 'resourceId'), max_length=100)
    description: str = Field(validation_alias=AliasChoices('Description', 'description'), min_length=1, max_length=1000)
    ip_address: str | None = Field(default=None, validation_alias=AliasChoices('IpAddress', 'ipAddress'), max_length=64)

    @field_validator('event_id', 'user_id')
    @classmethod
    def nonzero(cls, value: UUID) -> UUID:
        if value.int == 0:
            raise ValueError('Empty identifier')
        return value

    @field_validator('user_name', 'action', 'resource', 'description', 'correlation_id')
    @classmethod
    def nonblank(cls, value: str) -> str:
        if not value.strip():
            raise ValueError('Blank audit field')
        return value

    def document(self) -> dict:
        return {'_id': str(self.event_id), 'eventId': str(self.event_id), 'userId': str(self.user_id),
                'userName': self.user_name, 'action': self.action, 'resource': self.resource,
                'resourceId': self.resource_id, 'description': self.description, 'ipAddress': self.ip_address,
                'correlationId': self.correlation_id, 'timestamp': self.occurred_at.astimezone(timezone.utc),
                'schemaVersion': self.schema_version}


def response(document: dict) -> dict:
    return {'id': document['_id'], **{key: document.get(key) for key in
            ('userId', 'userName', 'action', 'resource', 'resourceId', 'description', 'ipAddress', 'timestamp', 'correlationId')}}
