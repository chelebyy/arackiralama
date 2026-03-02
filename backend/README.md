# RentACar Backend

Faz 1 foundation backend iskeleti.

## Projeler

- `src/RentACar.Core`: Domain entity ve interface katmani
- `src/RentACar.Infrastructure`: DbContext ve altyapi bagimliliklari
- `src/RentACar.API`: REST API ve middleware pipeline
- `src/RentACar.Worker`: Background worker host

## Hızlı Başlangıç

```bash
dotnet restore RentACar.sln
dotnet build RentACar.sln
dotnet run --project src/RentACar.API
```

API health endpoint:

- `GET http://localhost:5000/health` (docker ile)
- `GET http://localhost:8080/health` (container icinden)
- `GET http://localhost:5135/api/v1/health` (dotnet run varsayilan profili)

## Docker Local Development

```bash
docker compose up --build
```

Servisler:

- API: `http://localhost:5000`
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`
