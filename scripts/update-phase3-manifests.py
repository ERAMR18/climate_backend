"""Idempotent Phase 3 manifest maintenance; does not contact a cluster."""
from pathlib import Path
import copy
import yaml

root = Path(__file__).resolve().parents[1] / 'k8s'
def read(path): return yaml.safe_load((root/path).read_text(encoding='utf-8-sig'))
def write(path, data):
    (root/path).parent.mkdir(parents=True, exist_ok=True)
    (root/path).write_text(yaml.safe_dump(data, sort_keys=False, allow_unicode=True), encoding='utf-8')
def secret(name, key): return {'name':name,'valueFrom':{'secretKeyRef':{'name':'climate-secrets','key':key}}}
def literal(name, value): return {'name':name,'value':str(value)}
def common(): return [secret('JWT_SIGNING_KEY','JWT_SIGNING_KEY'), secret('INTERNAL_API_KEY','INTERNAL_API_KEY')]

definitions = {
 'identity': ('climate/auth:phase3',1000,common()+[secret('AUTH_DATABASE_URL','AUTH_DATABASE_URL'),secret('RABBITMQ_URL','RABBITMQ_URL'),literal('NODE_ENV','production'),literal('PORT','8080')]),
 'alerts': ('climate/alerts:phase3',1000,common()+[secret('ALERT_DATABASE_URL','ALERT_DATABASE_URL'),secret('ALERT_DATABASE_USER','ALERT_DATABASE_USER'),secret('ALERT_DATABASE_PASSWORD','ALERT_DATABASE_PASSWORD'),secret('RABBITMQ_USER','RABBITMQ_USER'),secret('RABBITMQ_PASSWORD','RABBITMQ_PASSWORD'),literal('SENSOR_SERVICE_URL','http://sensor-service:8080/'),literal('EVENT_SERVICE_URL','http://event-service:8080/'),literal('REALTIME_SERVICE_URL','http://monitoring-service:8080/')]),
 'audit': ('climate/audit:phase3',10001,common()+[secret('MONGO_URL','AUDIT_MONGO_URL'),secret('RABBITMQ_URL','RABBITMQ_URL'),literal('MONGO_DATABASE','audit')]),
 'community': ('climate/community:phase3',1654,[secret('Jwt__SigningKey','JWT_SIGNING_KEY'),secret('InternalApi__ApiKey','INTERNAL_API_KEY'),secret('ConnectionStrings__CommunityDb','COMMUNITY_DATABASE_CONNECTION'),secret('RABBITMQ_USER','RABBITMQ_USER'),secret('RABBITMQ_PASSWORD','RABBITMQ_PASSWORD'),secret('SensorService__ApiKey','INTERNAL_API_KEY')]),
 'reports': ('climate/reports:phase3',10001,common()+[secret('MONGO_URL','REPORT_MONGO_URL'),secret('RABBITMQ_URL','RABBITMQ_URL'),literal('MONGO_DATABASE','reports'),literal('REPORT_STORAGE_DIRECTORY','/data/reports'),literal('REPORT_MAX_ROWS','50000'),literal('REPORT_RETENTION_DAYS','30'),literal('REPORT_RENDER_TIMEOUT_SECONDS','180'),literal('RAINFALL_MODE','unknown')]+[literal(key+'_SERVICE_URL','http://'+value+':8080/') for key,value in [('AUTH','identity-service'),('COMMUNITY','community-service'),('SENSOR','sensor-service'),('MONITORING','monitoring-service'),('ALERT','alert-service'),('EVENT','event-service')]])
}
template=read('events/deployment.yaml')
service=read('events/service.yaml')
for name,(image,uid,env) in definitions.items():
    filename=f'{name}/deployment.yaml'
    document=read(filename) if (root/filename).exists() else copy.deepcopy(template)
    document['metadata']['name']=name
    spec=document['spec']; labels={'app.kubernetes.io/name':name,'app.kubernetes.io/part-of':'climate-monitoring'}
    spec['selector']['matchLabels']=labels; spec['template']['metadata']['labels']=labels
    pod=spec['template']['spec'];pod['securityContext'].update(runAsUser=uid,runAsGroup=uid,fsGroup=uid)
    container=pod['containers'][0];container.update(name=name,image=image,env=env)
    container['readinessProbe']['httpGet']['path']='/health/ready'
    container['resources']['limits']['memory']='1Gi' if name in {'reports','alerts'} else '512Mi'
    if name=='reports':
        spec['strategy']={'type':'Recreate'}
        container['volumeMounts']=[{'name':'tmp','mountPath':'/tmp'},{'name':'pdfs','mountPath':'/data/reports'}]
        pod['volumes']=[{'name':'tmp','emptyDir':{}},{'name':'pdfs','persistentVolumeClaim':{'claimName':'report-files'}}]
    write(filename,document)
    if not (root/f'{name}/service.yaml').exists():
        svc=copy.deepcopy(service);svc['metadata']['name']=name+'-service';svc['spec']['selector']=labels
        write(f'{name}/service.yaml',svc)

for name in ['gateway','sensors','monitoring','events','frontend']:
    document=read(f'{name}/deployment.yaml')
    document['spec']['template']['spec']['containers'][0]['image']=f'climate/{name}:phase3'
    if name=='gateway': document['spec']['template']['spec']['containers'][0]['readinessProbe']['httpGet']['path']='/health/ready'
    write(f'{name}/deployment.yaml',document)

config=read('configmap.yaml');config['data'].update({'CommunityService__BaseUrl':'http://community-service:8080/','CommunityService__Enabled':'true'})
write('configmap.yaml',config)
gateway=read('gateway/configmap.yaml')['data']
for i,name in enumerate(['communities','reports'],6):
    singular={'communities':'community','reports':'report'}[name]
    gateway.update({f'ReverseProxy__Routes__{name}__ClusterId':name,f'ReverseProxy__Routes__{name}__AuthorizationPolicy':'Default',
        f'ReverseProxy__Routes__{name}__Match__Path':f'/api/{name}/{{**remainder}}',
        f'ReverseProxy__Routes__{name}__Transforms__0__PathRemovePrefix':f'/api/{name}',
        f'ReverseProxy__Routes__{name}__Transforms__1__PathPrefix':f'/api/v1/{name}',
        f'ReverseProxy__Clusters__{name}__Destinations__primary__Address':f'http://{singular}-service:8080/',
        f'DownstreamHealthEndpoints__{i}':f'http://{singular}-service:8080/health'})
gatewayDocument=read('gateway/configmap.yaml');gatewayDocument['data']=gateway;write('gateway/configmap.yaml',gatewayDocument)
# Align report Service DNS with Compose and the fixed URLs above.
reportsService=read('reports/service.yaml');reportsService['metadata']['name']='report-service';write('reports/service.yaml',reportsService)

write('reports/storage.yaml',{'apiVersion':'v1','kind':'PersistentVolumeClaim','metadata':{'name':'report-files','namespace':'climate-monitoring'},'spec':{'accessModes':['ReadWriteOnce'],'resources':{'requests':{'storage':'5Gi'}}}})
kustomization=read('kustomization.yaml')
for path in ['community/deployment.yaml','community/service.yaml','reports/deployment.yaml','reports/service.yaml','reports/storage.yaml']:
    if path not in kustomization['resources']:kustomization['resources'].append(path)
write('kustomization.yaml',kustomization)
secrets=read('secrets.example.yaml')
for key in ['AUTH_DATABASE_URL','COMMUNITY_DATABASE_CONNECTION','ALERT_DATABASE_URL','ALERT_DATABASE_USER','ALERT_DATABASE_PASSWORD','AUDIT_MONGO_URL','REPORT_MONGO_URL','RABBITMQ_URL']:
    secrets['stringData'][key]='REPLACE_WITH_PRIVATE_'+key
for obsolete in ['SQL_IDENTITY_CONNECTION','SQL_ALERTS_CONNECTION','SQL_AUDIT_CONNECTION','ADMIN_SEED_PASSWORD']:
    secrets['stringData'].pop(obsolete,None)
write('secrets.example.yaml',secrets)
print('Updated Phase 3 manifests; external databases require private endpoints and pre-migrated data.')
