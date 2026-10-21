FROM python:3.13-slim AS build
WORKDIR /build
COPY src/Services/ReportService/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir --prefix=/install -r requirements.txt
FROM python:3.13-slim
RUN apt-get update && apt-get install -y --no-install-recommends libpango-1.0-0 libpangoft2-1.0-0 libharfbuzz-subset0 libjpeg62-turbo libopenjp2-7 fonts-dejavu-core tzdata && rm -rf /var/lib/apt/lists/*
RUN useradd --system --uid 10001 --create-home climate && mkdir -p /data/reports && chown climate /data/reports
WORKDIR /app
COPY --from=build /install /usr/local
COPY src/Services/PythonCommon/climate_common ./climate_common
COPY src/Services/ReportService/report_service ./report_service
USER climate
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8080/health/ready', timeout=3)"
CMD ["uvicorn","report_service.main:app","--host","0.0.0.0","--port","8080","--no-access-log"]
