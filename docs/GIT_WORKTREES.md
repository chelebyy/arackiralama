# Git Worktrees Kullanım Kılavuzu

## Nedir?

Git worktrees, aynı repository'nin farklı branch'lerini aynı anda farklı dizinlerde çalışmanıza olanak tanır. Her worktree bağımsız bir çalışma alanıdır.

## Neden Kullanmalı?

| Sorun | Çözüm |
|-------|-------|
| Context switching maliyeti | Aynı anda birden fazla branch'te çalışma |
| `git stash` karmaşası | Worktree'ler arasında geçiş yapmak anlık |
| Build'i bozmadan başka iş yapma | İzole çalışma ortamları |
| Parallel development | Farklı feature'ları eşzamanlı geliştirme |

## Temel Komutlar

### Yeni Worktree Oluştur

```bash
# Mevcut branch'ten yeni worktree
git worktree add ../my-feature feature-branch

# Yeni branch ile birlikte
git worktree add -b new-feature ../new-feature main
```

### Worktree'leri Listele

```bash
git worktree list
```

### Worktree Sil

```bash
# Önce worktree'yi temizle
cd ../my-feature
git checkout main
cd ../main-repo

# Sonra sil
git worktree remove my-feature

# Zorla sil (commit edilmemiş değişiklikler varsa)
git worktree remove --force my-feature
```

## Örnek Workflow

```bash
# Ana proje dizinindeyiz
cd /path/to/project

# Hotfix için worktree oluştur
git worktree add ../project-hotfix hotfix/urgent-bug

# Hotfix üzerinde çalış
cd ../project-hotfix
# ... fix yap ...
git add . && git commit -m "fix: urgent bug"
git push origin hotfix/urgent-bug

# Ana işe dön
cd ../project

# İş bitince worktree'yi temizle
git worktree remove ../project-hotfix
```

## Claude Code ile Kullanım

Claude Code, worktree desteği sunar:

```
# Claude'a worktree oluştur dedirtebilirsin
"start a worktree for feature-x"
```

Claude otomatik olarak:
1. `.worktrees/` altında yeni bir dizin oluşturur
2. İlgili branch'i checkout eder
3. Session'ı yeni worktree'ye taşır

## Dizin Yapısı

```
project/
├── .git/
├── .worktrees/          # ← Worktree'ler burada
│   ├── feature-auth/
│   └── hotfix-api/
├── frontend/
└── backend/
```

## Dikkat Edilmesi Gerekenler

1. **Aynı branch'i iki worktree'de kullanma** - Git buna izin vermez
2. **Alt modüller** - Her worktree kendi submodule'lerini çeker
3. **Disk alanı** - Her worktree tam bir working copy'dir
4. **IDE** - VS Code gibi IDE'ler farklı worktree'leri ayrı workspace olarak açabilir

## Pratik İpuçları

```bash
# Alias ekle (.gitconfig)
[alias]
    wt = worktree
    wta = worktree add
    wtl = worktree list
    wtr = worktree remove

# Kullanım
git wta ../feature-x feature-x
git wtl
git wtr ../feature-x
```

## Temizlik

Zamanla worktree'ler birikebilir. Düzenli olarak kontrol et:

```bash
# Tüm worktree'leri gör
git worktree list

# Kullanılmayanları temizle
git worktree prune
```
