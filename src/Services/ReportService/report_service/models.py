from datetime import datetime, timezone
from enum import StrEnum
import os
from uuid import UUID
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

from pydantic import AwareDatetime, BaseModel, ConfigDict, Field, field_validator, model_validator


class ReportType(StrEnum):
    COMMUNITIES = 'COMMUNITIES'
    SENSORS = 'SENSORS'
    READINGS = 'READINGS'
    ALERTS = 'ALERTS'
    EVENTS = 'EVENTS'
    SENSOR_STATUS = 'SENSOR_STATUS'
    CLIMATE_STATISTICS = 'CLIMATE_STATISTICS'
    EXECUTIVE = 'EXECUTIVE'


TITLES = {ReportType.COMMUNITIES: 'Reporte de comunidades', ReportType.SENSORS: 'Reporte de sensores',
          ReportType.READINGS: 'Historial de lecturas', ReportType.ALERTS: 'Reporte de alertas',
          ReportType.EVENTS: 'Historial de eventos climáticos', ReportType.SENSOR_STATUS: 'Estado de sensores',
          ReportType.CLIMATE_STATISTICS: 'Estadísticas climáticas', ReportType.EXECUTIVE: 'Reporte ejecutivo'}
SENSOR_TYPES = ['Temperature', 'Humidity', 'WindSpeed', 'Rainfall', 'WaterLevel', 'RiverLevel', 'ReservoirLevel', 'Smoke', 'Other']


class Filters(BaseModel):
    model_config = ConfigDict(extra='forbid', populate_by_name=True)
    sensor_id: UUID | None = Field(default=None, alias='sensorId')
    community_id: UUID | None = Field(default=None, alias='communityId')
    from_: AwareDatetime | None = Field(default=None, alias='from')
    to: AwareDatetime | None = None
    sensor_type: str | None = Field(default=None, alias='sensorType')
    municipality: str | None = Field(default=None, max_length=120)
    department: str | None = Field(default=None, max_length=120)
    search: str | None = Field(default=None, max_length=120)
    is_active: bool | None = Field(default=None, alias='isActive')
    risk_type: str | None = Field(default=None, alias='riskType')
    status: str | None = None
    timezone: str = 'America/Guatemala'

    @field_validator('sensor_type')
    @classmethod
    def valid_sensor_type(cls, value):
        if value is not None and value not in SENSOR_TYPES:
            raise ValueError('Unsupported sensor type')
        return value

    @field_validator('risk_type')
    @classmethod
    def valid_risk(cls, value):
        if value is not None and value not in {'Flood', 'Drought', 'Storm', 'Frost', 'ForestFire'}:
            raise ValueError('Unsupported phenomenon')
        return value

    @field_validator('status')
    @classmethod
    def valid_status(cls, value):
        if value is not None and value not in {'Active', 'Attended', 'Closed'}:
            raise ValueError('Unsupported alert state')
        return value

    @field_validator('timezone')
    @classmethod
    def valid_timezone(cls, value):
        try:
            ZoneInfo(value)
        except (ZoneInfoNotFoundError, ValueError) as error:
            raise ValueError('Unsupported timezone') from error
        return value

    @model_validator(mode='after')
    def valid_range(self):
        if self.from_ and self.to and (self.from_ > self.to or (self.to - self.from_).total_seconds() > int(os.getenv('REPORT_MAX_RANGE_DAYS', '366')) * 86400):
            raise ValueError('Invalid or oversized date range')
        return self


class GenerateRequest(BaseModel):
    model_config = ConfigDict(extra='forbid')
    type: ReportType
    filters: Filters = Field(default_factory=Filters)

    @model_validator(mode='after')
    def bound_historical_queries(self):
        if self.type in {ReportType.READINGS, ReportType.CLIMATE_STATISTICS} and (not self.filters.from_ or not self.filters.to):
            raise ValueError('Historical reports require both dates')
        return self


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def metadata(document: dict) -> dict:
    return {key: document.get(key) for key in ('id', 'type', 'userId', 'userName', 'generatedAt', 'parameters', 'status',
        'fileName', 'fileReference', 'downloadedAt', 'expiresAt', 'size', 'mime', 'sha256', 'error', 'progress', 'completedAt', 'rowCount')}
