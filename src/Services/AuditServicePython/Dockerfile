FROM python:3.13-slim AS build
WORKDIR /build
COPY src/Services/AuditServicePython/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir --prefix=/install -r requirements.txt
FROM python:3.13-slim
RUN useradd --system --uid 10001 --create-home climate
WORKDIR /app
COPY --from=build /install /usr/local
COPY src/Services/PythonCommon/climate_common ./climate_common
COPY src/Services/AuditServicePython/audit_service ./audit_service
USER climate
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8080/health/ready', timeout=3)"
CMD ["uvicorn", "audit_service.main:app", "--host", "0.0.0.0", "--port", "8080", "--no-access-log"]
