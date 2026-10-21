package climate.alerts;

import java.net.URI;
import java.net.http.*;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.TimeUnit;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.core.*;
import org.springframework.amqp.core.Queue;
import org.springframework.amqp.rabbit.connection.CorrelationData;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

@Component
public class Delivery {
    private static final Logger log=LoggerFactory.getLogger(Delivery.class);
    private final Store store;private final RabbitTemplate rabbit;
    private final HttpClient http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(5)).build();
    private final String sensorUrl,eventUrl,realtimeUrl,key;
    public Delivery(Store store,RabbitTemplate rabbit,@Value("${SENSOR_SERVICE_URL}")String sensorUrl,
        @Value("${EVENT_SERVICE_URL}")String eventUrl,@Value("${REALTIME_SERVICE_URL}")String realtimeUrl,@Value("${INTERNAL_API_KEY}")String key){
        this.store=store;this.rabbit=rabbit;this.sensorUrl=sensorUrl;this.eventUrl=eventUrl;this.realtimeUrl=realtimeUrl;this.key=key;
        if(key.length()<32)throw new IllegalArgumentException("Internal API key must contain at least 32 characters");
    }
    @Bean Declarables topology(){return new Declarables(new DirectExchange("climate.audit",true,false),new DirectExchange("climate.audit.dead",true,false),
        new Queue("climate.audit.events.dlq",true),new Queue("climate.audit.events",true,false,false,Map.of("x-dead-letter-exchange","climate.audit.dead","x-dead-letter-routing-key","audit.created")),
        new Binding("climate.audit.events",Binding.DestinationType.QUEUE,"climate.audit","audit.created",null),new Binding("climate.audit.events.dlq",Binding.DestinationType.QUEUE,"climate.audit.dead","audit.created",null));}
    boolean sensorActive(UUID sensor,String correlation){
        try{HttpResponse<String> response=http.send(request(sensorUrl,"api/v1/internal/sensors/"+sensor+"/active",correlation).GET().build(),HttpResponse.BodyHandlers.ofString());
            if(response.statusCode()!=200)throw new IllegalStateException("Sensor API unavailable");return Boolean.TRUE.equals(store.mapper().readValue(response.body(),Boolean.class));
        }catch(Exception e){if(e instanceof InterruptedException)Thread.currentThread().interrupt();throw new DependencyUnavailable();}
    }
    private HttpRequest.Builder request(String base,String path,String correlation){return HttpRequest.newBuilder(URI.create(base.endsWith("/")?base+path:base+"/"+path))
        .timeout(Duration.ofSeconds(10)).header("X-Internal-Api-Key",key).header("X-Correlation-ID",correlation).header("Content-Type","application/json");}
    @Scheduled(fixedDelayString="${OUTBOX_INTERVAL_MS:5000}") @Transactional
    public void dispatch(){
        List<Map<String,Object>> rows=store.jdbc().queryForList("SELECT id,kind,payload::text,correlation_id FROM delivery_outbox WHERE published_at IS NULL ORDER BY created_at,id LIMIT 100 FOR UPDATE SKIP LOCKED");
        for(Map<String,Object> row:rows){
            try{
                String payload=(String)row.get("payload");String correlation=(String)row.get("correlation_id");
                if(row.get("kind").equals("AUDIT")){
                    CorrelationData confirmed=new CorrelationData(row.get("id").toString());
                    MessageProperties props=new MessageProperties();props.setContentType("application/json");props.setDeliveryMode(MessageDeliveryMode.PERSISTENT);
                    props.setMessageId(store.mapper().readTree(payload).get("EventId").asText());props.setCorrelationId(correlation);props.setType("AuditLogRequested");
                    rabbit.send("climate.audit","audit.created",new Message(payload.getBytes(StandardCharsets.UTF_8),props),confirmed);
                    if(!confirmed.getFuture().get(10,TimeUnit.SECONDS).isAck()||confirmed.getReturned()!=null)throw new IllegalStateException("Audit publication not confirmed");
                }else{
                    String base=row.get("kind").equals("EVENT")?eventUrl:realtimeUrl;String path=row.get("kind").equals("EVENT")?"api/v1/internal/events":"api/v1/internal/realtime";
                    HttpResponse<Void> response=http.send(request(base,path,correlation).POST(HttpRequest.BodyPublishers.ofString(payload)).build(),HttpResponse.BodyHandlers.discarding());
                    if(response.statusCode()<200||response.statusCode()>299)throw new IllegalStateException("Domain notification not accepted");
                }
                store.jdbc().update("UPDATE delivery_outbox SET published_at=now() WHERE id=?",row.get("id"));
            }catch(Exception error){if(error instanceof InterruptedException)Thread.currentThread().interrupt();log.warn("Unconfirmed {} delivery {} retained for retry",row.get("kind"),row.get("id"));}
        }
    }
    static class DependencyUnavailable extends RuntimeException {}
}
