using System.Text;
using Climate.Sensors.Infrastructure.Persistence;
using System.Text.Json.Serialization;
using Climate.Contracts.Identity;
using Climate.Contracts.Audit;
using Climate.Contracts.Realtime;
using Climate.Sensors.Api.Configuration;
using Climate.Sensors.Api.Errors;
using Climate.Sensors.Application;
using Climate.Sensors.Infrastructure;
using Climate.Sensors.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuditOutbox<SensorsDbContext>(builder.Configuration);
string sensorRealtimeUrl=builder.Configuration["RealtimeService:BaseUrl"]??throw new InvalidOperationException("Realtime Service base URL is required.");
string sensorRealtimeKey=builder.Configuration["RealtimeService:ApiKey"]??throw new InvalidOperationException("Realtime Service API key is required.");
builder.Services.AddSingleton(new RealtimeWriter(new HttpClient{BaseAddress=new Uri(sensorRealtimeUrl)},sensorRealtimeKey));

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSensorsApplication();
builder.Services.AddSensorsInfrastructure(builder.Configuration);
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
    .Validate(options => options.SigningKey.Length >= 32, "JWT signing key must contain at least 32 characters.")
    .ValidateOnStart();
builder.Services.AddOptions<InternalApiOptions>()
    .Bind(builder.Configuration.GetSection(InternalApiOptions.SectionName))
    .Validate(options => options.ApiKey.Length >= 32, "Internal API key must contain at least 32 characters.")
    .ValidateOnStart();

JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(
        "ManageSensors",
        policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Operator));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Climate Monitoring Sensors API",
        Version = "v1",
        Description = "Community and simulated sensor catalog service."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT access token."
    });
});

WebApplication app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health");

if (!builder.Configuration.GetValue<bool>("OpenApi:ExportOnly"))
    await Climate.Contracts.DatabaseStartup.RunAsync(app.Services.InitializeSensorsDatabaseAsync, app.Logger, app.Lifetime.ApplicationStopping);
await app.RunAsync();

public partial class Program;
