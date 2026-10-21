"""Refresh public report contracts without exposing private APIs."""
import json
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[1]
sys.path[:0]=[str(root/'src/Services/PythonCommon'),str(root/'src/Services/ReportService')]
from report_service.main import app
path=root/'docs/openapi/climate-api-v1.json'
document=json.loads(path.read_text(encoding='utf-8-sig'))
report=app.openapi()
def rewrite(value):
    if isinstance(value,dict):
        return {key:('#/components/schemas/Report'+item.rsplit('/',1)[-1] if key=='$ref' and item.startswith('#/components/schemas/') else rewrite(item)) for key,item in value.items()}
    if isinstance(value,list):return [rewrite(item) for item in value]
    return value
document['openapi']='3.1.0'
for route,operations in report['paths'].items():
    if route.startswith('/api/v1/reports'):
        document['paths'][route.replace('/api/v1/reports','/api/reports',1)]=rewrite(operations)
for name,schema in report['components']['schemas'].items():
    document.setdefault('components',{}).setdefault('schemas',{})['Report'+name]=rewrite(schema)
document['components'].setdefault('securitySchemes',{}).update(report['components']['securitySchemes'])
document['components']['schemas']['DashboardClimateStatistic']={'type':'object','properties':{
    'sensorType':{'$ref':'#/components/schemas/SensorType'},'unit':{'type':'string'},'count':{'type':'integer','format':'int32'},
    **{name:{'type':'number'} for name in ('average','minimum','maximum')}}}
document['components']['schemas']['DashboardSummary']['properties']['climateStatistics']={
    'type':'array','items':{'$ref':'#/components/schemas/DashboardClimateStatistic'}}
path.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Refreshed public Report OpenAPI paths; private endpoints excluded.')
