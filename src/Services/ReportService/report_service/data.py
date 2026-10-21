from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import os
from typing import AsyncIterable
from zoneinfo import ZoneInfo

from .clients import DomainClients
from .models import Filters, ReportType, SENSOR_TYPES


@dataclass
class Section:
    title: str
    columns: list[str]
    rows: AsyncIterable[list]


async def sequence(rows):
    for row in rows:
        yield row


def instant(value: str) -> datetime:
    return datetime.fromisoformat(value.replace('Z', '+00:00')).astimezone(timezone.utc)


def sensor_type(value):
    return SENSOR_TYPES[value] if isinstance(value, int) and 0 <= value < len(SENSOR_TYPES) else value


def label(value):
    return {'Active': 'Activa', 'Attended': 'Atendida', 'Closed': 'Cerrada', 'Green': 'Verde', 'Yellow': 'Amarillo',
            'Orange': 'Naranja', 'Red': 'Rojo', 'Flood': 'Inundación', 'Drought': 'Sequía', 'Storm': 'Tormenta',
            'Frost': 'Helada', 'ForestFire': 'Incendio forestal'}.get(value, value)


class ReportData:
    def __init__(self, clients: DomainClients, filters: Filters, correlation: str):
        self.clients, self.f, self.correlation = clients, filters, correlation
        self.communities: dict = {}
        self.sensors: dict = {}

    def date(self, value):
        return instant(value).astimezone(ZoneInfo(self.f.timezone)).strftime('%Y-%m-%d %H:%M:%S %Z') if value else 'Sin registro'

    def parameters(self, kind):
        f = self.f
        candidates = {'communityId': str(f.community_id) if f.community_id else None,
                      'sensorId': str(f.sensor_id) if f.sensor_id else None,
                      'from': f.from_.isoformat() if f.from_ else None, 'to': f.to.isoformat() if f.to else None,
                      'type': f.sensor_type, 'riskType': f.risk_type, 'status': f.status,
                      'municipality': f.municipality, 'department': f.department, 'search': f.search,
                      'isActive': str(f.is_active).lower() if f.is_active is not None else None}
        allowed = {'communities': {'municipality','department','search','isActive'},
                   'sensors': {'communityId','type','search','isActive'}, 'readings': {'communityId','sensorId','from','to','type'},
                   'alerts': {'communityId','sensorId','from','to','riskType','status'},
                   'events': {'communityId','sensorId','from','to','riskType'}}[kind]
        return {key: value for key, value in candidates.items() if key in allowed and value is not None}

    async def rows(self, kind):
        service, path = {'communities': ('COMMUNITY','api/v1/communities'), 'sensors': ('SENSOR','api/v1/sensors'),
                         'readings': ('MONITORING','api/v1/monitoring/readings'), 'alerts': ('ALERT','api/v1/alerts'),
                         'events': ('EVENT','api/v1/events')}[kind]
        async for row in self.clients.pages(service, path, self.parameters(kind), self.correlation):
            if kind == 'communities' and self.f.community_id and row['id'] != str(self.f.community_id):
                continue
            if kind == 'sensors' and self.f.sensor_id and row['id'] != str(self.f.sensor_id):
                continue
            if kind in {'alerts','events'} and self.f.sensor_type:
                item = self.sensors.get(row['sensorId'])
                if item is None or sensor_type(item['type']) != self.f.sensor_type:
                    continue
            yield row

    def names(self, row):
        sensor = self.sensors.get(row['sensorId'], {})
        community = self.communities.get(row['communityId'], {})
        return sensor.get('name', row['sensorId']), community.get('name', row['communityId'])

    async def sections(self, type_: ReportType) -> list[Section]:
        if type_ == ReportType.COMMUNITIES:
            async def communities():
                async for x in self.rows('communities'):
                    yield [x['name'], x.get('municipality'), x.get('department'), x.get('country'),
                           f"{x['latitude']}, {x['longitude']}", 'Activa' if x['isActive'] else 'Inactiva', x['sensorCount']]
            return [Section('Comunidades', ['Nombre','Municipio','Departamento','País','Coordenadas','Estado','Sensores'], communities())]
        if type_ == ReportType.SENSORS:
            async def sensors():
                async for x in self.rows('sensors'):
                    yield [x['code'], x['name'], sensor_type(x['type']), x['communityName'], x.get('location'), x['unit'],
                           'Activo' if x['isActive'] else 'Inactivo', x.get('installationDate')]
            return [Section('Sensores', ['Código','Nombre','Tipo','Comunidad','Ubicación','Unidad','Estado','Instalación'], sensors())]

        self.communities = {x['id']: x async for x in self.rows('communities')}
        self.sensors = {x['id']: x async for x in self.rows('sensors')}
        if type_ == ReportType.READINGS:
            async def readings():
                async for x in self.rows('readings'):
                    sensor, community = self.names(x)
                    state = 'Sin registro' if x.get('sensorWasActive') is None else ('Activo' if x['sensorWasActive'] else 'Inactivo')
                    yield [sensor, community, self.date(x['recordedAt']), x['value'], x['unit'], state]
            return [Section('Lecturas históricas', ['Sensor','Comunidad','Fecha y hora','Valor','Unidad','Estado registrado'], readings())]
        if type_ == ReportType.ALERTS:
            async def alerts():
                async for x in self.rows('alerts'):
                    sensor, community = self.names(x)
                    yield [self.date(x['generatedAt']),community,sensor,label(x['alertType']),label(x['level']),
                           x['sensorValue'],x['thresholdValue'],self.sensors.get(x['sensorId'],{}).get('unit'),x['description'],label(x['status'])]
            return [Section('Alertas y atención', ['Fecha y hora','Comunidad','Sensor','Fenómeno','Nivel','Valor','Umbral','Unidad','Mensaje','Estado'], alerts())]
        if type_ == ReportType.EVENTS:
            async def events():
                async for x in self.rows('events'):
                    sensor, community = self.names(x)
                    yield [self.date(x['occurredAt']),community,sensor,label(x['riskType']),label(x['alertLevel']),x.get('value'),label(x['status']),x['description']]
            return [Section('Eventos climáticos', ['Fecha y hora','Comunidad','Sensor','Fenómeno','Nivel','Valor','Estado','Descripción'], events())]
        if type_ == ReportType.SENSOR_STATUS:
            latest = await self.clients.get('MONITORING','api/v1/monitoring/current',{},self.correlation)
            by_sensor = {x['sensorId']: x for x in latest}
            cutoff = datetime.now(timezone.utc) - timedelta(seconds=int(os.getenv('SENSOR_COMMUNICATION_TIMEOUT_SECONDS','900')))
            rows=[]
            for x in self.sensors.values():
                reading=by_sensor.get(x['id']); state='Inactivo' if not x['isActive'] else (
                    'Sin comunicación' if not reading or instant(reading['recordedAt']) < cutoff else 'Activo')
                rows.append([x['code'],x['name'],self.communities.get(x['communityId'],{}).get('name',x['communityId']),state,
                             self.date(reading['recordedAt']) if reading else 'Sin lecturas', reading['value'] if reading else None, x['unit']])
            return [Section('Estado de comunicación de sensores', ['Código','Sensor','Comunidad','Estado','Última lectura','Valor','Unidad'], sequence(rows))]

        groups=defaultdict(lambda:{'n':0,'sum':Decimal(0),'min':None,'max':None})
        async for x in self.rows('readings'):
            key=(x['sensorId'],sensor_type(x['sensorType']),x['unit']); g=groups[key]; value=Decimal(str(x['value']))
            g['n']+=1;g['sum']+=value;g['min']=value if g['min'] is None else min(g['min'],value);g['max']=value if g['max'] is None else max(g['max'],value)
        statistics=[]
        for (id_,sensor_kind,unit),g in sorted(groups.items()):
            accumulated = str(g['sum']) if sensor_kind == 'Rainfall' and os.getenv('RAINFALL_MODE','unknown') == 'incremental' and unit == 'mm' else 'No definido'
            statistics.append([self.sensors.get(id_,{}).get('name',id_),sensor_kind,unit,g['n'],str((g['sum']/g['n']).quantize(Decimal('.0001'))),str(g['min']),str(g['max']),accumulated])
        climate=Section('Estadísticas por sensor, unidad y periodo', ['Sensor','Tipo','Unidad','Lecturas','Promedio','Mínimo','Máximo','Lluvia acumulada (mm)'], sequence(statistics))
        alerts=[x async for x in self.rows('alerts')]
        if type_ == ReportType.CLIMATE_STATISTICS:
            return [climate, Section('Alertas del periodo', ['Indicador','Cantidad'], sequence([['Alertas',len(alerts)]]))]
        events=[x async for x in self.rows('events')]
        active=sum(1 for x in self.sensors.values() if x['isActive'])
        indicators=[['Comunidades',len(self.communities)],['Sensores activos',active],['Sensores inactivos',len(self.sensors)-active],
                    ['Alertas del periodo',len(alerts)],['Alertas activas',sum(1 for x in alerts if x['status']=='Active')],['Eventos del periodo',len(events)]]
        levels=defaultdict(int)
        for x in alerts: levels[label(x['level'])]+=1
        conclusions=[['Situación observada',f'Se registraron {len(alerts)} alertas y {len(events)} eventos en el periodo consultado.'],
                     ['Cobertura',f'{len(self.communities)} comunidades y {len(self.sensors)} sensores incluidos por los filtros.'],
                     ['Datos disponibles',f'{sum(g["n"] for g in groups.values())} lecturas; no se imputaron valores faltantes.']]
        return [Section('Resumen ejecutivo e indicadores', ['Indicador','Valor'], sequence(indicators)),
                Section('Distribución por nivel', ['Nivel','Cantidad'], sequence(list(levels.items()))), climate,
                Section('Conclusiones basadas en los registros consultados', ['Aspecto','Conclusión'], sequence(conclusions))]
