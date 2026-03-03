# Arac Kiralama Platformu

Bu repository iki ana bolum icerir:

- `Admin_Dashboard/`: Next.js tabanli dashboard arayuzu
- `backend/`: .NET 10 backend (API, Worker, Core, Infrastructure)

## Dokumanlar

Uygulama plani ve takip:

- `docs/09_Implementation_Plan.md`
- `docs/10_Execution_Tracking.md`
- `docs/11_Private_Repo_Soft_Protection_Policy.md` (private repo branch guvenlik politikasi)

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

## Private Repo Icin Soft Main Koruma

Bu repo private oldugu ve plan kisiti nedeniyle GitHub branch protection acilamadigi icin soft koruma kullanilir.

- GitHub Action: `.github/workflows/soft-main-guard.yml`
  - `main` branch'e PR baglantisi olmayan push tespit edilirse otomatik revert eder.
  - Acil durum override: commit mesajina `[main-guard:allow]` eklenebilir.
- Local hook: `.githooks/pre-push`
  - Gelistirici tarafinda `main` branch'e direkt push'u engeller.

Local hook'u aktif etmek icin bir kez calistir:

```bash
git config core.hooksPath .githooks
```

