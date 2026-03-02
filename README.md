# Arac Kiralama Platformu

Bu repository iki ana bolum icerir:

- `Admin_Dashboard/`: Next.js tabanli dashboard arayuzu
- `backend/`: .NET 10 backend (API, Worker, Core, Infrastructure)

## Dokumanlar

Uygulama plani ve takip:

- `docs/09_Implementation_Plan.md`
- `docs/10_Execution_Tracking.md`

## Backend Kurulum

```bash
cd backend
dotnet restore RentACar.sln
dotnet build RentACar.sln
dotnet run --project src/RentACar.API
```

## Docker ile Calistirma

```bash
cd backend
docker compose up --build
```
