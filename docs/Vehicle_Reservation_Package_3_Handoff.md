# Paket 3 — oturum devir belgesi

## 2026-10-17 — Combined PR 457 validation

Combined backend code commit `277aaa8e494a47baa5fd4c458fd00a07fdcfd286`, based on published `7ccc51206e13ac6bae799d114d838b9d99231a13`, includes the reminder recovery correction and group quote declaration validation. Both corrections were independently reviewed, combined without conflicts, and validated together before this documentation-only evidence update.

New group quote creates now require a declaration that matches the quoted driver age and the current vehicle group's minimum age and licence tenure, with both validity/document confirmations accepted. The existing declaration validator runs inside the quote claim's release-on-error scope, so a rejected request can be corrected and retried. Exact-vehicle validation, persisted group replays and legacy unquoted creation retain their existing contracts; group booking conditions remain absent. Before correction, the group regressions reproduced 14 invalid unit cases and 12 invalid API cases being accepted. The final suite covers rejection, corrected retry, minimum eligibility, persisted replay and legacy controls for both public create endpoints.

| Validation | Exact scope | Result |
|---|---|---|
| Backend Release solution build | Combined code `277aaa8` | Zero errors; 13 existing package/Redis warnings |
| Full backend test project | Combined code `277aaa8` | 943 passed, zero failed or skipped |
| Full API integration project | Combined code `277aaa8`; isolated PostgreSQL 16.15 and Redis 7 | 202 passed, zero failed or skipped |
| Frontend CI | Published `7ccc512`; frontend files unchanged by the combined corrections | 372 tests in 73 files passed; lint zero errors and one existing warning; production build generated 146 pages |
| Guest-management browser suite | Published `7ccc512`; controlled API responses; Chromium 149.0.7827.55 | 34 passed: 17 desktop and 17 Pixel 5 cases; zero failures, skips or retries |

These are completed runs with distinct provenance. The browser run covers guest challenge cooldown/reload, session/consent reset, localized statuses, cancellation and amendment interaction. It does not execute the payment-flow, reservation-extra-options or full E2E suites; those booking selector updates were checked against the current form controls. Exact-head remote CI on the published combined result remains a separate gate and uses PostgreSQL 17.

The combined follow-ups introduce no schema or dependency changes. The earlier declaration migration was applied only by isolated test fixtures; production migrations and deployment were not performed. Existing Crypto.Xml/Pkcs package advisories remain tracked in [PR #461](https://github.com/chelebyy/arackiralama/pull/461). Reminder dispatch retains the documented final-check-to-provider-send race below; already active provider calls are not interrupted. All PRs remain subject to human review, with no merge or automatic merge performed.

## 2026-10-17 — Reminder recovery after reservation changes

A further regression reproduced the remaining reminder recovery window: a worker could claim a reminder and crash less than five minutes before a guest cancellation or date change. The fresh `Processing` job survived the reservation transaction and later sent the old email and SMS when recovered. Both real-database cancellation and amendment cases failed before the correction, delivering two obsolete reminders each.

The notification processor now checks the current reservation before rendering and immediately before sending pickup and return reminders in either channel. Missing or cancelled reservations and reminders created before a later applied guest amendment are skipped without consuming retries. The check uses immutable `CreatedAt`, because provider retries change `ScheduledAt`. Amendment application records `AppliedAt` before constructing its replacement jobs in the same transaction, so replacements remain eligible; unapplied quotes do not invalidate reminders. Access and transaction notice templates retain their existing behavior.

Validation: the Release solution build completed with zero errors, all 927 backend tests passed, and all 29 real-database guest integration cases passed against isolated PostgreSQL 16.15 and Redis 7. The new coverage includes both reminder templates and channels, valid provider retries, unapplied quotes, cancellation during rendering, the fresh-claim recovery window, and successful delivery of all four replacement jobs after amendment. The integration reminder fixture now uses the production job-type constant so the worker exercises the email path. The 184-case full API integration result below predates this additional correction; exact-head remote CI remains a separate gate using PostgreSQL 17.

This correction adds no migration and does not interrupt an already active provider call. A reservation commit after the final database check can still race an external send; the database transaction and provider delivery are not atomic. No merge or deployment is included.

## 2026-10-17 — Independent declaration persistence

The remaining declaration-loss finding is corrected. Group-based checkout and manual reservations can have no booking conditions or pricing snapshot, so storing driver declarations only inside booking conditions discarded their input. `Reservation.DriverDeclaration` now persists the four declaration fields independently in a nullable `driver_declaration` JSONB column. Creation and admin updates write it; guest date changes replace it with the accepted declaration and retain the previous value in amendment history. Exact-vehicle holds and operator responses retain a fallback to declarations already stored inside booking conditions. Pricing snapshots, assignment rules and historical sensitive fields are not repurposed.

The initial regression run reproduced five failures returning a null declaration. After the correction, the targeted reservation/guest-policy suite passed 118 tests and the full backend Release test project passed 913 tests with zero failures or skips. The full API integration suite passed 184 tests against isolated PostgreSQL 16.15 and Redis 7 instances, including the amendment declaration round trip. An older missing-declaration fixture was updated to clear both stores; all 11 focused legacy/independent declaration hold cases passed, including two new compatibility cases.

The EF pending-model check passed, and the generated SQL for `20261017171429_PersistReservationDriverDeclaration` contains only the nullable column addition and migration-history entry. Integration fixtures applied migrations only to their isolated test databases. Exact-head remote CI remains a separate gate and uses PostgreSQL 17. NuGet restore still reports existing `System.Security.Cryptography.Xml` and transitive `Pkcs` advisories on this branch; their patch is tracked in the existing [backend dependency PR #461](https://github.com/chelebyy/arackiralama/pull/461).

The migration is additive and has not been applied to production. Existing rows retain their original data and use the read fallback where applicable. No merge, automatic merge or deployment is included.

## PR 457 review follow-up

The latest three findings are corrected: pending verification survives same-tab reload with the original cooldown; amendments recreate eligible SMS reminders; and all ten backend statuses are localized. Validation: 910 backend unit tests, 27 PostgreSQL/Redis guest integration cases, 372 frontend tests, 34 desktop/mobile browser cases, production build and lint passed (one existing lint warning). Browser API responses are controlled; production provider delivery is not covered. Remote checks must be read from the final commit; no merge or deployment is part of this review.

The six Codex findings and two React Doctor warnings are addressed: shared local API/Worker keys; client resend cooldown; authenticated per-client BFF rate-limit partitions; declaration-based E2E inputs; allowlisted operator driver summaries; expired-mail cancellation; memoized formatters; and upstream status checks before body parsing. See [deployment boundaries](Vehicle_Reservation_Package_3_Data_Handling.md) for the required trusted ingress/secret configuration. Existing publication evidence below predates these corrections; use the latest PR head/checks for closure. No merge or deployment is authorized by this review task.

The next Codex pass identified the Worker's missing ASP.NET shared runtime and rollout rejection of version-2 checkout holds. Both are corrected, with a real Worker container startup and nine legacy/current-version negative and positive database cases. Six outdated mail fixtures from the first CI run are updated; the full 910-test backend Release suite passed. See [review validation](Vehicle_Reservation_Package_3_Implementation.md#pr-457-review-validation) for scope and remaining deployment checks.

## Durum ve çalışma alanı

Further review corrections retire superseded unverified challenges, skip their queued mail, cancel abandoned reminders after reservation changes, and reset consent/form state on logout or access denial. Validation: 910 Release unit tests, 23 real guest integration tests, 20 desktop/mobile guest browser scenarios and lint passed (one existing lint warning). Exact-head remote checks remain the publication gate.

Paket 3 yerel uygulaması ve kapsamlı yerel doğrulaması tamamlandı. [PR #457](https://github.com/chelebyy/arackiralama/pull/457) 28 Eylül 2026 tarihinde `main` hedefiyle açıldı. İlk uygulama commit'i `706b7ab`; sonraki belge/biçim düzeltmeleri aynı PR'dedir. Uzak CI sonucu bu belgede başarılı kabul edilmez; son PR commit'i üzerinden kontrol edilmelidir.

- Dal: `codex/guest-reservations`.
- Çalışma ağacı: `C:\Users\muham\.codex\worktrees\guest-reservations\Araç Kiralama`.
- Doğrulanmış uzak varsayılan dal: `main`; başlangıç commit'i `1a352c984972a3c6a705b10073563eca470b92fc` (Paket 2, PR #447).
- 28 Eylül yeniden fetch kontrolünde uzak `main`, yerel `main` ve özellik dalının başlangıcı aynı commit'tedir.
- Ana çalışma dizini `C:\All_Project\Araç Kiralama`, ayrı `codex/docs-db-ops-phase0-doc-contract` dalındaki ilgisiz çalışmayı içerir; değiştirilmemelidir.
- Merge, canlı dağıtım, gerçek müşteriye e-posta ve tarihsel veri silme yetkisi verilmedi; yapılmadı.

## Kesinleşen kararlar

İptal ve tarih değişikliği için izin, ön süre ve ücretleri admin tanımlar. Eksik, kapalı veya tamamlanmamış ayarda ilgili işlem kapalı kalır. Varsayılan ücretsiz iptal kuralı eklenmemelidir.

Kısa rezervasyon formu iletişim bilgisi ve sürücü beyanı toplar. Kimlik/ehliyet numarası ve tam doğum tarihi yeni rezervasyon/profil işlemlerinde bağlanmaz veya yazılmaz; eski kayıtlar korunur. Kamuya açık header/hero kaynakları değişmedi.

## Uygulama haritası

| Alan | Dosyalar / sorumluluk |
|---|---|
| Misafir erişimi ve işlemler | `backend/src/RentACar.API/Services/GuestReservationService.cs`, `Controllers/GuestReservationsController.cs` |
| Kural ve fiyat doğrulama | `VehicleBookingService.cs`, `ReservationQuoteService.cs`, `ReservationService.cs`, `Core/Entities/OfficeOperatingPolicy.cs` |
| Erişim/geçmiş tabloları | `20260927141937_GuestReservationManagement` migration, `GuestReservationAccess.cs`, `GuestReservationConfiguration.cs` |
| E-posta işleme | `GuestReservationMail.cs`, `NotificationBackgroundJobProcessor.cs`, `SmtpEmailProvider.cs`, Infrastructure `DependencyInjection.cs` |
| Tarayıcı oturumu | `frontend/app/api/guest/[...path]/route.ts`; HttpOnly oturum çerezi, CSRF ve aynı origin kontrolü |
| Müşteri ekranları | `manage-reservation/page.tsx`, rezervasyon adım 3/4, takip/onay bağlantıları, `guest-reservation-copy.ts` |
| Admin kuralları | `frontend/components/admin/dialogs/OperatingPolicyEditor.tsx` |

Tarih değişikliği güncel araç/ek hizmet fiyatlarıyla hesaplanır; önceki kabul edilmiş değişiklik ücretleri korunur. Yeni ücret ve toplam açıkça kabul edilir. Sürüm, politika, teklif süresi ve stok kontrolleri transaction içinde tekrar yapılır. Tekrar gönderimde aynı sonuç döner; ücret ve bildirim işi tekrar oluşturulmaz.

New exact-reservation replay proofs use version **3**; pricing quote schema remains **2**. Existing version-2 drafts may create or recover holds only with their original session and stored eligibility, while current expiry and rental-policy checks still apply.

## Ölçülmüş yerel kanıt

27 Eylül uygulama oturumunda:

- Backend: 896 birim testi geçti; localhost SMTP teslim testi dahil.
- Gerçek izole PostgreSQL/Redis: 70 mevcut teklif/hold testi ve ayrı çalıştırmada 19 misafir/iş kuyruğu testi geçti (89 farklı test).
- Frontend: 71 dosyada 359 Vitest testi geçti; üretim Webpack derlemesi ve TypeScript kontrolü geçti.
- Son üretim derlemesinde 12 Chromium masaüstü/mobil senaryosu geçti: beş dil, Arapça RTL, iptal, sayfa yenileme, açık tutar kabulü ve süresi dolmuş erişim.
- ESLint: 0 hata, `SearchForm.test.tsx` içinde önceden mevcut 1 uyarı.
- EF bekleyen model değişikliği kontrolü geçti; eklemeli migration izole test veritabanlarına uygulandı.

Tarayıcı testleri kontrollü API yanıtları kullanır. Gerçek PostgreSQL/Redis ve localhost SMTP kanıtları ayrı testlerdir; dağıtılmış tarayıcı–BFF–API–Worker–SMTP zincirinin tek uçtan uca kabulü yapılmadı. Bu devir oturumunda ürün kodu değiştirilmedi; önceki testler yeni çalıştırma olarak sunulmamalıdır.

## Çalıştırma ve devam noktası

Backend birim testleri `backend/tests/RentACar.Tests`; API senaryoları `backend/tests/RentACar.ApiIntegrationTests/Endpoints/GuestReservationTests.cs` ve `ReservationQuoteEndpointTests.cs` altındadır. Frontend BFF testleri route yanında; tarayıcı senaryoları `frontend/e2e/tests/guest-management.spec.ts` içindedir.

Önceki izole test PostgreSQL portu 55439, Redis portu 56379, Next portu 3313 idi. Bu test servisleri iş bitiminde durduruldu. PostgreSQL verisi ana dizinin `.tmp/package3-postgres` yolunda, Redis container adı `rentacar-package3-redis`; yeniden kullanmadan önce süreç/port sahipliğini doğrulayın. Paylaşılan servisleri durdurmayın veya verileri silmeyin.

1. PR'nin son commit'ini ve her CI işinin gerçek sonucunu kontrol edin; önceki yerel başarıyı uzak CI sonucu saymayın.
2. Misafir erişimi, CSRF, eşzamanlı iptal/tarih değişikliği ve mali geçmiş için odaklı bağımsız güvenlik incelemesi yapılabilir. Kapsamlı uygulama güvenliği veya üretim güvenliği kanıtlanmış değildir.
3. Canlı öncesi admin kuralları, saklama/silme sorumluluğu, korumalı ortak anahtar deposu ve geri yükleme kabulünü tamamlayın.
4. API ve Worker aynı `RentACar.GuestReservations` Data Protection adı ve kalıcı `GuestAccess:KeyRingPath` kullanmalıdır. Sertifika koruması `GuestAccess:CertificateThumbprint` ile yapılandırılır; anahtar/sertifika geri yükleme ve döndürme provası gerekir.
5. SMTP teslimi en az bir kez garantisi modelindedir; belirsiz teslim/çökme durumunda çift e-posta mümkündür. Rezervasyon işlemi idempotent kalır. Gerçek sağlayıcı teslimi, HTTPS ve proxy/rate-limit kabulü ayrı kapılardır.
6. Sonraki geliştirme Paket 4'tür; korunan header/hero dışında sayfa sadeleştirme ve gerçek işletme metinleri. Paket 3 release gereklilikleri ayrıca izlenmelidir.

## İlgili belgeler

- [Uygulama ve doğrulama sınırları](Vehicle_Reservation_Package_3_Implementation.md)
- [Plan ve kabul edilen karar](Vehicle_Reservation_Package_3_Plan.md)
- [Veri ve anahtar yaşam döngüsü](Vehicle_Reservation_Package_3_Data_Handling.md)
- [Yalnızca sayım yapan envanter SQL'i](Vehicle_Reservation_Package_3_Inventory.sql)
- [Yol haritası](gelistirme-yol-haritasi.md)
- [Yerel kabul kontrol listesi](13_Local_Docker_Browser_Test_Checklist.md)
- [Admin uygulama kaydı](15_Admin_UX_Refresh_Implementation.md)
