# Paket 3 — oturum devir belgesi

## PR 457 review follow-up

The six Codex findings and two React Doctor warnings are addressed: shared local API/Worker keys; client resend cooldown; authenticated per-client BFF rate-limit partitions; declaration-based E2E inputs; allowlisted operator driver summaries; expired-mail cancellation; memoized formatters; and upstream status checks before body parsing. See [deployment boundaries](Vehicle_Reservation_Package_3_Data_Handling.md) for the required trusted ingress/secret configuration. Existing publication evidence below predates these corrections; use the latest PR head/checks for closure. No merge or deployment is authorized by this review task.

The next Codex pass identified the Worker's missing ASP.NET shared runtime and rollout rejection of version-2 checkout holds. Both are corrected, with a real Worker container startup and nine legacy/current-version negative and positive database cases. Six outdated mail fixtures from the first CI run are updated; the full 910-test backend Release suite passed. See [review validation](Vehicle_Reservation_Package_3_Implementation.md#pr-457-review-validation) for scope and remaining deployment checks.

## Durum ve çalışma alanı

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

Yeni kesin rezervasyon tekrar kanıtı sürümü **3**, fiyat teklifi şeması **2** olarak kalır. Hold doğrulaması da kanıt sürümü 3 bekler. Eski sürüm girişimleri yeni checkout başlatmalıdır.

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
