using System.Threading.RateLimiting;
using System.Text;
using Climate.Gateway.Health;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder=WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
string signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ClimateMonitoring.Identity",
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ClimateMonitoring.Client",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateLifetime = true, RequireExpirationTime = true, ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "unique_name",
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/monitoring"))
                context.Token = context.Request.Query["access_token"];
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
string[] allowedOrigins=builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()??[];
if(allowedOrigins.Length==0)throw new InvalidOperationException("At least one CORS origin is required.");

builder.Services.AddCors(options=>options.AddPolicy("Frontend",policy=>policy
    .WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options=>
{
    options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context=>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_
            =>new FixedWindowRateLimiterOptions{PermitLimit=builder.Configuration.GetValue("RateLimit:PermitLimit",120),Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
});
builder.Services.Configure<ForwardedHeadersOptions>(options=>options.ForwardedHeaders=
    ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto|ForwardedHeaders.XForwardedHost);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHttpClient(nameof(DownstreamHealthCheck)).ConfigureHttpClient(client=>client.Timeout=TimeSpan.FromSeconds(3));
builder.Services.AddHealthChecks().AddCheck<DownstreamHealthCheck>("downstream-services",failureStatus:HealthStatus.Unhealthy);
builder.Services.AddProblemDetails();

WebApplication app=builder.Build();
app.UseExceptionHandler();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    string incoming = context.Request.Headers["X-Correlation-ID"].ToString();
    string correlationId = incoming.Length is > 0 and <= 128 && incoming.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
        ? incoming : Guid.NewGuid().ToString();
    context.TraceIdentifier = correlationId;
    context.Request.Headers["X-Correlation-ID"] = correlationId;
    context.Response.OnStarting(() =>
    {
        // YARP also copies downstream headers; replace after proxying to avoid duplicate values.
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        return Task.CompletedTask;
    });
    using IDisposable? scope = app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
    await next(context);
});
app.UseCors("Frontend");
app.UseStaticFiles();
app.UseSwaggerUI(options=>
{
    options.RoutePrefix="swagger";
    options.SwaggerEndpoint("/swagger/v1/swagger.json","Climate Monitoring System API v1");
    options.DocumentTitle="Climate Monitoring System API";
    options.DisplayRequestDuration();
    options.EnableDeepLinking();
    options.EnablePersistAuthorization();
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
// A failed optional consumer must not remove the entire gateway from service.
app.MapHealthChecks("/health/ready",new HealthCheckOptions{Predicate = _ => false}).DisableRateLimiting();
app.MapHealthChecks("/health",new HealthCheckOptions{ResponseWriter=HealthResponseWriter.WriteAsync}).DisableRateLimiting();
app.MapReverseProxy();
await app.RunAsync();
public partial class Program;
