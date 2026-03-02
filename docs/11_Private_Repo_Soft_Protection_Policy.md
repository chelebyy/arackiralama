# 11 - Private Repo Soft Protection Policy

Bu dokuman, `arackiralama` reposu private kalirken `main` branch guvenligini korumak icin kalici politika kaydidir.

## Neden Bu Politika Var?

- Bu repository private.
- Mevcut plan kisiti nedeniyle GitHub branch protection/rulesets API private repoda 403 donuyor.
- Bu nedenle branch protection yerine soft protection uygulanir.

## Aktif Koruma Mekanizmalari

1. GitHub Action: `.github/workflows/soft-main-guard.yml`
   - `main` branch push eventlerini izler.
   - PR ile iliskisi olmayan commitleri unauthorized kabul eder.
   - Unauthorized commitleri otomatik revert eder.
   - Olay kaydi icin issue acilir.

2. Local pre-push hook: `.githooks/pre-push`
   - Gelistirici makinesinde `main`e direkt pushu varsayilan olarak engeller.
   - Hook aktivasyonu: `git config core.hooksPath .githooks`

## Zorunlu Gelistirme Akisi

- `main` branch'e direkt push yapma.
- Her degisiklik feature branch uzerinde gelistirilir.
- `main`e gecis sadece PR merge ile yapilir.

## Acil Durum Override Kurali

- Sadece zorunlu durumlarda kullanilir.
- Local push override: `ALLOW_MAIN_PUSH=1 git push origin HEAD:main`
- Action override: commit mesajina `[main-guard:allow]` eklenir.
- Override kullanimi sonrasinda neden/etki kaydi `docs/10_Execution_Tracking.md` icine yazilir.

## Isletim Checklist'i (Her Yeni Makinede)

1. `git config core.hooksPath .githooks`
2. `bash -n .githooks/pre-push`
3. `bash -n .github/scripts/apply-branch-protection.sh`
4. Feature branch olusturup calisma akisini PR ile surdur

## Karar Notu

Bu politika, GitHub plani branch protection'i private repo icin destekleyene kadar gecerlidir.
Plan kosulu degisirsa, soft protection yerine native branch protection'a gecis yapilir.
