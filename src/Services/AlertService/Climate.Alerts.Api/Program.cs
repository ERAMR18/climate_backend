using Climate.Contracts.Audit;
using System.Text;
using System.Text.Json.Serialization;
using Climate.Alerts.Api.Configuration;
using Climate.Alerts.Api.Errors;
using Climate.Alerts.Application;
using Climate.Alerts.Infrastructure;
using Climate.Alerts.Infrastructure.Persistence;
using Climate.Contracts.Identity;
using Climate.Contracts.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuditOutbox<AlertsDbContext>(builder.Configuration);
string alertRealtimeUrl=builder.Configuration["RealtimeService:BaseUrl"]??throw new InvalidOperationException("Realtime Service base URL is required.");
string alertRealtimeKey=builder.Configuration["RealtimeService:ApiKey"]??throw new InvalidOperationException("Realtime Service API key is required.");
builder.Services.AddSingleton(new RealtimeWriter(new HttpClient{BaseAddress=new Uri(alertRealtimeUrl)},alertRealtimeKey));
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAlertsApplication();
builder.Services.AddAlertsInfrastructure(builder.Configuration);

JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(value => !string.IsNullOrWhiteSpace(value.Issuer), "JWT issuer is required.")
    .Validate(value => !string.IsNullOrWhiteSpace(value.Audience), "JWT audience is required.")
    .Validate(value => value.SigningKey.Length >= 32, "JWT signing key must contain at least 32 characters.")
    .ValidateOnStart();
builder.Services.AddOptions<InternalApiOptions>().Bind(builder.Configuration.GetSection(InternalApiOptions.SectionName))
    .Validate(value => value.ApiKey.Length >= 32, "Internal API key must contain at least 32 characters.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
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
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };
});
builder.Services.AddAuthorizationBuilder().AddPolicy(
    "ManageAlerts",
    policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Operator));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Climate Alerts API",
        Version = "v1",
        Description = "Configurable climate risk evaluation and alert lifecycle service."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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
    await Climate.Contracts.DatabaseStartup.RunAsync(app.Services.InitializeAlertsDatabaseAsync, app.Logger, app.Lifetime.ApplicationStopping);
await app.RunAsync();

public partial class Program;
