package climate.alerts;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.databind.JsonNode;
import jakarta.validation.constraints.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.*;

public final class Models {
    private Models() {}
    public enum SensorType { Temperature, Humidity, WindSpeed, Rainfall, WaterLevel, RiverLevel, ReservoirLevel, Smoke, Other;
        @JsonCreator public static SensorType parse(JsonNode value) {
            if (value.isIntegralNumber() && value.intValue() >= 0 && value.intValue() < values().length) return values()[value.intValue()];
            if (value.isTextual()) return valueOf(value.textValue());
            throw new IllegalArgumentException("Invalid sensor type");
        }
    }
    public enum RiskType { Flood, Drought, Storm, Frost, ForestFire }
    public enum Level { Green, Yellow, Orange, Red }
    public enum Status { Active, Attended, Closed }
    public record RuleInput(@NotBlank @Size(max=200) String name, @NotNull SensorType sensorType,
        @DecimalMin("-1000000") @DecimalMax("1000000") BigDecimal minimumValue,
        @DecimalMin("-1000000") @DecimalMax("1000000") BigDecimal maximumValue,
        @NotNull Level alertLevel, @NotNull RiskType riskType, @NotBlank @Size(max=1000) String message,
        Boolean isActive) {
        @AssertTrue(message="At least one bound is required and minimum must not exceed maximum")
        public boolean isValidInterval() { return (minimumValue != null || maximumValue != null)
            && (minimumValue == null || maximumValue == null || minimumValue.compareTo(maximumValue) <= 0); }
    }
    public record Rule(UUID id, String name, SensorType sensorType, BigDecimal minimumValue, BigDecimal maximumValue,
        Level alertLevel, RiskType riskType, String message, boolean isActive, Instant createdAt, Instant updatedAt) {
        public boolean violated(BigDecimal value) { return (minimumValue != null && value.compareTo(minimumValue) < 0)
            || (maximumValue != null && value.compareTo(maximumValue) > 0); }
    }
    public record Reading(@NotNull UUID eventId, @NotNull Instant occurredAt, @NotBlank @Size(max=128) String correlationId,
        @NotNull UUID readingId, @NotNull UUID sensorId, @NotNull UUID communityId, @NotNull SensorType sensorType,
        @NotNull @DecimalMin("-1000000") @DecimalMax("1000000") BigDecimal value,
        @NotBlank @Size(max=20) String unit, @NotNull Instant recordedAt) {}
    public record Alert(UUID id, UUID sensorId, UUID communityId, RiskType alertType, Level level, String title,
        String description, BigDecimal sensorValue, BigDecimal thresholdValue, Instant generatedAt, boolean isActive,
        Instant resolvedAt, Status status, UUID attendedByUserId, Instant attendedAt, UUID closedByUserId, Instant closedAt,
        UUID ruleId, String ruleName, BigDecimal minimumValueSnapshot, BigDecimal maximumValueSnapshot) {}

    public static List<Rule> assess(Collection<Rule> rules, SensorType type, BigDecimal value) {
        Map<RiskType, Rule> selected = new EnumMap<>(RiskType.class);
        for (Rule rule : rules) if (rule.isActive && rule.sensorType == type && rule.alertLevel != Level.Green && rule.violated(value)) {
            Rule prior = selected.get(rule.riskType);
            if (prior == null || rule.alertLevel.ordinal() > prior.alertLevel.ordinal()
                || (rule.alertLevel == prior.alertLevel && rule.id.toString().compareTo(prior.id.toString()) < 0)) selected.put(rule.riskType, rule);
        }
        return new ArrayList<>(selected.values());
    }
    public static Alert transition(Alert a, UUID user, boolean close, Instant now) {
        if (user == null || user.equals(new UUID(0,0)) || a.status != (close ? Status.Attended : Status.Active))
            throw new Conflict("Attend an active alert before closing it; a closed alert cannot be changed.");
        return new Alert(a.id,a.sensorId,a.communityId,a.alertType,a.level,a.title,a.description,a.sensorValue,a.thresholdValue,
            a.generatedAt,!close,close?now:a.resolvedAt,close?Status.Closed:Status.Attended,
            close?a.attendedByUserId:user,close?a.attendedAt:now,close?user:a.closedByUserId,close?now:a.closedAt,
            a.ruleId,a.ruleName,a.minimumValueSnapshot,a.maximumValueSnapshot);
    }
    public static class Conflict extends RuntimeException { public Conflict(String message) { super(message); } }
    public static class Missing extends RuntimeException {}
}
