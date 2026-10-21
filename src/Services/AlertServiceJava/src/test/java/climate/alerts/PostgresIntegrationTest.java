package climate.alerts;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.nimbusds.jose.*;
import com.nimbusds.jose.crypto.MACSigner;
import com.nimbusds.jwt.*;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.junit.jupiter.api.Assertions.*;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;
import static climate.alerts.Models.*;

@SpringBootTest @AutoConfigureMockMvc
@EnabledIfEnvironmentVariable(named="ALERT_INTEGRATION",matches="true")
class PostgresIntegrationTest {
    @Autowired MockMvc http;
    @Autowired DomainService domain;
    @Autowired JdbcTemplate jdbc;
    @Autowired ObjectMapper json;
    // Only outbound HTTP/AMQP delivery is replaced here. Persistence, JWT and business transactions are real.
    @MockitoBean Delivery delivery;
    @BeforeEach void reset(){
        assertEquals("alerts_test",jdbc.queryForObject("SELECT current_database()",String.class));
        jdbc.update("DELETE FROM processed_readings");jdbc.update("DELETE FROM alerts");jdbc.update("DELETE FROM alert_rules");jdbc.update("DELETE FROM delivery_outbox");
        when(delivery.sensorActive(any(),any())).thenReturn(true);
    }
    private String token(String role,boolean expired,String issuer) throws Exception {
        JWTClaimsSet claims=new JWTClaimsSet.Builder().subject("11111111-1111-4111-8111-111111111111").issuer(issuer).audience("ClimateMonitoring.Client")
            .expirationTime(Date.from(Instant.now().plusSeconds(expired?-120:3600))).claim("unique_name","test")
            .claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role",role).build();
        SignedJWT jwt=new SignedJWT(new JWSHeader(JWSAlgorithm.HS256),claims);jwt.sign(new MACSigner(System.getenv("JWT_SIGNING_KEY")));return jwt.serialize();
    }
    @Test void jwtValidationAndAdministrativeRolesAreEnforced() throws Exception {
        String body="{\"name\":\"Hot\",\"sensorType\":\"Temperature\",\"maximumValue\":30,\"alertLevel\":\"Red\",\"riskType\":\"ForestFire\",\"message\":\"Hot weather\"}";
        http.perform(get("/api/v1/alerts")).andExpect(status().isUnauthorized());
        http.perform(get("/api/v1/alerts").header("Authorization","Bearer "+token("Administrator",true,"ClimateMonitoring.Identity"))).andExpect(status().isUnauthorized());
        http.perform(get("/api/v1/alerts").header("Authorization","Bearer "+token("Administrator",false,"wrong"))).andExpect(status().isUnauthorized());
        http.perform(post("/api/v1/alert-rules").header("Authorization","Bearer "+token("Viewer",false,"ClimateMonitoring.Identity")).contentType("application/json").content(body)).andExpect(status().isForbidden());
        http.perform(post("/api/v1/alert-rules").header("Authorization","Bearer "+token("Administrator",false,"ClimateMonitoring.Identity")).contentType("application/json").content(body)).andExpect(status().isCreated());
        assertEquals(1,jdbc.queryForObject("SELECT count(*) FROM alert_rules",Integer.class));assertEquals(1,jdbc.queryForObject("SELECT count(*) FROM delivery_outbox WHERE kind='AUDIT'",Integer.class));
        http.perform(get("/api/v1/alerts?from=2026-10-02T00:00:00Z&to=2026-10-01T00:00:00Z").header("Authorization","Bearer "+token("Viewer",false,"ClimateMonitoring.Identity"))).andExpect(status().isUnprocessableEntity());
    }
    @Test void readingRetriesAreIdempotentAndAlertTransitionsAreTransactional() throws Exception {
        domain.rule(null,new RuleInput("Hot",SensorType.Temperature,null,new BigDecimal("30"),Level.Red,RiskType.ForestFire,"Hot weather",true),"test");
        Reading reading=new Reading(UUID.randomUUID(),Instant.now(),"test",UUID.randomUUID(),UUID.randomUUID(),UUID.randomUUID(),SensorType.Temperature,new BigDecimal("40"),"°C",Instant.now());
        List<Alert> first=domain.evaluate(reading);assertEquals(1,first.size());
        int auditCount=jdbc.queryForObject("SELECT count(*) FROM delivery_outbox",Integer.class);
        assertEquals(first,domain.evaluate(reading));assertEquals(auditCount,jdbc.queryForObject("SELECT count(*) FROM delivery_outbox",Integer.class));
        assertEquals(1,jdbc.queryForObject("SELECT count(*) FROM alerts",Integer.class));
        String route="/api/v1/alerts/"+first.getFirst().id();String operator=token("Operator",false,"ClimateMonitoring.Identity");
        http.perform(patch(route+"/close").header("Authorization","Bearer "+operator)).andExpect(status().isConflict());
        http.perform(patch(route+"/attend").header("Authorization","Bearer "+operator)).andExpect(status().isNoContent());
        http.perform(patch(route+"/close").header("Authorization","Bearer "+operator)).andExpect(status().isNoContent());
        http.perform(patch(route+"/close").header("Authorization","Bearer "+operator)).andExpect(status().isConflict());
        String stored=jdbc.queryForObject("SELECT payload::text FROM alerts WHERE id=?",String.class,first.getFirst().id());
        Alert closed=json.readValue(stored,Alert.class);assertFalse(closed.isActive());assertEquals(Status.Closed,closed.status());assertNotNull(closed.closedByUserId());
        when(delivery.sensorActive(any(),any())).thenReturn(false);
        Reading inactive=new Reading(UUID.randomUUID(),Instant.now(),"test",UUID.randomUUID(),reading.sensorId(),reading.communityId(),SensorType.Temperature,new BigDecimal("50"),"°C",Instant.now());
        assertTrue(domain.evaluate(inactive).isEmpty());
    }
}
