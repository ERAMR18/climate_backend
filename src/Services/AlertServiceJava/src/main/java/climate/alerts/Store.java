package climate.alerts;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.*;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;
import static climate.alerts.Models.*;

@Repository
public class Store {
    private final JdbcTemplate jdbc; private final ObjectMapper json;
    public Store(JdbcTemplate jdbc, ObjectMapper json) { this.jdbc=jdbc; this.json=json; }
    public JdbcTemplate jdbc() { return jdbc; }
    public ObjectMapper mapper() { return json; }
    public String encode(Object value) { try { return json.writeValueAsString(value); } catch(JsonProcessingException e) { throw new IllegalStateException("JSON serialization failed",e); } }
    public <T> T decode(String value,Class<T> type) { try { return json.readValue(value,type); } catch(JsonProcessingException e) { throw new IllegalStateException("Stored JSON invalid",e); } }
    List<Rule> rules() { return jdbc.query("SELECT payload::text FROM alert_rules ORDER BY payload->>'name'",(r,n)->decode(r.getString(1),Rule.class)); }
    Rule rule(UUID id) { return jdbc.query("SELECT payload::text FROM alert_rules WHERE id=?",(r,n)->decode(r.getString(1),Rule.class),id).stream().findFirst().orElseThrow(Missing::new); }
    Alert alert(UUID id) { return jdbc.query("SELECT payload::text FROM alerts WHERE id=? FOR UPDATE",(r,n)->decode(r.getString(1),Alert.class),id).stream().findFirst().orElseThrow(Missing::new); }
    void save(Rule rule) { jdbc.update("INSERT INTO alert_rules(id,payload) VALUES(?,?::jsonb) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload",rule.id(),encode(rule)); }
    void save(Alert alert) { jdbc.update("INSERT INTO alerts(id,payload) VALUES(?,?::jsonb) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload",alert.id(),encode(alert)); }
    void queue(String kind,Object payload,String correlation) { jdbc.update("INSERT INTO delivery_outbox(id,kind,payload,correlation_id) VALUES(?,?,?::jsonb,?)",UUID.randomUUID(),kind,encode(payload),correlation); }
    List<Alert> list(Map<String,String> filters,Integer page,Integer size) {
        StringBuilder query=new StringBuilder("SELECT payload::text FROM alerts WHERE 1=1"); List<Object> values=new ArrayList<>();
        Map<String,String> fields=Map.of("riskType","alertType","alertLevel","level","sensorId","sensorId","communityId","communityId","isActive","isActive","status","status");
        fields.forEach((parameter,field)-> { String value=filters.get(parameter); if(value!=null) { query.append(" AND payload->>'").append(field).append("'=?"); values.add(value); } });
        for(String date:List.of("from","to")) if(filters.get(date)!=null) { query.append(" AND (payload->>'generatedAt')::timestamptz ").append(date.equals("from")?">=":"<=").append(" ?::timestamptz"); values.add(filters.get(date)); }
        query.append(" ORDER BY (payload->>'generatedAt')::timestamptz DESC,id");
        if(page!=null) { int limit=size==null?250:size; if(page<1||limit<1||limit>1000) throw new IllegalArgumentException("Invalid pagination"); query.append(" LIMIT ? OFFSET ?"); values.add(limit); values.add(Math.multiplyExact((long)page-1,limit)); }
        return jdbc.query(query.toString(),(r,n)->decode(r.getString(1),Alert.class),values.toArray());
    }
}
