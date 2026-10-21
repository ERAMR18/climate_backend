from datetime import datetime, timezone
import asyncio
from uuid import uuid4

from pydantic import ValidationError
import pytest

from report_service.models import Filters, GenerateRequest, ReportType, TITLES
from report_service.data import ReportData
from report_service.worker import templates


def test_eight_types_and_bounded_historical_queries():
    assert len(ReportType) == len(TITLES) == 8
    with pytest.raises(ValidationError):
        GenerateRequest(type='READINGS')
    with pytest.raises(ValidationError):
        GenerateRequest(type='READINGS',filters={'from':'2026-10-02T00:00:00Z','to':'2026-10-01T00:00:00Z'})
    with pytest.raises(ValidationError):
        Filters.model_validate({'sensorId':'../../etc/passwd'})
    with pytest.raises(ValidationError):
        Filters.model_validate({'sensorType':'invented'})


def test_template_escapes_domain_data_and_preserves_print_requirements():
    row=templates.get_template('row.html').render(values=['<script>secret()</script>',None])
    assert '<script>' not in row
    assert '&lt;script&gt;' in row
    header=templates.get_template('header.html').render(title='Test',generated_at='2026-10-21',user_name='admin',timezone='UTC',parameters={})
    assert 'counter(page)' in header and 'table-header-group' in header and 'Logo Climate Monitoring' in header


def test_sensor_status_distinguishes_inactive_from_communication_loss():
    active,inactive=str(uuid4()),str(uuid4());community=str(uuid4())
    class Clients:
        async def pages(self,service,path,parameters,correlation):
            if service=='COMMUNITY': yield {'id':community,'name':'Community'}
            else:
                for id_,state in [(active,True),(inactive,False)]:
                    yield {'id':id_,'communityId':community,'isActive':state,'name':id_,'code':id_,'unit':'°C'}
        async def get(self,*args): return []
    async def scenario():
        sections=await ReportData(Clients(),Filters(timezone='UTC'),'test').sections(ReportType.SENSOR_STATUS)
        rows=[row async for row in sections[0].rows]
        assert {row[3] for row in rows}=={'Sin comunicación','Inactivo'}
    asyncio.run(scenario())


def test_statistics_separate_sensors_units_and_rainfall_semantics(monkeypatch):
    class Clients:
        async def pages(self,service,path,parameters,correlation):
            if service=='MONITORING':
                for sensor,type_,unit,value in [('a','Temperature','°C',10),('a','Temperature','°C',20),('a','Temperature','°F',70),('b','Rainfall','mm',3),('b','Rainfall','mm',5)]:
                    yield {'sensorId':sensor,'sensorType':type_,'unit':unit,'value':value}
            elif service=='SENSOR':
                for id_ in ['a','b']:yield {'id':id_,'name':id_}
            elif service=='COMMUNITY':yield {'id':'c','name':'Community'}
    async def scenario():
        sections=await ReportData(Clients(),Filters(timezone='UTC'),'test').sections(ReportType.CLIMATE_STATISTICS)
        return [row async for row in sections[0].rows]
    monkeypatch.setenv('RAINFALL_MODE','incremental')
    rows=asyncio.run(scenario());assert len(rows)==3
    celsius=next(row for row in rows if row[2]=='°C');assert celsius[3:7]==[2,'15.0000','10','20']
    assert next(row for row in rows if row[2]=='mm')[7]=='8'
    monkeypatch.setenv('RAINFALL_MODE','unknown')
    assert next(row for row in asyncio.run(scenario()) if row[2]=='mm')[7]=='No definido'
