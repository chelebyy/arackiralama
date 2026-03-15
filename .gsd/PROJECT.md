# Araç Kiralama Platformu

## What This Is

Türkiye pazarına (Alanya odaklı) yönelik çok dilli araç kiralama platformu. Kullanıcılar online araç arayabilir, rezervasyon yapabilir ve ödeme gerçekleştirebilir. Backend .NET 10 Clean Architecture, Frontend Next.js 16.

## Core Value

Kullanıcıların 15 dakikalık rezervasyon tutma, 3D Secure ödeme ve çok dilli destek ile güvenilir araç kiralama deneyimi.

## Requirements

### Validated

- ✓ Temel altyapı (Database, API, Middleware, Docker) — Phase 1
- ✓ Filo yönetimi (Vehicle CRUD, Office, Maintenance) — Phase 2
- ✓ Fiyatlandırma motoru (Seasonal pricing, Campaigns, Deposit) — Phase 3
- ✓ Rezervasyon sistemi (Availability, Hold, Lifecycle) — Phase 4
- ✓ Ödeme entegrasyonu (Iyzico/Mock, 3D Secure, Webhook) — Phase 5

### Active

- [ ] Kullanıcı yönetimi ve kimlik doğrulama (JWT, RBAC) — S01 (domain+persistence) ve S02 (JWT/session altyapısı) tamamlandı; S03/S04 ile müşteri/admin uçtan uca auth API davranışları tamamlanacak
- [ ] Müşteri kayıt/giriş sistemi
- [ ] Admin kullanıcı yönetimi
- [ ] SMS ve E-posta bildirimleri
- [ ] Background job işleme
- [ ] Audit logging
- [ ] Feature flag yönetimi
- [ ] Public website (Next.js 5 dil desteği)
- [ ] Admin panel genişletme
- [ ] Production deployment (VPS, SSL, Monitoring)

### Out of Scope

- Multi-branch franchise management — Başlangıçta tek lokasyon (Alanya)
- Corporate invoicing automation — Manuel fatura yeterli
- Dynamic pricing AI engine — Sabit mevsimsel fiyatlandırma yeterli
- Loyalty program — İleriki versiyonlara ertelendi
- Android app — API-ready ama mobil uygulama v1'de yok

## Context

Mevcut kod tabanı:
- Backend: .NET 10 Clean Architecture (Core, Infrastructure, API, Worker)
- Frontend: Next.js 16 App Router + React 19 + Tailwind CSS
- Database: PostgreSQL 18 + Redis 7.4
- 5 faz tamamlandı: Foundation, Fleet, Pricing, Reservation, Payment
- Kalan 5 faz: User Management, Notifications, Frontend, Infrastructure, Testing

## Constraints

- **Tech Stack:** .NET 10, Next.js 16, PostgreSQL, Redis — Karar verildi
- **Timeline:** 20 hafta toplam proje süresi — 10 hafta kaldı
- **Budget:** VPS deployment, tek sunucu — Maliyet optimizasyonu
- **Target Market:** Türkiye (Alanya) — Lokal pazar odaklı

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Modular Monolith | Microservice-ready, az operational complexity | ✓ Good |
| PostgreSQL + Redis | ACID + Cache kombinasyonu | ✓ Good |
| Iyzico payment | Türkiye pazarı için yerel sağlayıcı | ✓ Good |
| 5 dil desteği | TR/EN/RU/AR/DE turist pazarı | — Pending |
| Single Next.js app | Public + Admin aynı deploy | — Pending |

---
*Last updated: 2026-03-15 after completing M001/S02 (JWT & Session Infrastructure)*
