# Implementation Plan (Uygulama Planı)
# Araç Kiralama Platformu - Enterprise

**Proje:** Araç Kiralama Platformu (Alanya Rent A Car)  
**Versiyon:** 1.0.0  
**Tarih:** 02 Mart 2026  
**Durum:** Taslak  
**Hedef Pazar:** Türkiye (Başlangıç: Alanya)

---

## 📋 Executive Summary

Bu doküman, docs klasöründeki PRD, ADR, TDD, IDD, Runbook, Security, API Contract ve Error Spec dokümanlarına dayanarak hazırlanmış kapsamlı bir uygulama planıdır. Plan, modüler bir yaklaşımla fazlara bölünmüş, her fazada somut görevler ve kabul kriterleri tanımlanmıştır.


**Ekib Önerisi:** 2 Backend, 2 Frontend, 1 DevOps, 1 QA

## 🔐 Security Plan Referansı

Faz 1-7 güvenlik inceleme çıktıları ve Faz 8-10 güvenlik kapıları için:
`docs/11_Codex_Sentinel_Phase1_7_Security_Report_and_Phase8_10_Gates.md`

---

## 🏗️ Mimari Özet

### Teknoloji Stack
| Katman | Teknoloji | Versiyon |
|--------|-----------|----------|
| **Backend** | .NET SDK / ASP.NET Core | 10.0.103 / 10.0.3 |
| **Database** | PostgreSQL | 18.3 |
| **Cache** | Redis | 7.4.x |
| **Frontend** | Next.js + React | 16.1.6 / 19.2.0 |
| **UI** | Tailwind CSS | 3.4+ |
| **i18n** | next-intl | 3.5+ |
| **ORM** | Entity Framework Core | 10.0.0 |
| **Runtime** | Node.js | 25.6.1 |
| **Container** | Docker | 29.2.1 |
| **Reverse Proxy** | Nginx | 1.28.2 |
| **OS** | Ubuntu | 22.04 LTS |

### Domain Modülleri
1. **Reservation** - Rezervasyon yönetimi
2. **Fleet** - Araç filo yönetimi
3. **Pricing** - Fiyatlandırma motoru
4. **Payment** - Ödeme işlemleri
5. **Notification** - SMS/E-posta bildirimleri
6. **Feature Management** - Özellik bayrakları
7. **Identity & Access** - Kimlik ve erişim yönetimi

### Dil Desteği (i18n)
- 🇹🇷 Türkçe (Varsayılan)
- 🇬🇧 English
- 🇷🇺 Русский
- 🇸🇦 العربية (RTL)
- 🇩🇪 Deutsch

---

## 📊 Proje Fazları

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 1: Foundation (Temel Altyapı) -                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── Database Schema & Migration                                          │
│  ├── Core Domain Entities                                                  │
│  ├── Base API Structure (Controllers, Middleware)                         │
│  ├── Docker & Local Development Setup                                     │
│  └── CI/CD Pipeline (GitHub Actions)                                      │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 2: Fleet Management (Filo Yönetimi) -                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── Vehicle Groups & Categories                                          │
│  ├── Vehicle CRUD Operations                                               │
│  ├── Office Management                                                     │
│  ├── Maintenance Scheduling                                                │
│  └── Admin Panel - Fleet Module                                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 3: Pricing Engine (Fiyatlandırma Motoru) -                │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── Base Pricing Model                                                    │
│  ├── Seasonal Pricing Rules                                               │
│  ├── Campaign/Discount System                                              │
│  ├── Airport Fees & Extra Charges                                          │
│  ├── Deposit Calculation                                                   │
│  └── Admin Panel - Pricing Module                                          │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 4: Reservation System (Rezervasyon Sistemi) -             │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── Availability Search Engine                                            │
│  ├── 15-Minute Hold Mechanism (Redis)                                     │
│  ├── Reservation Lifecycle Management                                     │
│  ├── Overlap Prevention (DB + Cache)                                      │
│  └── Public Booking Flow (Frontend)                                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 5: Payment Integration (Ödeme Entegrasyonu) -             │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── IPaymentProvider Interface & Abstraction                             │
│  ├── Mock Provider Implementation                                         │
│  ├── Halkbank/Iyzico Integration                                          │
│  ├── 3D Secure Flow                                                        │
│  ├── Deposit Pre-Authorization                                            │
│  ├── Webhook Handling & Idempotency                                       │
│  └── Payment Retry & Refund Logic                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 6: User Management & Auth -                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── JWT Authentication                                                    │
│  ├── RBAC (Role-Based Access Control)                                     │
│  ├── Customer Registration/Login                                          │
│  ├── Admin & SuperAdmin Roles                                             │
│  ├── Profile Management                                                    │
│  └── Reservation History                                                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 7: Notifications & Background Jobs - H                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── ISmsProvider Interface                                                │
│  ├── Netgsm Integration                                                    │
│  ├── Twilio Fallback                                                       │
│  ├── Email Notifications                                                   │
│  ├── Background Job Processing (Worker)                                   │
│  └── Audit Logging                                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 8: Frontend Development -                               │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── Next.js Project Setup (App Router)                                   │
│  ├── i18n Implementation (5 languages)                                    │
│  ├── Public Website Design                                                │
│  │   ├── Home Page                                                        │
│  │   ├── Vehicle Search Results                                           │
│  │   ├── Vehicle Detail Page                                              │
│  │   ├── Booking Flow (4 steps)                                           │
│  │   └── Reservation Tracking                                             │
│  ├── Admin Panel                                                           │
│  │   ├── Dashboard                                                        │
│  │   ├── Reservation Management                                           │
│  │   ├── Fleet Management                                                 │
│  │   ├── Pricing Management                                               │
│  │   ├── User Management                                                  │
│  │   └── Reports & Analytics                                              │
│  └── RTL Support for Arabic                                               │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 9: Infrastructure & Deployment -                       │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── VPS Setup (Ubuntu 22.04)                                             │
│  ├── Docker Compose Configuration                                         │
│  ├── Nginx Configuration                                                  │
│  ├── SSL/TLS (Let's Encrypt)                                              │
│  ├── Backup Strategy                                                      │
│  ├── Monitoring Setup                                                     │
│  └── Production Deployment                                                │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│  FAZ 10: Testing & Launch -                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  ├── Unit Tests                                                            │
│  ├── Integration Tests                                                     │
│  ├── E2E Tests                                                             │
│  ├── Load Testing                                                          │
│  ├── Security Audit                                                        │
│  ├── UAT (User Acceptance Testing)                                        │
│  └── Go-Live                                                               │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 🔷 FAZ 1: Foundation (Temel Altyapı) - Hafta 1-4

### 🎯 Hedefler
- Proje yapısını oluşturma
- Veritabanı şemasını tasarlayıp uygulama
- Temel API yapısını kurma
- Docker ortamını hazırlama
- CI/CD pipeline kurma

### 📋 Görevler

#### 1.1 Proje Yapısı ve Kurulum
- [ ] Solution ve proje yapısını oluştur
  - `RentACar.sln`
  - `RentACar.Core` (Domain, Interfaces)
  - `RentACar.Infrastructure` (Data, External Services)
  - `RentACar.API` (Controllers, Middleware)
  - `RentACar.Worker` (Background Jobs)
- [ ] Git repository yapılandırması
- [ ] `.gitignore` ve `.editorconfig` dosyaları
- [ ] README.md oluşturma

#### 1.2 Veritabanı Şeması
- [ ] **Tablo Yapıları:**
  ```
  - vehicles (id, plate, brand, model, year, color, group_id, status, office_id)
  - vehicle_groups (id, name_tr, name_en, name_ru, name_ar, name_de, deposit_amount, min_age, min_license_years)
  - offices (id, name, address, phone, is_airport, opening_hours)
  - reservations (id, public_code, customer_id, vehicle_id, pickup_datetime, return_datetime, status, total_amount)
  - customers (id, full_name, phone, email, birth_date, license_year, identity_number, nationality)
  - pricing_rules (id, vehicle_group_id, start_date, end_date, daily_price, multiplier)
  - campaigns (id, code, discount_type, discount_value, min_days, valid_from, valid_until)
  - payment_intents (id, reservation_id, amount, status, provider, idempotency_key)
  - payment_webhook_events (id, provider_event_id, payload, processed)
  - reservation_holds (id, reservation_id, vehicle_id, expires_at, session_id) - Redis fallback
  - admin_users (id, email, password_hash, full_name, role, is_active)
  - audit_logs (id, action, entity_type, entity_id, user_id, timestamp, details)
  - background_jobs (id, type, payload, status, retry_count, scheduled_at)
  - feature_flags (id, name, enabled, description)
  ```
- [ ] EF Core Migration dosyalarını oluştur
- [ ] Database indexes oluştur (TDD Section 11.2)
- [ ] Seed data (örnek ofisler, araç grupları)

#### 1.3 Core Domain Entities
- [ ] Base Entity class (Id, CreatedAt, UpdatedAt)
- [ ] Vehicle ve VehicleGroup entities
- [ ] Office entity
- [ ] Customer entity
- [ ] Reservation entity (status enum: Draft, Hold, PendingPayment, Paid, Active, Completed, Cancelled, Expired)
- [ ] PaymentIntent entity
- [ ] AuditLog entity

#### 1.4 API Temel Yapısı
- [ ] Program.cs yapılandırması
- [ ] Dependency Injection container setup
- [ ] Custom Middleware:
  - CultureMiddleware (i18n)
  - CorrelationIdMiddleware
  - ErrorHandlingMiddleware
  - RequestLoggingMiddleware
- [ ] Base Controller ve Response wrapper
- [ ] Swagger/OpenAPI dokümantasyonu

#### 1.5 Güvenlik Altyapısı
- [x] JWT Authentication yapılandırması
- [x] Password hashing (BCrypt)
- [x] RBAC authorization attributes
- [x] Rate limiting (TDD Section 12)

#### 1.6 Docker & Local Dev
- [ ] Backend Dockerfile
- [ ] Worker Dockerfile
- [ ] `docker-compose.yml` (local development)
- [ ] PostgreSQL container
- [ ] Redis container

#### 1.7 CI/CD Pipeline
- [ ] GitHub Actions workflow:
  - [x] Build & Test
  - [x] Docker image build
  - [x] Push to registry (opsiyonel)
- [ ] Branch protection rules

### ✅ Kabul Kriterleri
- [ ] `docker-compose up` komutu ile tüm servisler başlıyor
- [ ] Database migration'lar hatasız çalışıyor
- [ ] API health check endpoint (`/health`) 200 OK dönüyor
- [ ] Swagger UI erişilebilir ve dokümante edilmiş
- [ ] CI pipeline başarıyla tamamlanıyor

### 📦 Çıktılar
```
/backend
  ├── RentACar.sln
  ├── src/
  │   ├── RentACar.Core/
  │   │   ├── Entities/
  │   │   ├── Interfaces/
  │   │   ├── Enums/
  │   │   └── ValueObjects/
  │   ├── RentACar.Infrastructure/
  │   │   ├── Data/
  │   │   ├── Migrations/
  │   │   ├── Repositories/
  │   │   └── Services/
  │   ├── RentACar.API/
  │   │   ├── Controllers/
  │   │   ├── Middleware/
  │   │   └── appsettings.json
  │   └── RentACar.Worker/
  ├── docker-compose.yml
  └── .github/workflows/ci.yml
```

---

## 🔷 FAZ 2: Fleet Management (Filo Yönetimi) - Hafta 3-6

### 🎯 Hedefler
- Araç ve filo yönetim sistemini kurma
- Admin panel için backend API'leri
- Araç Kiralama

### 📋 Görevler

#### 2.1 Vehicle Group Management
- [ ] `IVehicleGroupRepository` interface
- [ ] CRUD endpoints for vehicle groups
- [ ] Multi-language name support
- [ ] Vehicle group features (JSONB array)

#### 2.2 Vehicle Management
- [ ] `IVehicleRepository` interface
- [ ] `IFleetService` implementation (TDD Section 8.4)
- [ ] Vehicle CRUD API endpoints
- [ ] Vehicle status management (Available, Maintenance, Retired)
- [ ] Vehicle transfer between offices
- [ ] Vehicle maintenance scheduling
- [ ] Photo upload (local storage for MVP)

#### 2.3 Office Management
- [ ] Office CRUD operations
- [ ] Office hours configuration
- [ ] Airport vs City office distinction

#### 2.4 Admin API Endpoints
```
GET    /api/admin/v1/vehicles
POST   /api/admin/v1/vehicles
PUT    /api/admin/v1/vehicles/{id}
DELETE /api/admin/v1/vehicles/{id}
POST   /api/admin/v1/vehicles/{id}/maintenance
POST   /api/admin/v1/vehicles/{id}/transfer

GET    /api/admin/v1/vehicle-groups
POST   /api/admin/v1/vehicle-groups
PUT    /api/admin/v1/vehicle-groups/{id}

GET    /api/admin/v1/offices
POST   /api/admin/v1/offices
PUT    /api/admin/v1/offices/{id}
```

#### 2.5 Repository Implementations
- [ ] Generic Repository pattern
- [ ] Unit of Work pattern
- [ ] Specification pattern for complex queries

### ✅ Kabul Kriterleri
- [ ] Tüm CRUD operasyonları Postman/Insomnia ile test edilmiş
- [ ] Araç durumu değişiklikleri audit log'a yazılıyor
- [ ] Bakım planlanan araçlar müsaitlik sorgularında hariç tutuluyor
- [ ] Araç transferleri ofis envanterini güncelliyor

---

## 🔷 FAZ 3: Pricing Engine (Fiyatlandırma Motoru) - Hafta 5-8

### 🎯 Hedefler
- Dinamik fiyatlandırma motoru
- Mevsimsel fiyat kuralları
- Kampanya indirim sistemi
- Ek ücret hesaplama (havalimanı, tek yön)

### 📋 Görevler

#### 3.1 Base Pricing
- [ ] `IPricingService` interface (TDD Section 8.5)
- [ ] Daily base price calculation
- [ ] Minimum rental days validation
- [ ] Weekend/weekday pricing (opsiyonel)

#### 3.2 Seasonal Pricing
- [ ] SeasonalPricingRule entity
- [ ] Date range overlap handling
- [ ] Multiplier vs Fixed price support
- [ ] Priority-based rule application

#### 3.3 Campaign System
- [ ] Campaign entity
- [ ] Campaign code validation
- [ ] Discount types: Percentage, Fixed amount
- [ ] Campaign restrictions (min days, vehicle groups)
- [ ] Campaign expiry handling

#### 3.4 Additional Fees
- [ ] Airport delivery fee calculation
- [ ] One-way rental fee
- [ ] Extra driver fee
- [ ] Child seat fee
- [ ] Young driver fee (if applicable)

#### 3.5 Deposit Calculation
- [ ] Per-vehicle-group deposit amounts
- [ ] Pre-authorization amount calculation
- [ ] Full coverage waiver option (opsiyonel)

#### 3.6 Price Breakdown API
```json
{
  "daily_rate": 750.00,
  "rental_days": 4,
  "base_total": 3000.00,
  "extras_total": 250.00,
  "campaign_discount": -250.00,
  "airport_fee": 250.00,
  "final_total": 3750.00,
  "deposit_amount": 2000.00
}
```

### ✅ Kabul Kriterleri
- [ ] Fiyat hesaplama tüm senaryolar için doğru çalışıyor
- [ ] Kampanya kodları büyük/küçük harf duyarsız
- [ ] Geçersiz kampanya kodu uygun hata mesajı dönüyor
- [ ] Mevsimsel fiyatlar öncelik sırasına göre uygulanıyor
- [ ] Fiyat hesaplama < 100ms response time

---

## 🔷 FAZ 4: Reservation System (Rezervasyon Sistemi) - Hafta 7-10

### 🎯 Hedefler
- Müsaitlik arama motoru
- 15 dakikalık rezervasyon tutma (hold)
- Rezervasyon yaşam döngüsü yönetimi
- Çakışma önleme (DB + Cache)

### 📋 Görevler

#### 4.1 Availability Search Engine
- [ ] `IReservationService` interface (TDD Section 8.3)
- [ ] Search availability query:
  ```sql
  -- Overlap detection: (start < existingEnd) AND (end > existingStart)
  SELECT vg.*, COUNT(v.id) as available_count
  FROM vehicle_groups vg
  JOIN vehicles v ON v.group_id = vg.id
  LEFT JOIN reservations r ON r.vehicle_id = v.id
    AND r.pickup_datetime < @returnDate
    AND r.return_datetime > @pickupDate
    AND r.status IN ('Paid', 'Active')
  WHERE v.status = 'Available'
    AND v.office_id = @officeId
    AND r.id IS NULL
  GROUP BY vg.id
  ```
- [ ] Office-based filtering
- [ ] Vehicle group-based search
- [ ] Pagination
- [ ] Caching with 5-minute TTL

#### 4.2 Reservation Hold Mechanism
- [ ] Redis-based hold storage (15-minute TTL)
- [ ] Hold creation endpoint
- [ ] Hold extension endpoint (max 15 min)
- [ ] Hold release endpoint
- [ ] Fallback to DB if Redis unavailable (TDD Section 9.5)
- [ ] Session-based idempotency

#### 4.3 Reservation Lifecycle
```
Draft → Hold → PendingPayment → Paid → Active → Completed
         ↓         ↓              ↓
    Expired   Cancelled      Refunded
```

- [ ] State machine implementation
- [ ] Status transition validation
- [ ] Automatic expiry handling (background job)

#### 4.4 Overlap Prevention
- [ ] Database-level unique constraints
- [ ] Transactional booking flow
- [ ] Optimistic locking
- [ ] Double-booking detection (edge case handling)

#### 4.5 Public API Endpoints
```
GET  /api/v1/vehicles/available?pickup_datetime=&return_datetime=&office_id=
GET  /api/v1/vehicles/groups
POST /api/v1/reservations                    # Create draft
POST /api/v1/reservations/{id}/hold          # Place 15-min hold
GET  /api/v1/reservations/{publicCode}       # Public tracking
```

#### 4.6 Admin Reservation Management
```
GET    /api/admin/v1/reservations
GET    /api/admin/v1/reservations/{id}
POST   /api/admin/v1/reservations/{id}/cancel
POST   /api/admin/v1/reservations/{id}/assign-vehicle
PUT    /api/admin/v1/reservations/{id}/check-in   # Teslim alma
PUT    /api/admin/v1/reservations/{id}/check-out  # Teslim etme
```

### ✅ Kabul Kriterleri
- [ ] Aynı araç için çakışan rezervasyon oluşturulamıyor
- [ ] 15 dakikalık hold süresi Redis TTL ile yönetiliyor
- [ ] Hold süresi dolunca araç tekrar müsait görünüyor
- [ ] Müsaitlik sorgusu < 300ms
- [ ] Çift rezervasyon vakası = 0 (test edilmiş)

---

## 🔷 FAZ 5: Payment Integration (Ödeme Entegrasyonu) - Hafta 9-12

### 🎯 Hedefler
- Ödeme sağlayıcı soyutlama katmanı
- 3D Secure ödeme akışı
- Depozito ön yetkilendirme
- Webhook işleme ve idempotency

### 📋 Görevler

#### 5.1 Payment Provider Abstraction
- [ ] `IPaymentProvider` interface (TDD Section 8.1)
- [ ] Mock Provider implementation (development)
- [ ] Provider configuration (appsettings)

#### 5.2 Halkbank/Iyzico Integration
- [ ] Iyzico SDK integration
- [ ] CreatePaymentIntent implementation
- [ ] 3D Secure redirect flow
- [ ] Payment verification callback
- [ ] Transaction status polling

#### 5.3 Payment Flow
```
1. Create PaymentIntent (idempotency key ile)
2. 3D Secure redirect
3. Bank callback → Webhook/API
4. Verify payment
5. Update reservation status (Paid)
6. Create background jobs (SMS, Email)
```

#### 5.4 Deposit Pre-Authorization
- [ ] CreatePreAuthorization
- [ ] CapturePreAuthorization (hasar varsa)
- [ ] ReleasePreAuthorization (araç iade)
- [ ] Deposit status tracking

#### 5.5 Webhook Handling
- [ ] Webhook endpoint: `POST /api/v1/payments/webhook/{provider}`
- [ ] Signature verification
- [ ] Idempotency enforcement (provider_event_id unique constraint)
- [ ] Webhook event queuing for processing
- [ ] Duplicate event detection

#### 5.6 Refund Operations
- [ ] Full refund
- [ ] Partial refund (opsiyonel)
- [ ] Cancellation fee calculation
- [ ] Refund reason tracking

#### 5.7 Payment Error Handling
- [ ] Card declined handling
- [ ] 3D Secure failure handling
- [ ] Timeout retry logic
- [ ] Payment retry limit (3 attempts)

#### 5.8 Admin Payment Operations
```
POST /api/admin/v1/reservations/{id}/refund
POST /api/admin/v1/reservations/{id}/release-deposit
POST /api/admin/v1/payments/retry
GET  /api/admin/v1/payments/{id}/status
```

### ✅ Kabul Kriterleri
- [ ] Ödeme idempotency anahtarı ile tekrarlanamıyor
- [ ] Webhook imza doğrulaması çalışıyor
- [ ] Aynı webhook event birden fazla işlenmiyor
- [ ] 3D Secure başarısızlığında uygun hata mesajı
- [ ] Depozito tahsilatı ve iadesi doğru çalışıyor

---

## 🔷 FAZ 6: User Management & Auth - Hafta 11-14

### 🎯 Hedefler
- JWT tabanlı kimlik doğrulama
- Rol bazlı yetkilendirme (RBAC)
- Müşteri kayıt/giriş
- Admin kullanıcı yönetimi

### 📋 Görevler

#### 6.1 Authentication
- [ ] JWT token generation
- [ ] JWT token validation middleware
- [ ] Refresh token mechanism
- [ ] Token revocation (logout)
- [ ] Password reset flow (email)

#### 6.2 User Types
- **Guest:** No auth required for search/booking
- **Customer:** Registered user (optional)
- **Admin:** Operational staff
- **SuperAdmin:** System admin

#### 6.3 Customer Management
- [ ] Customer registration
- [ ] Customer login (optional - can book as guest)
- [ ] Profile update
- [ ] Reservation history
- [ ] Driver license verification (opsiyonel)

#### 6.4 Admin User Management
- [ ] Admin user CRUD (SuperAdmin only)
- [ ] Role assignment
- [ ] Admin dashboard access
- [ ] Admin activity logging

#### 6.5 Authorization
- [ ] Role-based authorization attributes
- [ ] Resource-based authorization (own reservations)
- [ ] Permission matrix:

| Action | Guest | Customer | Admin | SuperAdmin |
|--------|-------|----------|-------|------------|
| Search vehicles | ✓ | ✓ | ✓ | ✓ |
| Create reservation | ✓ | ✓ | ✓ | ✓ |
| View own reservations | - | ✓ | ✓ | ✓ |
| Cancel reservation | - | ✓* | ✓ | ✓ |
| Manage vehicles | - | - | ✓ | ✓ |
| Manage pricing | - | - | ✓ | ✓ |
| Manage admins | - | - | - | ✓ |
| View audit logs | - | - | - | ✓ |

*Only if status allows

#### 6.6 API Endpoints
```
POST /api/v1/auth/register
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
POST /api/v1/auth/forgot-password
GET  /api/v1/auth/me
PUT  /api/v1/auth/profile

POST /api/admin/v1/auth/login
POST /api/admin/v1/auth/logout
```

### ✅ Kabul Kriterleri
- [ ] JWT token 24 saat geçerli
- [ ] Refresh token 7 gün geçerli
- [ ] Admin endpoint'ler JWT olmadan erişilemez
- [ ] Şifreler BCrypt ile hashlenmiş
- [ ] Hesap kilitleme (5 başarısız denemeden sonra)

---

## 🔷 FAZ 7: Notifications & Background Jobs - Hafta 13-16

### 🎯 Hedefler
- SMS bildirim sistemi
- E-posta bildirimleri
- Background job işleme
- Audit logging

### 📋 Görevler

#### 7.1 SMS Provider Integration
- [ ] `ISmsProvider` interface (TDD Section 8.2)
- [ ] Netgsm implementation (primary - Turkey)
- [ ] Twilio implementation (fallback)
- [ ] SMS template management (TR/EN/RU/AR/DE)
- [ ] Multi-language message support

#### 7.2 SMS Templates
- [ ] Reservation confirmed
- [ ] Payment received
- [ ] Reservation cancelled
- [ ] Pickup reminder (24h before)
- [ ] Return reminder (24h before)
- [ ] Deposit released

#### 7.3 Email Notifications
- [ ] SMTP configuration
- [ ] Email templates (HTML)
- [ ] Reservation confirmation email
- [ ] Payment receipt
- [ ] Cancellation confirmation

#### 7.4 Background Job Processing
- [ ] `background_jobs` table (TDD Section 7)
- [ ] Worker service implementation
- [ ] Job types:
  - SendSmsJob
  - SendEmailJob
  - ProcessPaymentWebhookJob
  - ReleaseExpiredHoldJob
  - DailyBackupJob
- [ ] Retry mechanism with exponential backoff
- [ ] Dead letter queue for failed jobs

#### 7.5 Audit Logging
- [ ] AuditLog entity
- [ ] Automatic audit on critical actions:
  - Reservation created/cancelled
  - Payment processed/refunded
  - Vehicle status changed
  - Admin actions
- [ ] Audit log viewing (SuperAdmin)

#### 7.6 Feature Flags
- [ ] Feature flag system
- [ ] Admin panel for toggling features
- [ ] Feature flags:
  - `EnableOnlinePayment`
  - `EnableSmsNotifications`
  - `EnableCampaigns`
  - `EnableArabicLanguage`
  - `MaintenanceMode`

### ✅ Kabul Kriterleri
- [ ] SMS'ler 5 saniye içinde gönderiliyor (queue'dan)
- [ ] Background job success rate > 99%
- [ ] Audit log tüm kritik işlemleri kaydediyor
- [ ] Feature flag değişiklikleri anında etkili oluyor

---

## 🔷 FAZ 8: Frontend Development - Hafta 15-18

### 🎯 Hedefler
- Next.js 16 App Router projesi
- 5 dil desteği (i18n)
- Public website
- Admin panel
- RTL desteği (Arapça)

### 📋 Görevler

#### 8.1 Project Setup
- [x] Next.js 16 project initialization
- [x] TypeScript configuration
- [x] Tailwind CSS setup
- [x] next-intl configuration
- [x] Folder structure (App Router)

#### 8.2 i18n Implementation
- [x] 5 language message files
- [x] Language switcher component
- [x] URL-based locale routing (`/tr/`, `/en/`, etc.)
- [x] RTL support for Arabic
- [x] Date/number localization

#### 8.3 Public Website

**Pages:**
- [x] **Home Page:**
  - Hero section with search form
  - Featured vehicles
  - Why choose us
  - FAQ
  - Contact info

- [x] **Search Results Page:**
  - Filter sidebar (office, dates, group)
  - Vehicle group cards
  - Pricing display
  - Availability indicator

- [x] **Vehicle Detail Page:**
  - Vehicle images gallery
  - Features list
  - Pricing details
  - Book now button

- [x] **Booking Flow (4 Steps):**
  1. Select dates & office
  2. Select vehicle group
  3. Customer information form
  4. Payment (3D Secure redirect — UI complete, payment provider pending)

- [x] **Reservation Tracking Page:**
  - Public code input
  - Reservation status display
  - Timeline view

- [x] **Static Pages:**
  - About us
  - Contact
  - Terms & Conditions
  - Privacy Policy

#### 8.4 Admin Panel

**Layout:**
- [x] Sidebar navigation
- [x] Header with user info
- [x] Breadcrumb navigation

**Pages:**
- [x] **Dashboard:**
  - Today's pickups/returns
  - Active reservations count
  - Revenue stats
  - Recent bookings

- [x] **Reservation Management:**
  - Reservation list (filters, search)
  - Reservation detail view
  - Cancel/Refund actions
  - Vehicle assignment
  - Check-in/Check-out

- [x] **Fleet Management:**
  - Vehicle list
  - Vehicle add/edit form
  - Vehicle groups
  - Maintenance calendar
  - Office management

- [x] **Pricing Management:**
  - Seasonal pricing rules
  - Campaign codes
  - Airport fees

- [x] **User Management:**
  - Customer list
  - Admin users (SuperAdmin only)
  - Role management

- [x] **Reports:**
  - Revenue reports
  - Occupancy reports
  - Popular vehicles

- [x] **Settings:**
  - Feature flags
  - Audit logs
  - System settings

#### 8.5 Components Library
- [x] Button variants
- [x] Form inputs (with validation)
- [x] Date/time picker
- [x] Modal dialogs
- [x] Toast notifications
- [x] Data tables (with pagination)
- [x] Charts (recharts)

#### 8.6 State Management
- [x] React Context for global state
- [x] SWR for API data
- [x] Local storage for cart/reservation state

#### 8.7 API Integration
- [x] API client setup (fetch)
- [x] Error handling
- [x] Loading states
- [x] Optimistic updates
- [x] Backend API integration (all admin modules connected to real .NET backend)

### ✅ Kabul Kriterleri
- [ ] Lighthouse score > 90 (Performance, Accessibility) — deferred to Faz 10
- [ ] All pages load < 3s
- [ ] Mobile responsive design
- [x] All 5 languages functional
- [x] RTL layout correct for Arabic
- [ ] 3D Secure flow works end-to-end — deferred until payment provider selected

---

## 🔷 FAZ 9: Infrastructure & Deployment - Hafta 17-19

### 🎯 Hedefler
- VPS ortamı kurulumu
- Production Docker yapılandırması
- SSL/TLS sertifikaları
- Monitoring ve alerting

### 📋 Görevler

#### 9.1 VPS Setup
- [ ] Ubuntu 22.04 LTS kurulumu
- [ ] SSH key-only authentication
- [ ] Firewall (UFW) yapılandırması
- [ ] Fail2ban kurulumu
- [ ] Docker & Docker Compose kurulumu

#### 9.2 Docker Production Configuration
- [ ] Multi-stage Dockerfiles
- [ ] `docker-compose.prod.yml`
- [ ] Environment variables (.env)
- [ ] Volume mounts for persistence
- [ ] Network isolation

#### 9.3 Nginx Configuration
- [ ] Reverse proxy yapılandırması
- [ ] Host-based routing (domain.com vs admin.domain.com)
- [ ] Gzip compression
- [ ] Rate limiting zones
- [ ] SSL/TLS configuration

#### 9.4 SSL/TLS
- [ ] Let's Encrypt certbot setup
- [ ] Auto-renewal configuration
- [ ] HTTP to HTTPS redirect
- [ ] Security headers

#### 9.5 Database
- [ ] PostgreSQL production tuning
- [ ] Automated daily backups
- [ ] Backup rotation (30 days)
- [ ] Restore procedure testing

#### 9.6 Monitoring (MVP)
- [ ] UptimeRobot or Pingdom setup
- [ ] Docker health checks
- [ ] Log aggregation (basic)
- [ ] Disk space alerts

#### 9.7 Deployment Script
- [ ] Automated deployment script
- [ ] Zero-downtime deployment (blue/green opsiyonel)
- [ ] Database migration automation
- [ ] Rollback procedure

### ✅ Kabul Kriterleri
- [ ] Site HTTPS ile erişilebilir
- [ ] SSL sertifikası A+ rating
- [ ] Otomatik yedekleme çalışıyor
- [ ] Deployment < 5 dakika
- [ ] Health check endpoint'leri çalışıyor

---

## 🔷 FAZ 10: Testing & Launch - Hafta 19-20

### 🎯 Hedefler
- Kapsamlı test coverage
- Güvenlik audit
- Production launch

### 📋 Görevler

#### 10.1 Unit Tests
- [ ] Domain entity tests
- [ ] Service logic tests
- [ ] Repository tests (in-memory DB)
- [ ] Target: > 70% coverage

#### 10.2 Integration Tests
- [ ] API endpoint tests
- [ ] Database integration tests
- [ ] Redis integration tests
- [ ] Payment provider mock tests

#### 10.3 E2E Tests
- [ ] Booking flow test
- [ ] Payment flow test
- [ ] Admin operations test
- [ ] Cypress or Playwright

#### 10.4 Load Testing
- [ ] Availability query performance
- [ ] Concurrent booking simulation
- [ ] API load test (k6 or Artillery)
- [ ] Target: 100 concurrent users

#### 10.5 Security Audit
- [ ] OWASP Top 10 check
- [ ] SQL injection testing
- [ ] XSS testing
- [ ] Authentication bypass testing
- [ ] Dependency vulnerability scan

#### 10.6 UAT (User Acceptance Testing)
- [ ] Internal team testing
- [ ] Beta customer testing
- [ ] Bug fixes
- [ ] Performance optimization

#### 10.7 Launch Preparation
- [ ] Production data seeding
- [ ] Admin user creation
- [ ] Payment provider production credentials
- [ ] SMS provider production credentials
- [ ] SSL certificates
- [ ] DNS configuration
- [ ] Monitoring alerts

#### 10.8 Go-Live
- [ ] Soft launch (limited traffic)
- [ ] Full launch
- [ ] Post-launch monitoring
- [ ] Issue response plan

### ✅ Kabul Kriterleri
- [ ] All tests passing
- [ ] Security scan clean
- [ ] Performance targets met
- [ ] UAT sign-off
- [ ] Go-live checklist complete

---




## 🔐 Güvenlik Kontrol Listesi

- [ ] HTTPS everywhere
- [ ] JWT token expiration (24h)
- [ ] Password hashing (BCrypt)
- [ ] Rate limiting on all endpoints
- [ ] SQL injection prevention (EF Core parameterized queries)
- [ ] XSS prevention (input validation, output encoding)
- [ ] CSRF tokens for state-changing operations
- [ ] Webhook signature verification
- [ ] PII masking in logs
- [ ] No credit card data storage
- [ ] Admin routes protected by middleware
- [ ] RBAC enforcement on all admin endpoints
- [ ] Security headers (HSTS, CSP, X-Frame-Options)
- [ ] Dependency vulnerability scanning

---

## 📊 Başarı Metrikleri (Success Metrics)

| Metric | Target |
|--------|--------|
| API Response Time (p95) | < 300ms |
| Payment Success Rate | > 95% |
| Booking Completion Rate | > 70% |
| System Uptime | > 99% |
| Error Rate | < 2% |
| Double Booking Incidents | 0 |
| Cache Hit Rate | > 80% |
| Test Coverage | > 70% |

---

## 🚨 Riskler ve Mitigasyon

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Payment provider integration issues | High | Medium | Early POC, mock provider fallback |
| 3D Secure complexity | Medium | High | Thorough testing, clear error messages |
| Double booking in high concurrency | Critical | Medium | DB transactions, row locking, testing |
| Redis failure | Medium | Low | DB fallback mode implemented |
| Turkish localization complexity | Low | Medium | Professional translation service |
| RTL layout issues | Low | Medium | Extensive testing on Arabic |
| Performance with large dataset | Medium | Medium | Proper indexing, caching strategy |

---

## 📚 Dokümantasyon Referansları

Bu plan aşağıdaki dokümanlara dayanmaktadır:

1. **01_PRD_ENTERPRISE_FULL.md** - Product Requirements Document
2. **02_ADR_ENTERPRISE_FULL.md** - Architecture Decision Record
3. **03_TDD_ENTERPRISE_FULL.md** - Technical Design Document
4. **04_IDD_ENTERPRISE_FULL.md** - Infrastructure & Deployment Document
5. **05_Runbook_ENTERPRISE_FULL.md** - Production Runbook
6. **06_Security_Compliance_ENTERPRISE_FULL.md** - Security & Compliance
7. **07_API_Contract_ENTERPRISE_FULL.md** - API Contract (OpenAPI)
8. **08_Error_Spec_ENTERPRISE_FULL.md** - Error Handling Specification

---

## 📝 Notlar

- Bu plan iterative (yinelenen) bir yaklaşım içerir. Her faz tamamlandığında review ve feedback alınmalıdır.
- Frontend ve Backend geliştirme paralel yürütülebilir.
- Payment integration erken başlamalıdır (en karmaşık kısım).
- Performance testing production benzeri veri ile yapılmalıdır.
- Security audit her faz sonrası yapılmalıdır.

---

**Son Güncelleme:** 02 Mart 2026  
**Hazırlayan:** Atlas (AI Orchestrator)  
**Durum:** İncelenmeye hazır
