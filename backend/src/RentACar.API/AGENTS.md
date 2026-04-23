# RentACar.API Knowledge Base

**Scope:** ASP.NET Core API layer — controllers, application services, auth, middleware, contracts
**Parent:** `backend/src/`

## Where to Look
| Task | Location | Notes |
|------|----------|-------|
| Add controller | `Controllers/` | 22 controllers, domain-organized (Fleet, Auth, Pricing, Reservations, etc.) |
| Add application service | `Services/` | 20 services — **non-standard placement** (should be in Infrastructure or Application layer) |
| Add request/response DTO | `Contracts/{Domain}/` | Per-domain folders: Fleet, Auth, Pricing, Reservations |
| Add auth logic | `Authentication/` | JWT handling, claims, policies |
| Add middleware | `Middleware/` | 5 middleware components |
| Configure services | `Configuration/ServiceCollectionExtensions.cs` | `AddApiApplicationServices()` extension method |
| Add API options | `Options/` | Strongly-typed config classes |
| Startup entry | `Program.cs` | Delegates to `AddApiApplicationServices()` + `InitializeApiAsync()` |

## Non-Standard Patterns
- **Services in API layer**: 20 application services (ReservationService, FleetService, PaymentService, etc.) live in `RentACar.API/Services/` instead of a separate Application project or `RentACar.Infrastructure/Services/`. This deviates from strict Clean Architecture.
- **Contracts in API**: Request/response DTOs are co-located with controllers rather than in a shared contracts assembly.
- **No Startup.cs**: Uses modern minimal hosting (`WebApplication.CreateBuilder`) with extension methods instead of classic `Startup` class.

## Entry Points
- **`Program.cs`** — Minimal API host. Calls `builder.Services.AddApiApplicationServices()` and `app.InitializeApiAsync()` (runs pending EF migrations at startup).
- **`Configuration/ServiceCollectionExtensions.cs`** — Central service registration: auth, Swagger, CORS, rate limiting, EF Core, Redis, etc.

## Auth & Security
- JWT Bearer authentication with cookie fallback
- `CookieSecurePolicy.Always` for HTTPS environments
- Custom authorization policies in `Authentication/`
- Rate limiting middleware configured

## Testing
- Controller tests live in `backend/tests/RentACar.Tests/Unit/Controllers/`
- Integration tests cover auth + middleware pipelines

## Notes
- `appsettings.json` + `appsettings.Development.json` for environment config
- Swagger/OpenAPI enabled in development
- CORS configured for frontend origin
- Docker exposes port 8080
