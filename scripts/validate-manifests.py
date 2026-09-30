"""Validate rendered manifests against Kubernetes' official schema and project constraints."""
import argparse
import json
from pathlib import Path
import subprocess
import urllib.request
from urllib.parse import urlparse

import jsonschema
import yaml

parser = argparse.ArgumentParser()
parser.add_argument('--schema', type=Path, help='Local official Kubernetes 1.34.1 swagger.json; downloads it if omitted.')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1] / 'k8s'
schema_url = 'https://raw.githubusercontent.com/kubernetes/kubernetes/v1.34.1/api/openapi-spec/swagger.json'
if args.schema:
    schema = json.loads(args.schema.read_text(encoding='utf-8-sig'))
else:
    with urllib.request.urlopen(schema_url, timeout=60) as response:
        schema = json.load(response)
rendered = subprocess.run(['kubectl', 'kustomize', str(root)], capture_output=True, text=True, check=True).stdout
objects = list(yaml.safe_load_all(rendered))

def normalize(node):
    if isinstance(node, dict):
        if node.get('format') == 'int-or-string':
            node['type'] = ['integer', 'string']
        for value in node.values():
            normalize(value)
    elif isinstance(node, list):
        for value in node:
            normalize(value)

normalize(schema)
example = yaml.safe_load((root / 'secrets.example.yaml').read_text())

def require(condition, message):
    if not condition:
        raise ValueError(message)

for item in objects + [example]:
    group = {'apps/v1': 'apps', 'networking.k8s.io/v1': 'networking'}.get(item['apiVersion'], 'core')
    key = f'io.k8s.api.{group}.v1.{item["kind"]}'
    document = {'$ref': f'#/definitions/{key}', 'definitions': schema['definitions']}
    jsonschema.Draft4Validator(document).validate(item)
    if item['kind'] != 'Namespace':
        require(item['metadata']['namespace'] == 'climate-monitoring', 'Resource outside the project namespace.')

deployments = [x for x in objects if x['kind'] == 'Deployment']
services = {x['metadata']['name']: x for x in objects if x['kind'] == 'Service'}
configs = {x['metadata']['name']: x['data'] for x in objects if x['kind'] == 'ConfigMap'}
workloads = deployments + [x for x in objects if x['kind'] == 'StatefulSet']
require(len(deployments) == 8, 'Expected eight application Deployments.')
require(len(workloads) == 9, 'Expected the eight Deployments and RabbitMQ StatefulSet.')
require(not any(x['kind'] == 'Secret' for x in objects), 'Example secrets must not be applied by kustomize.')
require(not any('sqlserver' in x['metadata']['name'].lower() for x in workloads), 'SQL Server must stay outside Kubernetes.')
for service in services.values():
    require(service['spec']['type'] == 'ClusterIP', 'An internal service is publicly exposed.')
    require(not service['spec'].get('externalIPs'), 'Unexpected external service IP.')
    require(any(w['spec']['template']['metadata']['labels'] == service['spec']['selector'] for w in workloads), 'Service selector matches no workload.')
for workload in workloads:
    pod = workload['spec']['template']['spec']
    require(pod['automountServiceAccountToken'] is False, 'Unnecessary API token mounted.')
    require(not pod.get('initContainers'), 'Dependencies must recover without startup ordering or build init containers.')
    for container in pod['containers']:
        require(all(k in container for k in ['startupProbe','readinessProbe','livenessProbe','resources']), 'Missing probes or resources.')
        require('mssql' not in container['image'].lower(), 'SQL Server image in a pod.')
        require(not container.get('command') and not container.get('args'), 'Application manifests must execute the built image entrypoint.')
        for entry in container.get('env', []):
            secret = entry.get('valueFrom', {}).get('secretKeyRef')
            if secret:
                require(secret['name'] == example['metadata']['name'] and secret['key'] in example['stringData'], 'Undefined secret reference.')
        for ref in container.get('envFrom', []):
            require(ref['configMapRef']['name'] in configs, 'Undefined ConfigMap reference.')
for config in configs.values():
    for key, value in config.items():
        require(not any(word in key.lower() for word in ['password','signingkey','apikey','connectionstrings']), 'Secret value in ConfigMap.')
        if key.endswith('__BaseUrl') or key.startswith('ReverseProxy__') or key.startswith('DownstreamHealthEndpoints__'):
            require(urlparse(value).hostname in services, f'Internal URL does not use a Kubernetes Service: {key}')
broker = next(x for x in workloads if x['metadata']['name'] == 'rabbitmq')
require(broker['spec'].get('volumeClaimTemplates'), 'RabbitMQ requires a persistent claim template.')
require(broker['spec']['persistentVolumeClaimRetentionPolicy'] == {'whenDeleted': 'Retain', 'whenScaled': 'Retain'}, 'RabbitMQ data must survive scale-down/deletion.')
policy = next(x for x in objects if x['kind'] == 'NetworkPolicy' and x['metadata']['name'] == 'rabbitmq-ingress')
require(policy['spec']['ingress'][0]['ports'] == [{'protocol': 'TCP', 'port': 5672}], 'Only AMQP should be allowed by the broker ingress policy.')
print(f'PASS: {len(objects)} resources + example Secret; official Kubernetes 1.34.1 schema, namespace, selectors, probes, secret references, ClusterIP, internal DNS and external SQL constraints.')
