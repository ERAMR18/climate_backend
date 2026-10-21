package climate.alerts;

import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.validation.Validation;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import static climate.alerts.Models.*;

class DomainTests {
    private Rule rule(Level level,RiskType risk,BigDecimal min,BigDecimal max,boolean active) {
        return new Rule(UUID.randomUUID(),"Rule",SensorType.Temperature,min,max,level,risk,"Risk",active,Instant.now(),Instant.now());
    }
    @Test void highestViolatedRulePerPhenomenonWinsAndBoundsAreExclusive() {
        Rule yellow=rule(Level.Yellow,RiskType.ForestFire,null,new BigDecimal("30"),true);
        Rule red=rule(Level.Red,RiskType.ForestFire,null,new BigDecimal("40"),true);
        Rule frost=rule(Level.Red,RiskType.Frost,new BigDecimal("0"),null,true);
        assertEquals(List.of(yellow),Models.assess(List.of(red,yellow,frost),SensorType.Temperature,new BigDecimal("40")));
        assertEquals(List.of(red),Models.assess(List.of(red,yellow,frost),SensorType.Temperature,new BigDecimal("41")));
        assertEquals(List.of(frost),Models.assess(List.of(red,yellow,frost),SensorType.Temperature,new BigDecimal("-1")));
        assertTrue(Models.assess(List.of(red,yellow,frost),SensorType.Temperature,BigDecimal.ZERO).isEmpty());
    }
    @Test void inactiveGreenAndWrongSensorRulesDoNotTriggerAlerts() {
        List<Rule> rules=List.of(rule(Level.Green,RiskType.Storm,null,BigDecimal.ONE,true),rule(Level.Red,RiskType.Storm,null,BigDecimal.ONE,false));
        assertTrue(Models.assess(rules,SensorType.Temperature,BigDecimal.TEN).isEmpty());
        assertTrue(Models.assess(List.of(rule(Level.Red,RiskType.Storm,null,BigDecimal.ONE,true)),SensorType.Humidity,BigDecimal.TEN).isEmpty());
    }
    @Test void transitionsPreserveResponsibleUsersAndRejectSkippingOrRepeatingStates() {
        UUID id=UUID.randomUUID(),user=UUID.randomUUID();Instant now=Instant.now();
        Alert active=new Alert(id,UUID.randomUUID(),UUID.randomUUID(),RiskType.Flood,Level.Red,"Title","Message",BigDecimal.TEN,BigDecimal.ONE,now,true,null,
            Status.Active,null,null,null,null,UUID.randomUUID(),"Rule",null,BigDecimal.ONE);
        assertThrows(Conflict.class,()->Models.transition(active,user,true,now));
        Alert attended=Models.transition(active,user,false,now);
        assertEquals(user,attended.attendedByUserId());assertEquals(Status.Attended,attended.status());assertTrue(attended.isActive());
        assertThrows(Conflict.class,()->Models.transition(attended,user,false,now));
        Alert closed=Models.transition(attended,user,true,now.plusSeconds(1));
        assertEquals(Status.Closed,closed.status());assertFalse(closed.isActive());assertEquals(user,closed.closedByUserId());assertEquals(user,closed.attendedByUserId());
        assertThrows(Conflict.class,()->Models.transition(closed,user,true,now));
    }
    @Test void ruleValidationRejectsMissingBoundsAndReversedIntervals() {
        try(var factory=Validation.buildDefaultValidatorFactory()) {
            var validator=factory.getValidator();
            assertFalse(validator.validate(new RuleInput("Rule",SensorType.Temperature,null,null,Level.Red,RiskType.Frost,"Message",true)).isEmpty());
            assertFalse(validator.validate(new RuleInput("Rule",SensorType.Temperature,BigDecimal.TEN,BigDecimal.ONE,Level.Red,RiskType.Frost,"Message",true)).isEmpty());
            assertTrue(validator.validate(new RuleInput("Rule",SensorType.Temperature,BigDecimal.ONE,BigDecimal.TEN,Level.Red,RiskType.Frost,"Message",true)).isEmpty());
        }
    }
    @Test void numericDotnetSensorTypesAndStringAngularSensorTypesAreBothAccepted() throws Exception {
        ObjectMapper mapper=new ObjectMapper();
        assertEquals(SensorType.Temperature,mapper.readValue("0",SensorType.class));
        assertEquals(SensorType.Smoke,mapper.readValue("7",SensorType.class));
        assertEquals(SensorType.RiverLevel,mapper.readValue("\"RiverLevel\"",SensorType.class));
        assertThrows(Exception.class,()->mapper.readValue("99",SensorType.class));
    }
}
