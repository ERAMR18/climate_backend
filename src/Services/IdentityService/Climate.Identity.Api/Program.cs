using System.Text;
using Climate.Contracts.Identity;
using Climate.Contracts.Audit;
using Climate.Identity.Api.Errors;
using Climate.Identity.Application;
using Climate.Identity.Infrastructure;
using Climate.Identity.Infrastructure.Security;
using Climate.Identity.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string identityAuditUrl=builder.Configuration["AuditService:BaseUrl"]??throw new InvalidOperationException("Audit Service base URL is required.");
string identityAuditKey=builder.Configuration["AuditService:ApiKey"]??throw new InvalidOperationException("Audit Service API key is required.");
builder.Services.AddSingleton(new AuditWriter(new HttpClient{BaseAddress=new Uri(identityAuditUrl)},identityAuditKey));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdministratorsOnly", policy => policy.RequireRole(SystemRoles.Administrator));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Climate Monitoring Identity API",
        Version = "v1",
        Description = "Authentication and user administration service."
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
app.MapHealthChecks("/health");

await app.Services.InitializeIdentityDatabaseAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();

public partial class Program;
