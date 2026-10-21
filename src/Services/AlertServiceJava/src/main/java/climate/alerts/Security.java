package climate.alerts;

import jakarta.servlet.*;
import jakarta.servlet.http.*;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.*;
import javax.crypto.spec.SecretKeySpec;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.*;
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.oauth2.core.*;
import org.springframework.security.oauth2.jose.jws.MacAlgorithm;
import org.springframework.security.oauth2.jwt.*;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationConverter;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.web.filter.OncePerRequestFilter;

@Configuration @EnableMethodSecurity
public class Security {
    @Bean JwtDecoder decoder(@Value("${JWT_SIGNING_KEY}") String secret,
        @Value("${JWT_ISSUER:ClimateMonitoring.Identity}") String issuer,
        @Value("${JWT_AUDIENCE:ClimateMonitoring.Client}") String audience) {
        if (secret.getBytes(StandardCharsets.UTF_8).length < 32) throw new IllegalArgumentException("JWT key must contain at least 32 bytes");
        NimbusJwtDecoder decoder = NimbusJwtDecoder.withSecretKey(new SecretKeySpec(secret.getBytes(StandardCharsets.UTF_8), "HmacSHA256"))
            .macAlgorithm(MacAlgorithm.HS256).build();
        decoder.setJwtValidator(new DelegatingOAuth2TokenValidator<>(new JwtTimestampValidator(Duration.ofSeconds(30)),
            new JwtIssuerValidator(issuer), jwt -> jwt.getAudience().contains(audience) && jwt.getExpiresAt() != null && jwt.getSubject() != null
                ? OAuth2TokenValidatorResult.success() : OAuth2TokenValidatorResult.failure(new OAuth2Error("invalid_token"))));
        return decoder;
    }
    @Bean SecurityFilterChain chain(HttpSecurity http) throws Exception {
        JwtAuthenticationConverter converter = new JwtAuthenticationConverter();
        converter.setJwtGrantedAuthoritiesConverter(jwt -> {
            String role = jwt.getClaimAsString("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
            return role != null && Set.of("Administrator","Operator","Viewer").contains(role)
                ? List.of(new SimpleGrantedAuthority("ROLE_"+role)) : List.of();
        });
        return http.csrf(csrf -> csrf.disable()).sessionManagement(session -> session.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
            .authorizeHttpRequests(auth -> auth.requestMatchers("/health/**", "/api/v1/internal/**").permitAll().anyRequest().authenticated())
            .oauth2ResourceServer(oauth -> oauth.jwt(jwt -> jwt.jwtAuthenticationConverter(converter))).build();
    }
    @Bean OncePerRequestFilter correlation() { return new OncePerRequestFilter() {
        @Override protected void doFilterInternal(HttpServletRequest request,HttpServletResponse response,FilterChain chain) throws ServletException,IOException {
            String id = request.getHeader("X-Correlation-ID");
            if (id == null || !id.matches("[a-zA-Z0-9._-]{1,128}")) id=UUID.randomUUID().toString();
            MDC.put("CorrelationId",id); request.setAttribute("correlationId",id); response.setHeader("X-Correlation-ID",id);
            try { chain.doFilter(request,response); } finally { MDC.remove("CorrelationId"); }
        }
    }; }
}
