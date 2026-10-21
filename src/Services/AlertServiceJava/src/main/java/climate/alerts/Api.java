package climate.alerts;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import java.time.Instant;
import java.util.*;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.*;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.*;
import static climate.alerts.Models.*;

@RestController
public class Api {
    private final Store store;private final DomainService domain;private final String internalKey;
    public Api(Store store,DomainService domain,@Value("${INTERNAL_API_KEY}")String internalKey){this.store=store;this.domain=domain;this.internalKey=internalKey;}
    private String correlation(HttpServletRequest request){return (String)request.getAttribute("correlationId");}
    @GetMapping("/api/v1/alert-rules") public List<Rule> rules(){return store.rules();}
    @GetMapping("/api/v1/alert-rules/{id}") public Rule rule(@PathVariable UUID id){return store.rule(id);}
    @PostMapping("/api/v1/alert-rules") @PreAuthorize("hasRole('Administrator')")
    public ResponseEntity<Rule> create(@Valid @RequestBody RuleInput input,HttpServletRequest request){Rule rule=domain.rule(null,input,correlation(request));return ResponseEntity.created(java.net.URI.create("/api/v1/alert-rules/"+rule.id())).body(rule);}
    @PutMapping("/api/v1/alert-rules/{id}") @PreAuthorize("hasRole('Administrator')")
    public Rule update(@PathVariable UUID id,@Valid @RequestBody RuleInput input,HttpServletRequest request){return domain.rule(id,input,correlation(request));}
    @PatchMapping("/api/v1/alert-rules/{id}/{action:activate|deactivate}") @PreAuthorize("hasRole('Administrator')")
    public ResponseEntity<Void> status(@PathVariable UUID id,@PathVariable String action,HttpServletRequest request){domain.ruleStatus(id,action.equals("activate"),correlation(request));return ResponseEntity.noContent().build();}
    @DeleteMapping("/api/v1/alert-rules/{id}") @PreAuthorize("hasRole('Administrator')")
    public ResponseEntity<Void> delete(@PathVariable UUID id,HttpServletRequest request){domain.deleteRule(id,correlation(request));return ResponseEntity.noContent().build();}
    @GetMapping("/api/v1/alerts") public List<Alert> alerts(@RequestParam Map<String,String> filters,@RequestParam(required=false)Integer page,@RequestParam(required=false)Integer pageSize){
        if(filters.get("riskType")!=null)RiskType.valueOf(filters.get("riskType"));
        if(filters.get("alertLevel")!=null)Level.valueOf(filters.get("alertLevel"));
        if(filters.get("status")!=null)Status.valueOf(filters.get("status"));
        for(String id:List.of("sensorId","communityId"))if(filters.get(id)!=null)UUID.fromString(filters.get(id));
        if(filters.get("isActive")!=null&&!List.of("true","false").contains(filters.get("isActive")))throw new IllegalArgumentException("Invalid boolean");
        Instant from=filters.get("from")==null?null:Instant.parse(filters.get("from"));Instant to=filters.get("to")==null?null:Instant.parse(filters.get("to"));
        if(from!=null&&to!=null&&from.isAfter(to))throw new IllegalArgumentException("Invalid date range");return store.list(filters,page,pageSize);}
    @GetMapping("/api/v1/alerts/{id}") public Alert alert(@PathVariable UUID id){return store.alert(id);}
    @PatchMapping("/api/v1/alerts/{id}/{action:attend|close|resolve}") @PreAuthorize("hasAnyRole('Administrator','Operator')")
    public ResponseEntity<Void> transition(@PathVariable UUID id,@PathVariable String action,HttpServletRequest request){domain.transition(id,!action.equals("attend"),correlation(request));return ResponseEntity.noContent().build();}
    @PostMapping("/api/v1/internal/risk-evaluations") public ResponseEntity<List<Alert>> evaluate(@Valid @RequestBody Reading reading,HttpServletRequest request){
        if(!internalKey.equals(request.getHeader("X-Internal-Api-Key")))return ResponseEntity.status(401).build();return ResponseEntity.ok(domain.evaluate(reading));}
}

@RestControllerAdvice
class Problems {
    @ExceptionHandler(Missing.class) ResponseEntity<ProblemDetail> missing(){return problem(404,"Requested resource does not exist.");}
    @ExceptionHandler(Conflict.class) ResponseEntity<ProblemDetail> conflict(Conflict error){return problem(409,error.getMessage());}
    @ExceptionHandler({MethodArgumentNotValidException.class,IllegalArgumentException.class,java.time.format.DateTimeParseException.class})
    ResponseEntity<ProblemDetail> invalid(Exception error){return problem(422,"Invalid fields, filters or date range.");}
    @ExceptionHandler(Delivery.DependencyUnavailable.class) ResponseEntity<ProblemDetail> dependency(){return problem(503,"Sensor service unavailable.");}
    @ExceptionHandler(org.springframework.http.converter.HttpMessageNotReadableException.class) ResponseEntity<ProblemDetail> json(){return problem(422,"Invalid JSON fields or enum values.");}
    @ExceptionHandler(org.springframework.dao.DataAccessException.class) ResponseEntity<ProblemDetail> database(){return problem(503,"Persistence temporarily unavailable.");}
    private ResponseEntity<ProblemDetail> problem(int status,String detail){return ResponseEntity.status(status).body(ProblemDetail.forStatusAndDetail(HttpStatusCode.valueOf(status),detail));}
}

@RestController
class Health {
    private final org.springframework.jdbc.core.JdbcTemplate jdbc;
    Health(org.springframework.jdbc.core.JdbcTemplate jdbc){this.jdbc=jdbc;}
    @GetMapping("/health/live") Map<String,String> live(){return Map.of("status","Healthy");}
    @GetMapping({"/health","/health/ready"}) Map<String,String> ready(){jdbc.queryForObject("SELECT 1",Integer.class);return Map.of("status","Healthy");}
}
