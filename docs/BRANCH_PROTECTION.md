# Branch Protection Kuralları

Bu doküman, GitHub repository'sinde ayarlanması gereken branch protection kurallarını tanımlar.

## main Branch Koruması

GitHub'da ayarlamak için: **Settings → Branches → Add branch protection rule → main**

### Zorunlu Kurallar

| Kural | Ayar | Açıklama |
|-------|------|----------|
| Require pull request before merging | ✅ | Direct push engellenir |
| Require approvals | ✅ (1) | En az 1 review gerekli |
| Require status checks | ✅ | CI check'leri geçmeli |
| Require branches to be up to date | ✅ | Merge öncesi rebase |

### Status Checks (Gerekli)

Aşağıdaki check'lerin geçmesi zorunludur:

```
✅ backend-build-test     # .NET build + test
✅ frontend-ci           # Next.js lint + build
✅ docker-build          # Docker image build test
```

### Ayar Detayları

#### 1. Pull Request Gereksinimleri

```
☑ Require a pull request before merging
  ☑ Require approvals (1)
  ☑ Dismiss stale pull request approvals when new commits are pushed
  ☑ Require review from Code Owners
```

#### 2. Status Checks

```
☑ Require status checks to pass before merging
  ☑ Require branches to be up to date before merging

  Status checks required:
  - backend-build-test
  - frontend-ci
  - docker-build
```

#### 3. Merge Stratejisi

```
☐ Allow merge commits
☐ Allow squash merging
☑ Allow rebase merging  ← Tercih edilen
```

#### 4. Diğer Ayarlar

```
☑ Do not allow bypassing the above settings
☐ Allow force pushes
☐ Allow deletions
```

## Soft Main Guard

Mevcut `soft-main-guard.yml` workflow'u, direct push girişimlerini tespit eder ve uyarır. Ancak bu **reactive** bir çözümdür. Asıl koruma için GitHub UI'dan yukarıdaki kuralları ayarlamak gerekir.

### Workflow Davranışı

```yaml
# Tetiklendiğinde:
1. Direct push'u tespit eder
2. Comment olarak uyarı yazar
3. Slack/email bildirimi (opsiyonel)
```

## Özel Durumlar

### Acil Hotfix

Acil durumlarda branch protection'ı bypass etmek için:

1. **Repository Admin** → Settings → Branches → main → Edit
2. "Do not allow bypassing" işaretini kaldır
3. Hotfix'i push et
4. Tekrar işaretle

⚠️ Bu işlem loglanır ve sonradan review edilmelidir.

### Release Branch'leri

Release branch'leri için daha sıkı kurallar:

```
☑ Require pull request before merging
☑ Require approvals (2)  ← Daha fazla review
☑ Require signed commits
```

## Doğrulama

Kuralların doğru çalıştığını test etmek için:

```bash
# 1. Direct push denemesi (başarısız olmalı)
git push origin main
# Expected: ! [remote rejected] main -> main (protected branch hook declined)

# 2. PR aç (check'lerin geçmesini bekle)
gh pr create --base main --head feature/test

# 3. Check'leri görüntüle
gh pr checks <pr-number>
```

## Repository Ayarları

### Actions Permissions

Settings → Actions → General:

```
☑ Allow all actions and reusable workflows
☐ Allow actions created by GitHub
☐ Allow actions created by Metadoc
```

### Fork Pull Request Workflow

```
☑ Require approval for all outside collaborators
```

## İlgili Dokümanlar

- [GIT_WORKTREES.md](./GIT_WORKTREES.md) - Worktree kullanımı
- [soft-main-guard.yml](../.github/workflows/soft-main-guard.yml) - Push koruması
- [Private Repo Policy](./11_Private_Repo_Soft_Protection_Policy.md) - Soft protection politikası
