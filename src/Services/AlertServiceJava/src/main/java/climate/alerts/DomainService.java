package climate.alerts;

import java.math.BigDecimal;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Instant;
import java.util.*;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import static climate.alerts.Models.*;

@Service
public class DomainService {
    private final Store store; private final Delivery delivery;
    public DomainService(Store store,Delivery delivery) { this.store=store;this.delivery=delivery; }
    @Transactional public Rule rule(UUID id,RuleInput input,String correlation) {
        Instant now=Instant.now(); Instant created=id==null?now:store.rule(id).createdAt();
        Rule rule=new Rule(id==null?UUID.randomUUID():id,input.name().trim(),input.sensorType(),input.minimumValue(),input.maximumValue(),input.alertLevel(),input.riskType(),input.message().trim(),input.isActive()==null||input.isActive(),created,now);
        store.save(rule); audit(id==null?"CreateAlertRule":"UpdateAlertRule","AlertRule",rule.id(),correlation);return rule;
    }
    @Transactional public void ruleStatus(UUID id,boolean active,String correlation) {
        Rule a=store.rule(id); store.save(new Rule(a.id(),a.name(),a.sensorType(),a.minimumValue(),a.maximumValue(),a.alertLevel(),a.riskType(),a.message(),active,a.createdAt(),Instant.now()));
        audit(active?"Activate":"Deactivate","AlertRule",id,correlation);
    }
    @Transactional public void deleteRule(UUID id,String correlation) { store.rule(id);store.jdbc().update("DELETE FROM alert_rules WHERE id=?",id);audit("Delete","AlertRule",id,correlation); }
    @Transactional public void transition(UUID id,boolean close,String correlation) {
        Jwt jwt=(Jwt)SecurityContextHolder.getContext().getAuthentication().getPrincipal();
        Alert result=Models.transition(store.alert(id),UUID.fromString(jwt.getSubject()),close,Instant.now());
        store.save(result);audit(close?"CloseAlert":"AttendAlert","Alert",id,correlation);notifications(result,close?"AlertClosed":"AlertAttended",correlation);
    }
    @Transactional public List<Alert> evaluate(Reading reading) {
        // Serialize retries by reading, then changes for one sensor, including multiple replicas.
        store.jdbc().queryForObject("SELECT pg_advisory_xact_lock(?)",Object.class,reading.readingId().getLeastSignificantBits());
        String fingerprint=fingerprint(reading);
        List<Map<String,Object>> previous=store.jdbc().queryForList("SELECT fingerprint,result::text FROM processed_readings WHERE reading_id=?",reading.readingId());
        if(!previous.isEmpty()) {
            if(!fingerprint.equals(previous.getFirst().get("fingerprint")))throw new Conflict("Reading ID reused with different data");
            return Arrays.asList(store.decode((String)previous.getFirst().get("result"),Alert[].class));
        }
        if(!delivery.sensorActive(reading.sensorId(),reading.correlationId()))return List.of();
        store.jdbc().queryForObject("SELECT pg_advisory_xact_lock(?)",Object.class,reading.sensorId().getMostSignificantBits());
        List<Alert> affected=new ArrayList<>();
        for(Rule rule:Models.assess(store.rules(),reading.sensorType(),reading.value())) {
            List<Alert> existing=store.jdbc().query("SELECT payload::text FROM alerts WHERE payload->>'sensorId'=? AND payload->>'alertType'=? AND payload->>'isActive'='true' FOR UPDATE",(r,n)->store.decode(r.getString(1),Alert.class),reading.sensorId().toString(),rule.riskType().name());
            if(existing.size()>1)throw new Conflict("Multiple active alerts require reconciliation");
            Alert prior=existing.isEmpty()?null:existing.getFirst();
            BigDecimal threshold=rule.minimumValue()!=null&&reading.value().compareTo(rule.minimumValue())<0?rule.minimumValue():rule.maximumValue();
            Alert alert=new Alert(prior==null?UUID.randomUUID():prior.id(),reading.sensorId(),reading.communityId(),rule.riskType(),rule.alertLevel(),rule.name(),rule.message(),reading.value(),threshold,reading.recordedAt(),true,null,
                prior==null?Status.Active:prior.status(),prior==null?null:prior.attendedByUserId(),prior==null?null:prior.attendedAt(),null,null,rule.id(),rule.name(),rule.minimumValue(),rule.maximumValue());
            store.save(alert);affected.add(alert);audit("EvaluateAlert","Alert",alert.id(),reading.correlationId());notifications(alert,"AlertGenerated",reading.correlationId());
        }
        store.jdbc().update("INSERT INTO processed_readings(reading_id,fingerprint,result) VALUES(?,?,?::jsonb)",reading.readingId(),fingerprint,store.encode(affected));
        return affected;
    }
    private String fingerprint(Reading r) {
        String canonical=r.sensorId()+"|"+r.communityId()+"|"+r.sensorType()+"|"+r.value().stripTrailingZeros().toPlainString()+"|"+r.unit()+"|"+r.recordedAt();
        try{return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(canonical.getBytes(StandardCharsets.UTF_8)));}
        catch(java.security.NoSuchAlgorithmException e){throw new IllegalStateException(e);}
    }
    private void audit(String action,String resource,UUID id,String correlation) {
        Object principal=SecurityContextHolder.getContext().getAuthentication()==null?null:SecurityContextHolder.getContext().getAuthentication().getPrincipal();
        Jwt jwt=principal instanceof Jwt value?value:null;
        Map<String,Object> event=new LinkedHashMap<>();event.put("SchemaVersion",1);event.put("EventId",UUID.randomUUID());event.put("OccurredAt",Instant.now());event.put("CorrelationId",correlation);
        event.put("UserId",jwt==null?"ffffffff-ffff-ffff-ffff-ffffffffffff":jwt.getSubject());event.put("UserName",jwt==null?"system":jwt.getClaimAsString("unique_name"));
        event.put("Action",action);event.put("Resource",resource);event.put("ResourceId",id);event.put("Description",action+" "+resource+".");event.put("IpAddress",null);
        store.queue("AUDIT",event,correlation);
    }
    private void notifications(Alert a,String eventName,String correlation) {
        Map<String,Object> event=new LinkedHashMap<>();event.put("eventId",a.id());event.put("alertId",a.id());event.put("sensorId",a.sensorId());event.put("communityId",a.communityId());
        event.put("riskType",a.alertType());event.put("alertLevel",a.level());event.put("description",a.description());event.put("occurredAt",a.generatedAt());event.put("resolvedAt",a.resolvedAt());
        event.put("value",a.sensorValue());event.put("status",a.status());event.put("responsibleUserId",a.closedByUserId()!=null?a.closedByUserId():a.attendedByUserId());
        store.queue("EVENT",event,correlation);store.queue("REALTIME",Map.of("eventName",eventName,"payload",a),correlation);
    }
}
