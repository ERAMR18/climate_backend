# Fase 4 — Identity Service

## 1. Objetivo

Identity Service implementa autenticación JWT y administración de usuarios sin compartir su base de datos con otros microservicios. Las contraseñas se almacenan mediante el `PasswordHasher<TUser>` mantenido por ASP.NET Core Identity y nunca se incluyen en respuestas o logs.

## 2. Estructura implementada

```text
src/Services/IdentityService/
├── Climate.Identity.Api/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   └── UsersController.cs
│   ├── Errors/
│   │   ├── GlobalExceptionHandler.cs
│   │   └── ResultExtensions.cs
│   ├── Program.cs
│   └── appsettings.json
├── Climate.Identity.Application/
│   ├── Abstractions/
│   │   ├── IJwtTokenGenerator.cs
│   │   ├── IPasswordService.cs
│   │   └── IUserRepository.cs
│   ├── Users/
│   │   ├── DTO y requests
│   │   ├── validadores FluentValidation
│   │   ├── IUserService.cs
│   │   ├── UserErrors.cs
│   │   └── UserService.cs
│   └── DependencyInjection.cs
├── Climate.Identity.Domain/
│   └── Users/User.cs
└── Climate.Identity.Infrastructure/
    ├── Persistence/
    │   ├── IdentityDbContext.cs
    │   ├── UserConfiguration.cs
    │   ├── UserRepository.cs
    │   └── Migrations/
    ├── Security/
    │   ├── JwtOptions.cs
    │   ├── JwtTokenGenerator.cs
    │   └── PasswordService.cs
    ├── Seeding/
    │   ├── AdminSeedOptions.cs
    │   └── IdentityDatabaseInitializer.cs
    └── DependencyInjection.cs
```

## 3. Modelo de usuario

`User` almacena:

- `Id`
- `Username` y `NormalizedUsername`
- `Email` y `NormalizedEmail`
- `PasswordHash`
- `Role`
- `IsActive`
- `CreatedAt`
- `UpdatedAt`

Los campos normalizados permiten búsquedas y restricciones únicas predecibles sin depender de la cultura del proceso. Las fechas utilizan `DateTimeOffset` y se producen mediante `TimeProvider.GetUtcNow()`.

## 4. Casos de uso

`UserService` implementa:

- registro con rol inicial `Viewer`;
- inicio de sesión mediante usuario o correo;
- rechazo uniforme de credenciales inválidas;
- rechazo de cuentas inactivas;
- rehash transparente cuando el algoritmo configurado lo recomienda;
- consulta del usuario autenticado;
- listado y consulta administrativa;
- modificación de usuario y rol;
- activación o desactivación.

Los controllers sólo traducen HTTP a casos de uso. No acceden a EF Core ni contienen reglas.

## 5. Endpoints

| Método | Ruta | Acceso | Resultado principal |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Público | `201 Created` |
| POST | `/api/v1/auth/login` | Público | JWT y usuario |
| GET | `/api/v1/users/me` | Autenticado | Usuario actual |
| GET | `/api/v1/users` | Administrator | Lista de usuarios |
| GET | `/api/v1/users/{id}` | Administrator | Usuario por ID |
| PUT | `/api/v1/users/{id}` | Administrator | Usuario actualizado |
| PATCH | `/api/v1/users/{id}/status` | Administrator | `204 No Content` |
| GET | `/health` | Público | Estado de aplicación y `IdentityDb` |

El registro público no acepta rol, por lo que no permite elevar privilegios. La administración de roles queda restringida por la política `AdministratorsOnly`.

## 6. Validación y errores

FluentValidation comprueba nombres, correo, contraseña y roles. Una contraseña registrada debe:

- tener entre 12 y 128 caracteres;
- contener mayúscula;
- contener minúscula;
- contener número.

Los fallos esperados se convierten a Problem Details:

```json
{
  "title": "auth.invalid_credentials",
  "status": 401,
  "detail": "The supplied credentials are invalid.",
  "traceId": "..."
}
```

Validación usa `422`, ausencia `404`, duplicados `409`, autenticación `401` y autorización `403`. El manejador global registra excepciones inesperadas y responde `500` sin exponer el stack trace.

## 7. JWT

El token incluye:

- identificador de usuario (`sub` y name identifier);
- nombre;
- correo;
- rol;
- identificador único del token (`jti`);
- emisor, audiencia y expiración.

Se firma con HMAC SHA-256. La clave se valida al inicio y debe tener al menos 32 caracteres. Existe un margen de reloj de 30 segundos.

## 8. Persistencia y migración

`IdentityDbContext` sólo administra `IdentityDb`. `UserConfiguration` define longitudes, índices únicos normalizados e índice por estado/rol.

La migración inicial está en:

```text
Climate.Identity.Infrastructure/Persistence/Migrations/InitialIdentity
```

La herramienta EF queda fijada localmente en `dotnet-tools.json`:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/Services/IdentityService/Climate.Identity.Infrastructure `
  --startup-project src/Services/IdentityService/Climate.Identity.Api
```

Al iniciar el servicio también se ejecutan migraciones pendientes. En despliegues futuros con múltiples réplicas conviene mover esta operación a un job de despliegue único.

## 9. Administrador inicial

El seed se ejecuta únicamente cuando la base no contiene usuarios y las tres variables están configuradas:

```text
AdminSeed__Username
AdminSeed__Email
AdminSeed__Password
```

La contraseña no existe en el código ni en `appsettings.json`. `.env.example` sólo contiene marcadores que deben sustituirse.

## 10. Paquetes agregados

| Paquete | Proyecto | Motivo |
|---|---|---|
| `FluentValidation.DependencyInjectionExtensions 12.1.1` | Application | validadores y registro por DI |
| `Microsoft.EntityFrameworkCore.SqlServer 10.0.10` | Infrastructure | persistencia SQL Server |
| `Microsoft.EntityFrameworkCore.Design 10.0.10` | Infrastructure | migraciones |
| `Microsoft.Extensions.Identity.Core 10.0.10` | Infrastructure | hashing mantenido y versionable |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.0.10` | Infrastructure | health check de `IdentityDb` |
| `System.IdentityModel.Tokens.Jwt 8.19.2` | Infrastructure | creación del JWT |
| `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.10` | Api | validación Bearer |
| `Swashbuckle.AspNetCore 10.2.3` | Api | Swagger y UI |
| `Microsoft.EntityFrameworkCore.InMemory 10.0.10` | Tests | soporte para pruebas de persistencia posteriores |
| `Moq 4.20.72` | Tests | dobles de repositorio y seguridad |

## 11. Ejecución local

SQL Server debe estar accesible y las variables deben configurarse antes de iniciar:

```powershell
$env:ConnectionStrings__IdentityDb='Server=localhost,1433;Database=IdentityDb;User Id=sa;Password=REPLACE_ME;TrustServerCertificate=True'
$env:Jwt__SigningKey='REPLACE_WITH_AT_LEAST_32_RANDOM_CHARACTERS'
$env:AdminSeed__Username='admin'
$env:AdminSeed__Email='admin@example.com'
$env:AdminSeed__Password='REPLACE_WITH_A_STRONG_PASSWORD'

dotnet run --project src/Services/IdentityService/Climate.Identity.Api
```

Swagger estará disponible en `/swagger` cuando `ASPNETCORE_ENVIRONMENT=Development`.

## 12. Comprobación

```powershell
dotnet build ClimateMonitoringSystem.sln --no-restore
dotnet test ClimateMonitoringSystem.sln --no-build --no-restore
```

Las pruebas de Identity cubren login correcto, contraseña incorrecta, usuario inactivo, registro, hashing, rol inicial, correo duplicado, cambio de estado, normalización y validadores.

## 13. Límite de la fase

El envío de `Login` y `UpdateUser` a Audit Service se conectará cuando exista la API de auditoría en la Fase 9. No se ha simulado una escritura a una bitácora inexistente ni se ha introducido acceso directo a `AuditDb`.

La siguiente etapa es la **Fase 5 — Sensor Service** y debe comenzar sólo cuando se solicite.
