# Codex Sentinel Security Review (Faz 1-7) ve Faz 8-10 Güvenlik Kapıları

**Proje:** Araç Kiralama Platformu (Alanya Rent A Car)  
**Hazırlayan:** Codex Sentinel review pass  
**Tarih:** 23.03.2026  
**Kapsam:** Tamamlanan Faz 1-7 kod incelemesi + kalan Faz 8-10 güvenlik planı

---

## 1) İnceleme Kapsamı ve Durum

- [x] Kaynak plan dokümanları doğrulandı: `docs/09_Implementation_Plan.md`, `docs/10_Execution_Tracking.md`
- [x] Tamamlanan fazlar doğrulandı: Faz 1-7 (`Completed`)
- [x] Güvenlik açısından yüksek riskli yüzeyler incelendi:
  - Kimlik doğrulama/oturum
  - Ödeme ve webhook
  - Arka plan işleri (worker/command execution)
  - Rate limiting ve cookie ayarları
- [ ] Faz 8-10 kod incelemesi (henüz kodlanmadı)

---

## 2) Faz 1-7 Güvenlik Bulguları (Checklist)

### 2.1 [HIGH] Placeholder JWT secret production’da yanlışlıkla kullanılabilir

- [x] Bulgu tespit edildi
- [ ] Düzeltme tamamlandı
- [ ] Test/kanıt eklendi

**Referanslar**
- `backend/src/RentACar.API/appsettings.json` (Jwt.Secret): `CHANGE_THIS_TO_A_32_CHAR_MINIMUM_SECRET`
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs:83`

**Risk**
- Zayıf/tahmin edilebilir secret ile token sahteciliği riski (yanlış config yönetiminde).

**Önerilen aksiyon**
- Placeholder değerleri startup’ta hard-fail et.
- Production için env/secret store zorunlu kıl.

---

### 2.2 [MEDIUM] Register akışında kullanıcı varlığı davranış farkı ile sızıyor

- [x] Bulgu tespit edildi
- [ ] Düzeltme tamamlandı
- [ ] Test/kanıt eklendi

**Referanslar**
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs:46`
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs:77`

**Risk**
- Email enumeration (mevcut kullanıcıda farklı response davranışı).

**Önerilen aksiyon**
- Mevcut/yeni kullanıcı senaryolarında aynı response paterni.
- İç denetim için generic audit log.

---

### 2.3 [MEDIUM] Job dedup kontrolünde `Payload.Contains(...)` kullanımı

- [x] Bulgu tespit edildi
- [ ] Düzeltme tamamlandı
- [ ] Test/kanıt eklendi

**Referanslar**
- `backend/src/RentACar.API/Services/PaymentService.cs:214`
- `backend/src/RentACar.API/Services/PaymentService.cs:219`
- `backend/src/RentACar.Worker/Worker.cs:79`

**Risk**
- Yanlış eşleşme nedeniyle duplicate kararının hatalı verilmesi ve iş atlama riski.

**Önerilen aksiyon**
- String arama yerine normalize alanlar (`ProviderEventId`, `ReservationId`) ve unique index.

---

### 2.4 [MEDIUM] Worker backup komutu config’den çalıştırılıyor, stdout tam loglanıyor

- [x] Bulgu tespit edildi
- [ ] Düzeltme tamamlandı
- [ ] Test/kanıt eklendi

**Referanslar**
- `backend/src/RentACar.Worker/Worker.cs:305`
- `backend/src/RentACar.Worker/Worker.cs:354`

**Risk**
- Komut kötüye kullanım yüzeyi ve loglarda hassas veri sızıntısı.

**Önerilen aksiyon**
- Allowlist komut politikası.
- Argüman doğrulama.
- stdout/stderr redaction.

---

### 2.5 [LOW] Webhook imza doğrulama hatası için HTTP semantiği

- [x] Bulgu tespit edildi
- [ ] Düzeltme tamamlandı
- [ ] Test/kanıt eklendi

**Referanslar**
- `backend/src/RentACar.API/Controllers/PaymentsController.cs:118`

**Risk**
- Düşük: güvenlik/operasyon gözlemlenebilirliği için sınıflama netliği azalır.

**Önerilen aksiyon**
- Signature fail için `401/403` standardizasyonu.

---

## 3) Faz 1-7 Aksiyon Backlog’u (Issue Checklist)

### Issue-1 `security(auth): block placeholder/weak JWT secret at startup`
- [ ] Placeholder secret hard-fail
- [ ] Env/secret-store zorunluluğu
- [ ] CI config guard testi
**Owner:** Backend  
**Hedef tarih:** 27.03.2026

### Issue-2 `security(auth): make register response enumeration-safe`
- [ ] Tek tip response
- [ ] Audit log
- [ ] Enum testi
**Owner:** Backend  
**Hedef tarih:** 28.03.2026

### Issue-3 `security(payments): replace payload string matching with typed dedup keys`
- [ ] Şema güncellemesi (typed alanlar)
- [ ] Unique index + migration
- [ ] Dedup testleri
**Owner:** Backend + DBA  
**Hedef tarih:** 02.04.2026

### Issue-4 `security(worker): harden backup command execution and logs`
- [ ] Allowlist
- [ ] Arg validation
- [ ] Log redaction
- [ ] Least privilege runtime
**Owner:** Backend + DevOps  
**Hedef tarih:** 03.04.2026

### Issue-5 `security(api): return 401/403 for webhook signature failures`
- [ ] Controller response code standardizasyonu
- [ ] Monitoring kural güncellemesi
**Owner:** Backend  
**Hedef tarih:** 29.03.2026

---

## 4) Faz 8-10 Güvenlik Planı (Kullanım Kılavuzu)

## Faz 8 (Frontend Development) - Security Gate Checklist

- [ ] CSP + güvenli response header seti
- [ ] Kritik input noktalarında validation + encoding
- [ ] Admin route authz bypass testleri
- [ ] XSS smoke test paketi
- [ ] Auth cookie davranışı (`HttpOnly/Secure/SameSite`) e2e doğrulama

## Faz 9 (Infrastructure & Deployment) - Security Gate Checklist

- [ ] Secret yönetimi: repo içi secret sıfır
- [ ] CI security gates: SAST + dependency + image scan
- [ ] Nginx/TLS hardening (HSTS dahil)
- [ ] DB/Redis ağ segmentasyonu
- [ ] Backup/restore güvenli prosedür doğrulaması

## Faz 10 (Testing & Launch) - Security Gate Checklist

- [ ] Threat model güncellemesi
- [ ] Auth/session + webhook replay + rate-limit bypass testleri
- [ ] Incident runbook + rollback tatbikatı
- [ ] Release security gate: kritik/yüksek açık kapatılmadan go-live yok

---

## 5) Operasyonel Kullanım Önerisi (En Uygun Yöntem)

- [x] Bu dokümanı **tek kaynak** (source of truth) olarak kullan.
- [x] `docs/10_Execution_Tracking.md` içinde bu dosyaya referans tut.
- [x] `docs/09_Implementation_Plan.md` içinde Faz 8-10 için güvenlik kapısı referansı tut.
- [x] Her faz sonunda bu dosyadaki checklist durumlarını güncelle.

**Neden bu yöntem?**
- Faz bazlı ilerleme (`10_Execution_Tracking`) bozulmaz.
- Plan dokümanı (`09_Implementation_Plan`) sade kalır.
- Güvenlik detayları tek dosyada izlenir ve sürdürülür.

---

## 6) Kapsam Dışı / Notlar

- Bu rapor, tamamlanan Faz 1-7 kod yüzeyi için odaklı bir güvenlik incelemesidir.
- “Tam güvenli” veya “prod-safe kesin” iddiası içermez.
- İncelenmeyen yeni kodlar için aynı checkpoint yaklaşımı Faz 8-10 boyunca tekrar uygulanmalıdır.

---

## 7) Faz 8 Başlatma Checklist Snapshot (Kopyala/Kullan)

**Snapshot Tarihi:** 23.03.2026  
**Faz:** 8 - Frontend Development  
**Durum:** Başlatmaya Hazır

### 7.1 Başlangıç Kapısı

- [ ] Security referans dokümanı okundu (`docs/11_Codex_Sentinel_Phase1_7_Security_Report_and_Phase8_10_Gates.md`)
- [ ] Faz 8 kapsamındaki ekran/akış listesi netleştirildi
- [ ] Frontend auth boundary (public/admin/customer) netleştirildi
- [ ] Güvenlik test kapsamı (XSS, authz, CSRF etkisi) tanımlandı

### 7.2 Uygulama Kapısı (Development)

- [ ] CSP taslağı çıkarıldı ve uygulandı
- [ ] Güvenli header seti doğrulandı
- [ ] Input validation + output encoding kritik formlarda uygulandı
- [ ] Admin route authz guardları uygulandı
- [ ] Hassas veriler client loglarında maskelendi

### 7.3 Doğrulama Kapısı (Verification)

- [ ] XSS smoke testleri geçti
- [ ] Authz bypass denemeleri başarısız (beklenen)
- [ ] Cookie davranışı (`HttpOnly/Secure/SameSite`) beklentiyle uyumlu
- [ ] Güvenlik bulguları/aksiyonlar bu dokümana işlendi

### 7.4 Faz 8 Çıkış Kapısı (Exit Criteria)

- [ ] High/Critical açık yok
- [ ] Medium bulgular için aksiyon planı oluşturuldu
- [ ] `docs/10_Execution_Tracking.md` faz notu güncellendi
- [ ] Faz 9 başlangıcı için infra güvenlik kapıları devredildi

---

## 8) Faz 9 Başlatma Checklist Snapshot (Kopyala/Kullan)

**Snapshot Tarihi:** 23.03.2026  
**Faz:** 9 - Infrastructure & Deployment  
**Durum:** Başlatmaya Hazır

### 8.1 Başlangıç Kapısı

- [ ] Faz 9 mimari kapsamı (VPS/Docker/Nginx/CI/CD/Monitoring) netleştirildi
- [ ] Secret yönetim modeli (env/secret store) kararlaştırıldı
- [ ] Deployment ortamları (dev/stage/prod) ayrıştırıldı
- [ ] Rollback stratejisi yazılı hale getirildi

### 8.2 Uygulama Kapısı (Development/Setup)

- [ ] CI security gates aktif: SAST, dependency scan, image scan
- [ ] Secret’lar repo dışı güvenli kaynağa taşındı
- [ ] Nginx/TLS hardening uygulandı (HSTS + modern TLS policy)
- [ ] DB/Redis ağ erişimleri minimum yetki ile sınırlandı
- [ ] Backup komut/işletim politikası (allowlist + redaction) uygulandı

### 8.3 Doğrulama Kapısı (Verification)

- [ ] CI security gate fail durumunda deploy bloklanıyor
- [ ] TLS/header doğrulama testleri geçti
- [ ] Container/image zafiyet sonuçları kabul kriterini sağlıyor
- [ ] Backup/restore dry-run başarıyla tamamlandı

### 8.4 Faz 9 Çıkış Kapısı (Exit Criteria)

- [ ] High/Critical infra security açık yok
- [ ] Medium bulgular için kapanış planı hazır
- [ ] Operasyon runbook güncellendi
- [ ] Faz 10 test/launch güvenlik kapılarına devir yapıldı

---

## 9) Faz 10 Başlatma Checklist Snapshot (Kopyala/Kullan)

**Snapshot Tarihi:** 23.03.2026  
**Faz:** 10 - Testing & Launch  
**Durum:** Başlatmaya Hazır

### 9.1 Başlangıç Kapısı

- [ ] Faz 10 test stratejisi (unit/integration/e2e/load/security) netleştirildi
- [ ] Release candidate kapsamı donduruldu (scope freeze)
- [ ] Threat model güncelleme kapsamı belirlendi
- [ ] Go-live karar kriterleri yazılı onaylandı

### 9.2 Uygulama Kapısı (Testing)

- [ ] Auth/session güvenlik test paketi çalıştırıldı
- [ ] Payment/webhook replay-idempotency testleri çalıştırıldı
- [ ] Rate limit bypass ve abuse senaryoları test edildi
- [ ] Kritik log/PII sızıntısı kontrolleri yapıldı
- [ ] Incident response ve rollback tatbikatı gerçekleştirildi

### 9.3 Doğrulama Kapısı (Verification)

- [ ] Security test raporu üretildi ve arşivlendi
- [ ] High/Critical açıklar kapatıldı
- [ ] Medium riskler için kabul/erteleme kararı kayıt altına alındı
- [ ] UAT ve teknik doğrulama sonuçları tutarlı

### 9.4 Faz 10 Çıkış Kapısı (Go-Live Gate)

- [ ] Go-live security gate geçti
- [ ] Final release notlarına güvenlik özeti eklendi
- [ ] Post-launch izleme/alert eşikleri doğrulandı
- [ ] İlk 72 saatlik operasyon planı hazır
