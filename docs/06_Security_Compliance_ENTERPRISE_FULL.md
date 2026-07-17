# Security & Compliance Document

Date: 2026-02-25
Updated: 2026-07-17 (Dokploy Disabled-mode deployment acceptance evidence added)

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

## 5. Dependency Security (Historical Snapshot)

> The results in sections 5.1 and 5.2 are the 4 May 2026 point-in-time scans. They are retained as historical evidence and must not be used as the current release gate. Section 13 records the live dependency-alert state.

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

## 7. OWASP Top 10 Değerlendirmesi (Phase 10.5 Historical Snapshot)

| # | Kategori | Durum | Notlar |
|---|----------|-------|--------|
| A01 | Broken Access Control | ✅ | RBAC policy'leri + `[Authorize]` attribute'ları aktif |
| A02 | Cryptographic Failures | ✅ | BCrypt wf=12, JWT HMAC-SHA256, CSPRNG refresh token, timing-safe compare |
| A03 | Injection | ✅ | EF Core parameterized queries, raw SQL yok (production) |
| A04 | Insecure Design | 🟡 | CORS eksik, security headers eksik — medium risk |
| A05 | Security Misconfiguration | 🟡 | `AllowedHosts: "*"`, Swagger unconditionally, `AutoMigrateOnStartup: true` |
| A06 | Vulnerable Components | ✅ | The 11 tracked Dependabot alerts are fixed without dismissal and the fresh SBOM contains only the patched target versions; current evidence and future dependency-review requirements are tracked in section 13 |
| A07 | Auth Failures | ✅ | JWT + refresh + session validation + brute force lockout |
| A08 | Data Integrity Failures | ✅ | Webhook HMAC verification, idempotency keys |
| A09 | Logging Failures | ✅ | Audit log + request log + error log tam |
| A10 | SSRF | ✅ | Outbound requests sadece configured payment provider URL'lerine |

## 13. Codex Security Findings Remediation Status (17 July 2026)

| Boundary | Implemented control | Current evidence | Remaining gate |
| --- | --- | --- | --- |
| Guest account claim | Hashed, expiring, single-use email claim token; previous active tokens superseded; generic registration response; normalized-account cooldown; one-active-token database invariant; bounded retention cleanup | Fresh focused final validation passed the 143-test security subset, 794/794 unit tests, 53/53 integration tests, and the five-locale Chromium claim/replay/login/profile-preservation path. The E2E harness follows the secure fragment-token and same-origin proxy contracts | Deliberately select and configure a production email provider, then prove controlled real delivery after deployment |
| Public reservation read | Allowlisted public DTO, strict rate limit, `no-store` | Production-like Docker Chromium captured the real response through all five localized confirmation pages; the exact 10-field allowlist matched and test-owned PII/internal values were absent | Re-run after deployment as part of the final combined release matrix |
| Reservation cancellation | Anonymous route removed; owner/admin routes preserved | Fresh Chromium HTTP proof returned `404/405` for anonymous and `404` for non-owner with unchanged `status/xmin/updated_at`; owner cancellation returned `200` and persisted `Cancelled`; the focused final validation records this original path as suppressed locally | Re-run after deployment |
| Production payment configuration | `ValidateOnStart`; missing/Mock/unknown/sandbox/incomplete real-provider and enabled configurations rejected; explicit `Disabled` accepted only with payments off and resolved to a dedicated fail-closed provider | Focused configuration tests pass 17/17, including real host start, concrete DI selection, and every provider operation. Compose expands to Disabled/TRY/false with blank optional Iyzico values, and the rebuilt Release image reaches Docker `running/healthy` without provider credentials; the earlier unsafe matrix remains the negative control. PR #410 was merged as `d0a7990`, its main-branch CI/GHCR path passed, and the live Dokploy public matrix kept browsing/settings available while all three public payment entry points returned `503` | GO for Disabled-mode deployment/public containment; independent container-log/image-digest evidence and real-provider sandbox proof remain separate or deferred gates |
| Payment state integrity | Payments default disabled; intent, 3DS return, webhook, and admin retry paths return `503` before service mutation | Fresh Docker proof returned `503` for intent, forged 3DS, and forged webhook; payment intent/event/job/paid-reservation fingerprint stayed `4|0|0|1` | Keep disabled until a real provider is selected; then require server-to-server verification, negative/replay tests, and sandbox success |
| Secret artifacts | Generated Ship-Safe artifacts removed; scanner outputs and local `.dotnet/` telemetry ignored; all 13 remaining tracked `.dotnet` sentinel/cache/telemetry files removed; Gitleaks scans working tree and Git history | Fresh CI-equivalent pinned Gitleaks working-tree/full-history scan passed; generated paths are untracked and ignored. The Resend-shaped candidate had no source/env/deploy/account anchor. The three Upstash-shaped matches were arbitrary substrings inside Base64-encoded gzip .NET telemetry and were absent after decoding. Neither provider-shaped candidate is applicable for rotation | Preserve the sanitized triage evidence; no provider rotation or access-log review is required for these scanner false positives |
| Main change governance | Active `Protect main - solo developer` ruleset requires a PR, resolved threads, current branch, seven checks, and squash merge; deletion/non-fast-forward updates blocked; no bypass actors | Ruleset ID `18985047` verified active for `refs/heads/main`; PR #408 merged as `27c7f05` and the exact merge commit passed CI, Secret Scan, React Doctor, and CodeQL | Preserve exact check names and revalidate the ruleset after workflow renames |
| Dependency review | Dependabot auto-merge workflow removed; dependency PRs use the same ruleset and require a manual merge/close decision | Live ruleset `18985047` is active with zero bypass actors, resolved-thread enforcement, strict current-branch checks, seven required checks, and squash-only merge. No Dependabot PR was created after activation | Observe one Dependabot PR created or refreshed after ruleset activation through the complete gated lifecycle |
| Dependency alert state | No blanket clean-scan claim; GitHub Dependabot is the live alert source; PR #405 merged patched Babel, Vite, esbuild, undici, and js-yaml versions | PR #405 passed the Node 22 frontend job, all repository checks, and exact-head Codex review, then squash-merged as `479317b`. A complete live traversal of `frontend/pnpm-lock.yaml` returned 1,069 dependencies across 11 pages with only the five patched target versions and zero old target entries. The 11 original alerts later reconciled to `fixed`: `39`, `41`, `43`, `44`, `45`, `46`, and `47` at `2026-07-15T15:27:57Z`; `48`, `50`, `51`, and `52` at `2026-07-15T15:27:58Z`; no dismissal or auto-dismissal was used. The fresh SBOM generated at `2026-07-16T10:24:16Z` contains 1,121 packages and only `@babel/core` 7.29.6, Vite 7.3.5, esbuild 0.28.1, undici 7.28.0, and js-yaml 4.2.0 for the target set, with zero old-version matches. Local `pnpm audit` still returns registry HTTP `410`, so it is not a vulnerability result | The dependency alert-record reconciliation gate is satisfied. Preserve the alert IDs, `fixed_at` timestamps, PR/check evidence, and patched SBOM; continue to require the normal human-reviewed dependency workflow for future changes |
| Focused final validation | Instance-preserving revalidation of all seven original findings with the application-under-test at `202074f`; corrected committed account-claim harness re-run at `9420446` against the same Docker stack | Five candidates are suppressed by current dynamic/static counterevidence. The Resend and Upstash scanner candidates are now not applicable; authoritative payment-provider verification remains deferred. Full backend, full frontend, Docker build/health, two Chromium attack regressions, disabled-payment no-write proof, Gitleaks, live GitHub governance checks, and the Disabled-mode Dokploy public acceptance matrix passed | Disabled-mode deployment/public containment is closed; production email, deployed remaining attack-path revalidation, independent container evidence, and real-provider proof remain separate release gates |

No statement in this table means the repository has received a complete security audit or is production-safe. `docs/18_Codex_Security_Findings_Implementation.md` remains the closure authority.
