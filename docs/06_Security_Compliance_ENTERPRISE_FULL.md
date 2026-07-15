# Security & Compliance Document

Date: 2026-02-25
Updated: 2026-07-15 (PR #402 merge evidence and main-branch governance added)

## 1. Network Security

-   Firewall restricted to 80/443
-   No public DB access
-   SSH key-only login

## 2. Application Security

### 2.1 Doğrulanan Kontroller (Phase 10.5 — 4 Mayıs 2026)

| Kontrol | Durum | Kanıt |
|---------|-------|-------|
| JWT authentication | ✅ | HMAC-SHA256, 15 dk access / 7 gün refresh, session validation, custom OnChallenge |
| RBAC authorization | ✅ | GuestOnly, CustomerOnly, AdminOnly, SuperAdminOnly policy'leri |
| Rate limiting | ✅ | Tiered: Global 100/min, Strict 5/min, Payment 10/min, Standard 30/min, Health 10/min |
| Idempotency enforcement | ✅ | IdempotencyMiddleware + IdempotentAttribute |
| Password hashing | ✅ | BCrypt work factor 12 |
| Auth cookies | ✅ | `__Host-rac_refresh`, SameSite=Strict, HttpOnly, Secure |
| Error handling | ✅ | Generic client mesajı, stack trace log'a yazılır (sızdırılmaz) |
| Request logging sanitization | ✅ | Newline sanitization (log injection önlemi) |
| JWT secret validation | ✅ | `JwtSecretValidator`: 32+ char, placeholder blok (Production/Staging) |
| Webhook signature verification | ✅ | Iyzico HMAC + MockProvider secret verify |
| SQL Injection prevention | ✅ | EF Core parameterized queries; `ExecuteSqlRaw` sadece migration/test fixture'larda |

### 2.2 Phase 10.5 Hardening Follow-up (10 Mayıs 2026)

| Kontrol | Durum | Kanıt |
|---------|-------|-------|
| CORS | ✅ | `ServiceCollectionExtensions` named `ApiCors` policy + explicit `Cors:AllowedOrigins`; development localhost fallbacks in `appsettings.Development.json` |
| Security Headers | ✅ | Non-development responses emit HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy |
| Swagger/OpenAPI exposure | ✅ | `ApplicationBuilderExtensions` only maps OpenAPI + Swagger UI in Development |
| AllowedHosts | ✅ | Base `appsettings.json` no longer uses `"*"`; local defaults restricted to localhost variants |
| AutoMigrateOnStartup | ✅ | Base `appsettings.json` now defaults to `false`; explicit env opt-in required for startup migrations |
| Drifted local migration safety | ✅ | `AddMissingBackgroundJobColumns` is idempotent against pre-existing `background_jobs.last_error` / `failed_at` columns |

### 2.3 Düşük Risk Bulgular

- `dangerouslySetInnerHTML` kullanımı: `chart.tsx` ve `code-block.tsx`'te var. Veri internally generated (Shiki output, CSS variables) — kullanıcı girdisi değil.
- `AutoMigrateOnStartup`: Sadece local dev için riskli değil; production config'de mutlaka `false` olmalı.

## 3. Payment Security

-   No card storage
-   Webhook signature validation
-   Deposit pre-auth via provider

## 4. Data Protection

-   TLS enforced (Traefik/Dokploy edge termination)
-   PII masked in logs
-   5-year payment log retention

## 5. Dependency Security

### 5.1 Backend (4 Mayıs 2026)

| Araç | Komut | Sonuç |
|------|-------|-------|
| `dotnet list package --vulnerable` | `dotnet list backend/RentACar.sln package --vulnerable` | ✅ 0 vulnerability |

### 5.2 Frontend (4 Mayıs 2026)

| Araç | Komut | Sonuç |
|------|-------|-------|
| `pnpm audit` | `corepack pnpm -C frontend audit` | ✅ 0 vulnerability |

> **Not:** Önceki taramada 10 vulnerability bulunmuştu (4 high, 6 moderate). `pnpm update` + `pnpm.overrides` (lodash, uuid, postcss) ile temizlendi. `minimatch` override'u coverage test'ini kırdığı için kaldırıldı.

## 6. Monitoring

-   Uptime monitoring
-   5xx alerts
-   Disk usage alerts

## 7. OWASP Top 10 Değerlendirmesi (Phase 10.5)

| # | Kategori | Durum | Notlar |
|---|----------|-------|--------|
| A01 | Broken Access Control | ✅ | RBAC policy'leri + `[Authorize]` attribute'ları aktif |
| A02 | Cryptographic Failures | ✅ | BCrypt wf=12, JWT HMAC-SHA256, CSPRNG refresh token, timing-safe compare |
| A03 | Injection | ✅ | EF Core parameterized queries, raw SQL yok (production) |
| A04 | Insecure Design | 🟡 | CORS eksik, security headers eksik — medium risk |
| A05 | Security Misconfiguration | 🟡 | `AllowedHosts: "*"`, Swagger unconditionally, `AutoMigrateOnStartup: true` |
| A06 | Vulnerable Components | ✅ | Dependency scan: 0 vulnerability |
| A07 | Auth Failures | ✅ | JWT + refresh + session validation + brute force lockout |
| A08 | Data Integrity Failures | ✅ | Webhook HMAC verification, idempotency keys |
| A09 | Logging Failures | ✅ | Audit log + request log + error log tam |
| A10 | SSRF | ✅ | Outbound requests sadece configured payment provider URL'lerine |

## 13. Codex Security Findings Remediation Status (15 July 2026)

| Boundary | Implemented control | Current evidence | Remaining gate |
| --- | --- | --- | --- |
| Guest account claim | Hashed, expiring, single-use email claim token; previous active tokens superseded; generic registration response; normalized-account cooldown; one-active-token database invariant; bounded retention cleanup | Focused abuse/cleanup tests 29/29; Docker concurrency, worker cleanup, and five-locale Chromium claim/replay/login proof pass; final PR #402 backend run passed 794/794 unit and 53/53 integration | Resend integration and real production email-delivery proof, deferred until the provider is introduced |
| Public reservation read | Allowlisted public DTO, strict rate limit, `no-store` | Production-like Docker Chromium captured the real response through all five localized confirmation pages; the exact 10-field allowlist matched and test-owned PII/internal values were absent | Re-run after deployment as part of the final combined release matrix |
| Reservation cancellation | Anonymous route removed; owner/admin routes preserved | Production-like browser HTTP proof returned `404/405` for anonymous and `404` for non-owner with unchanged `status/xmin/updated_at`; owner cancellation returned `200` and persisted `Cancelled`; final PR #402 review found no major issue | Re-run after deployment |
| Production payment configuration | `ValidateOnStart`; missing/Mock/unknown/sandbox/incomplete configurations rejected; a fully configured real provider may boot with payments disabled | 13/13 focused host-start tests plus the current Release-image Docker matrix pass: five unsafe Production configurations exit non-zero without synthetic credential leakage; the safe-shaped control returns `/health` `200` with migrations/seeds disabled and an unchanged selected database fingerprint | GO for local configuration/startup acceptance; deployment rerun and real-provider sandbox proof remain separate gates |
| Payment state integrity | Payments default disabled; intent, 3DS return, webhook, and admin retry paths return `503` before service mutation | Unit proof plus local Docker HTTP/database-count proof | Keep disabled until a real provider is selected; then require server-to-server verification, negative/replay tests, and sandbox success |
| Secret artifacts | Generated Ship-Safe artifacts removed; scanner outputs ignored; Gitleaks scans working tree and Git history | Gitleaks passed on PR #402 and post-merge `main`; uploaded metadata is sanitized and retained for seven days | Provider-side credential rotation and access-log review |
| Main change governance | Active `Protect main - solo developer` ruleset requires a PR, resolved threads, current branch, seven checks, and squash merge; deletion/non-fast-forward updates blocked; no bypass actors | Ruleset ID `18985047` verified active for `refs/heads/main`; PR #402 required checks and post-merge `main` workflows passed | Preserve exact check names and revalidate the ruleset after workflow renames |
| Dependency review | Dependabot auto-merge workflow removed; dependency PRs use the same ruleset and require a manual merge/close decision | Existing PR #401 is mergeable at the Git-object level but `BEHIND`, so strict policy requires refresh and check rerun | Observe one Dependabot PR created or refreshed after ruleset activation through the complete gated lifecycle |

No statement in this table means the repository has received a complete security audit or is production-safe. `docs/18_Codex_Security_Findings_Implementation.md` remains the closure authority.
