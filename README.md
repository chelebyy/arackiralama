# Arac Kiralama Platformu

Bu repository iki ana uygulama alanini icerir:

- `frontend/`: Next.js tabanli yonetim arayuzu ve ortak UI kutuphanesi
- `backend/`: .NET 10 backend (API, Worker, Core, Infrastructure)

## Dokumanlar

- `docs/09_Implementation_Plan.md`
- `docs/10_Execution_Tracking.md`
- `docs/11_Private_Repo_Soft_Protection_Policy.md`

## Backend Kurulum

```bash
cd backend
dotnet restore RentACar.sln
dotnet build RentACar.sln
dotnet run --project src/RentACar.API
```

## Frontend Kurulum

```bash
cd frontend
corepack pnpm install
corepack pnpm test
corepack pnpm build
```

## Docker ile Calistirma

```bash
cd backend
docker compose up --build
```

## CI ve Registry Akisi

- Ana CI workflow: `.github/workflows/ci.yml`
- Backend build/test, frontend lint/test/build ve Docker image build ayni workflow icinde calisir.
- GHCR push adimi yalnizca `main` branch push'larinda `ci.yml` icindeki `docker-push` job'u ile calisir.

## Soft Main Koruma

Private repo senaryosu icin soft koruma kullanilir:

- GitHub Action: `.github/workflows/soft-main-guard.yml`
  - `main` branch'e gelen commit bir PR ile iliskili degilse workflow fail olur.
  - Acik override: commit mesajina `[main-guard:allow]` eklenebilir.
- Local hook: `.githooks/pre-push`
  - Gelistirici tarafinda `main` branch'e direkt push'u engeller.

Local hook'u aktif etmek icin bir kez calistir:

```bash
git config core.hooksPath .githooks
```
