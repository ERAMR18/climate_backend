CREATE TABLE IF NOT EXISTS schema_versions(version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS alert_rules(id uuid PRIMARY KEY, payload jsonb NOT NULL);
CREATE INDEX IF NOT EXISTS ix_rules_sensor ON alert_rules ((payload->>'sensorType'), (payload->>'isActive'));
CREATE TABLE IF NOT EXISTS alerts(id uuid PRIMARY KEY, payload jsonb NOT NULL);
CREATE INDEX IF NOT EXISTS ix_alerts_sensor_risk ON alerts ((payload->>'sensorId'), (payload->>'alertType'), (payload->>'isActive'));
CREATE INDEX IF NOT EXISTS ix_alerts_community_status ON alerts ((payload->>'communityId'), (payload->>'status'));
CREATE TABLE IF NOT EXISTS processed_readings(reading_id uuid PRIMARY KEY, fingerprint text NOT NULL, result jsonb NOT NULL);
CREATE TABLE IF NOT EXISTS delivery_outbox(id uuid PRIMARY KEY, kind varchar(16) NOT NULL, payload jsonb NOT NULL,
  correlation_id varchar(128) NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), published_at timestamptz);
CREATE INDEX IF NOT EXISTS ix_delivery_pending ON delivery_outbox(created_at) WHERE published_at IS NULL;
INSERT INTO schema_versions(version) VALUES(1) ON CONFLICT DO NOTHING;
