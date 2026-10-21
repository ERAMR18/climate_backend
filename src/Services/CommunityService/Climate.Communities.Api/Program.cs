using System.Text;
using Climate.Communities.Api;
using Climate.Communities.Api.Persistence;
using Climate.Contracts.Audit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders(); builder.Logging.AddJsonConsole();
string connection = builder.Configuration.GetConnectionString("CommunityDb") ?? throw new InvalidOperationException("CommunityDb is required.");
string key = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT key is required.");
string internalKey = builder.Configuration["InternalApi:ApiKey"] ?? throw new InvalidOperationException("Internal API key is required.");
if (Encoding.UTF8.GetByteCount(key) < 32 || internalKey.Length < 32) throw new InvalidOperationException("Keys must contain at least 32 bytes.");
builder.Services.AddDbContext<CommunityDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddAuditOutbox<CommunityDbContext>(builder.Configuration);
builder.Services.AddProblemDetails(); builder.Services.AddControllers(); builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<SensorCountsClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SensorService:BaseUrl"] ?? throw new InvalidOperationException("SensorService URL required."));
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ClimateMonitoring.Identity",
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ClimateMonitoring.Client",
        ValidateLifetime = true, ValidateIssuerSigningKey = true, RequireExpirationTime = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", NameClaimType = "unique_name", ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization(); builder.Services.AddHealthChecks();
WebApplication app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    int status = exception is HttpRequestException or TaskCanceledException ? 503
        : exception is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } ? 409 : 500;
    await Results.Problem(statusCode: status, title: status == 409 ? "Community name already exists." : status == 503 ? "Dependency unavailable." : "Request failed.").ExecuteAsync(context);
}));
app.Use(async (context, next) =>
{
    string incoming = context.Request.Headers["X-Correlation-ID"].ToString();
    context.TraceIdentifier = incoming.Length is > 0 and <= 128 && incoming.All(x => char.IsAsciiLetterOrDigit(x) || x is '.' or '-' or '_') ? incoming : Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    await next(context);
});
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapGet("/health/ready", async (CommunityDbContext db, CancellationToken token) =>
    await db.Database.CanConnectAsync(token) ? Results.Ok(new { status = "Healthy" }) : Results.StatusCode(503));
app.MapGet("/health", async (CommunityDbContext db, CancellationToken token) =>
    await db.Database.CanConnectAsync(token) ? Results.Ok(new { status = "Healthy" }) : Results.StatusCode(503));
await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<CommunityDbContext>().InitializeAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();
public partial class Program;
