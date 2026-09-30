using System.Text;
using System.Text.Json.Serialization;
using Climate.Contracts.Identity;
using Climate.Contracts.Audit;
using Climate.Monitoring.Api.Configuration;
using Climate.Monitoring.Api.Errors;
using Climate.Monitoring.Application;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Infrastructure;
using Climate.Monitoring.Infrastructure.Persistence;
using Climate.Monitoring.Api.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuditOutbox<MonitoringDbContext>(builder.Configuration);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IRealtimePublisher,SignalRRealtimePublisher>();
builder.Services.AddMonitoringApplication();
builder.Services.AddMonitoringInfrastructure(builder.Configuration);

JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
    .Validate(options => options.SigningKey.Length >= 32, "JWT signing key must contain at least 32 characters.")
    .ValidateOnStart();
builder.Services.AddOptions<InternalApiOptions>().Bind(builder.Configuration.GetSection(InternalApiOptions.SectionName))
    .Validate(x=>x.ApiKey.Length>=32,"Internal API key must contain at least 32 characters.").ValidateOnStart();

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
        NameClaimType = "unique_name",
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };
    options.Events=new JwtBearerEvents{OnMessageReceived=context=>{string? token=context.Request.Query["access_token"].FirstOrDefault();
        if(!string.IsNullOrEmpty(token)&&context.HttpContext.Request.Path.StartsWithSegments("/hubs/monitoring"))context.Token=token; return Task.CompletedTask;}};
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("OperateSimulation", policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Operator))
    .AddPolicy("ResetSystem", policy => policy.RequireRole(SystemRoles.Administrator));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Climate Monitoring API",
        Version = "v1",
        Description = "Sensor readings, history and climate simulation service."
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
app.MapHub<MonitoringHub>("/hubs/monitoring");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health");
if (!builder.Configuration.GetValue<bool>("OpenApi:ExportOnly"))
    await Climate.Contracts.DatabaseStartup.RunAsync(app.Services.InitializeMonitoringDatabaseAsync, app.Logger, app.Lifetime.ApplicationStopping);
await app.RunAsync();

public partial class Program;
