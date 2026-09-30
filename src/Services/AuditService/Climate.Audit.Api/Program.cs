using System.Text;
using Climate.Audit.Api;
using Climate.Contracts.Audit;
using Climate.Audit.Api.Configuration;
using Climate.Audit.Api.Errors;
using Climate.Audit.Application;
using Climate.Audit.Infrastructure;
using Climate.Audit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
WebApplicationBuilder builder=WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(RabbitMqAuditOptions.FromConfiguration(builder.Configuration));
if (!builder.Configuration.GetValue<bool>("OpenApi:ExportOnly")) builder.Services.AddHostedService<AuditConsumer>();
builder.Services.AddControllers(); builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAuditApplication(); builder.Services.AddAuditInfrastructure(builder.Configuration);
JwtOptions jwt=builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()??throw new InvalidOperationException("JWT configuration is required.");
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.SectionName)).Validate(x=>!string.IsNullOrWhiteSpace(x.Issuer)&&!string.IsNullOrWhiteSpace(x.Audience)&&x.SigningKey.Length>=32,"Valid JWT configuration is required.").ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(x=>{x.MapInboundClaims=false;x.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer=jwt.Issuer,ValidateAudience=true,ValidAudience=jwt.Audience,ValidateIssuerSigningKey=true,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),ValidateLifetime=true,RoleClaimType="http://schemas.microsoft.com/ws/2008/06/identity/claims/role"};});
builder.Services.AddAuthorization(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen(x=>
{ x.SwaggerDoc("v1",new OpenApiInfo{Title="Climate Audit API",Version="v1"});
  x.AddSecurityDefinition("Bearer",new OpenApiSecurityScheme{Type=SecuritySchemeType.Http,Scheme="bearer",BearerFormat="JWT",Description="Administrator JWT returned by /api/auth/login."}); });
WebApplication app=builder.Build(); app.UseExceptionHandler(); if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();} app.UseAuthentication();app.UseAuthorization();app.MapControllers();app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health");
if (!builder.Configuration.GetValue<bool>("OpenApi:ExportOnly"))
    await Climate.Contracts.DatabaseStartup.RunAsync(app.Services.InitializeAuditDatabaseAsync, app.Logger, app.Lifetime.ApplicationStopping); await app.RunAsync(); public partial class Program;
