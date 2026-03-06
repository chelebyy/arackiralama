# RentACar Backend

Faz 1 foundation backend iskeleti.

## Projeler

- `src/RentACar.Core`: Domain entity ve interface katmani
- `src/RentACar.Infrastructure`: DbContext, migrations ve altyapi bagimliliklari
- `src/RentACar.API`: REST API, middleware pipeline ve startup migration akisi
- `src/RentACar.Worker`: Background worker host

## Hizli Baslangic

```bash
dotnet restore RentACar.sln
dotnet build RentACar.sln
dotnet run --project src/RentACar.API
```

API startup'ta bekleyen migration'lari otomatik uygular. Gerekirse asagidaki ayarla kapatilabilir:

```bash
Database__AutoMigrateOnStartup=false
```

## Endpointler

- `GET http://localhost:5008/health` (`dotnet run` ile health check)
- `GET http://localhost:5008/api/v1/health` (controller tabanli health endpoint)
- `GET http://localhost:5008/openapi/v1.json` (Development ortaminda OpenAPI spec)
- `GET http://localhost:5000/health` (docker ile API)

## Test

```bash
dotnet test tests/RentACar.Tests/RentACar.Tests.csproj
```

## Docker Local Development

```bash
docker compose up --build
```

Servisler:

- API: `http://localhost:5000`
- PostgreSQL: `localhost:5433`
- Redis: `localhost:6379`
