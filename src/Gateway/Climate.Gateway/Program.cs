using System.Threading.RateLimiting;
using Climate.Gateway.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;

WebApplicationBuilder builder=WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
string[] allowedOrigins=builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()??[];
if(allowedOrigins.Length==0)throw new InvalidOperationException("At least one CORS origin is required.");

builder.Services.AddCors(options=>options.AddPolicy("Frontend",policy=>policy
    .WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options=>
{
    options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context=>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_
            =>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
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
app.MapHealthChecks("/health",new HealthCheckOptions{ResponseWriter=HealthResponseWriter.WriteAsync}).DisableRateLimiting();
app.MapReverseProxy();
await app.RunAsync();
public partial class Program;
