# Odemesiz Rezervasyon ve Admin Manuel Rezervasyon Tasarimi

## Summary

Odeme entegrasyonu kapaliyken musteriler web sitesinden odeme yapmadan rezervasyon talebi olusturabilecek. Bu talep 24 saat boyunca stok bloklayacak, admin onaylarsa kesin rezervasyona donusecek. Odeme entegrasyonu acikken de odemesiz talep opsiyonu ikincil secenek olarak kalacak. Admin ayrica web sitesi hic kullanilmadan telefonla gelen rezervasyonlari fiziksel arac, tarih araligi ve musteri bilgisiyle dogrudan rezerve edebilecek.

Bu tasarimin temel ayrimi sudur:

- Public web akisi: `Talep Alindi` durumunda 24 saatlik stok bloklu talep.
- Admin telefon akisi: `Onaylandi/Rezerve` durumunda kesin rezervasyon.
- Online odeme akisi: `EnableOnlinePayment` acildiginda mevcut odeme adimi yeniden gorunur, fakat odemesiz talep ikincil secenek olarak sunulur.

## Goals

- Odeme entegrasyonu hazir degilken rezervasyon akisini durdurmamak.
- Odeme seceneklerini admin tarafindan acilip kapatilabilir yapmak.
- UI gizleme ile yetinmeyip backend tarafinda da odeme baslatmayi feature flag ile korumak.
- Web talebi, admin onayi ve telefon rezervasyonunu stok cakismasi yaratmadan ayni rezervasyon modeli icinde yonetmek.
- `Paid` ile odemesiz ama onayli rezervasyonu karistirmamak.

## Non-Goals

- Ilk surumde kredi karti, banka karti, PayPal veya havale gibi yontemleri ayri ayri acilip kapanabilir yapmak.
- Ilk surumde manuel rezervasyon icin tam public booking wizard'ini admin paneline tasimak.
- Ilk surumde zorunlu manuel tahsilat kaydi, kasa hareketi veya muhasebe modulu eklemek.
- Ilk surumde musterinin kendi talebini online olarak onay/iptal etmesini saglamak.

## Decisions

- `EnableOnlinePayment=false` default davranis olarak korunur.
- Public booking step 4 odeme kapaliyken odeme metodu ve kart alanlari gostermeyecek.
- Backend payment intent endpoint'i `EnableOnlinePayment=false` iken istekleri reddedecek.
- Web'den odemesiz tamamlanan rezervasyon yeni `UnpaidRequest` durumuyla olusacak.
- `UnpaidRequest` UI etiketi `Talep Alindi` olacak.
- `UnpaidRequest` 24 saat stok bloklayacak.
- Admin onayladiginda rezervasyon yeni `Confirmed` durumuna gececek.
- `Confirmed` UI etiketi `Onaylandi/Rezerve` olacak.
- `Confirmed`, finansal olarak `Paid` degildir.
- `Confirmed` rezervasyonlar ilk surumde odeme alinmadan check-in yapilabilecek.
- `Confirmed` check-in sirasinda basarili online odeme yoksa depozito on provizyonu atlanacak; rezervasyon yine `Active` durumuna gececek.
- Telefonla gelen admin manuel rezervasyonu dogrudan `Confirmed` olarak olusacak.
- Admin manuel rezervasyonda fiziksel arac secilecek.
- Manuel toplam tutar opsiyonel olacak; girilmezse backend mevcut fiyatlama motoruyla hesaplanan toplam tutari saklayacak.
- Manuel musteri e-postasi opsiyonel olacak; e-posta yoksa backend telefon hash'inden turetilmis internal placeholder e-posta ile mevcut required/unique `Customer.Email` kisitini koruyacak.
- 24 saatlik public talep suresi Redis/payment hold ile degil, `reservations.unpaid_request_expires_at_utc` benzeri kalici DB alaniyla izlenecek.

## Reservation Status Model

Yeni statuslar:

- `UnpaidRequest`: Public web'den odeme yapmadan gelen, 24 saat stok bloklayan talep.
- `Confirmed`: Admin tarafindan onaylanmis veya telefonla manuel olusturulmus kesin rezervasyon.

Mevcut statuslar korunur:

- `Draft`
- `Hold`
- `PendingPayment`
- `Paid`
- `Active`
- `Completed`
- `Cancelled`
- `Expired`

Stok bloklayan durumlar:

- `Hold`
- `UnpaidRequest`
- `PendingPayment`
- `Paid`
- `Confirmed`
- `Active`

Stok bloklamayan durumlar:

- `Draft`
- `Cancelled`
- `Expired`
- `Completed`

Gecerli yeni gecisler:

- `UnpaidRequest -> Confirmed`
- `UnpaidRequest -> Cancelled`
- `UnpaidRequest -> Expired`
- `Confirmed -> Active`
- `Confirmed -> Cancelled`
- `Paid -> Active`
- `Active -> Completed`

`Confirmed` ve `Paid` ayni operasyonel check-in yolunu kullanabilir; fark finansal anlamdadir. `Paid` basarili online tahsilati, `Confirmed` ise odemesiz ama admin tarafindan kesinlestirilmis rezervasyonu temsil eder.

## Public Web Flow

### Online Payment Disabled

1. Kullanici arac, tarih, ekstra ve musteri bilgilerini girer.
2. Step 4 ekraninda odeme metotlari, kart alanlari ve PayPal/debit secenekleri gosterilmez.
3. Kullanici sartlari kabul edip rezervasyonu tamamlar.
4. Frontend `POST /api/v1/reservations/unpaid-requests` endpoint'ini cagirir.
5. Backend transaction icinde fiziksel araci secer, cakisma kontrolunu yapar ve rezervasyonu `UnpaidRequest` olarak olusturur.
6. Backend `UnpaidRequestExpiresAtUtc = now + 24 saat` degerini saklar.
7. Stok blogu Redis hold kaydiyla degil, `UnpaidRequest` statusu ve PostgreSQL overlap constraint'i ile saglanir.
8. Confirmation ekraninda musterinin talebinin alindigi ve aracin 24 saat bloke edildigi acikca soylenir.
9. Admin 24 saat icinde talebi onaylayabilir veya iptal edebilir.
10. Sure dolarsa worker talebi otomatik expire eder ve stok acilir.

### Online Payment Enabled

1. `EnableOnlinePayment=true` oldugunda mevcut odeme secenekleri yeniden gorunur.
2. Kart odemesi icin payment intent akisi birincil CTA olarak calisir.
3. Kullanici isterse "Odeme yapmadan talep gonder" ikincil aksiyonunu secebilir.
4. Basarili odeme rezervasyonu odeme akisi kurallarina gore `Paid` veya ilgili mevcut duruma tasir.
5. Odeme yapmadan talep gonderme secilirse `UnpaidRequest` akisi aynen kullanilir.

## Admin Web Request Flow

Web'den gelen `UnpaidRequest` rezervasyon detayinda admin icin iki ana aksiyon sunulur:

- `Onayla`: Rezervasyon `Confirmed` olur, `UnpaidRequestExpiresAtUtc` temizlenir, stok bloklu kalir.
- `Iptal Et`: Rezervasyon `Cancelled` olur, `UnpaidRequestExpiresAtUtc` temizlenir, stok acilir.

Admin onayi `Paid` statusu uretmez. Odeme alinmadiysa rezervasyon onayli olabilir ama finansal olarak odenmis sayilmaz.

Check-in davranisi:

- `Confirmed` rezervasyon check-in yapilabilir.
- Check-in sonrasi rezervasyon `Active` olur.
- Basarili online odeme intent'i yoksa depozito on provizyonu olusturulmaz ve bu durum hata sayilmaz.
- Ileride manuel tahsilat/depozito modulu eklenirse bu kural yeniden degerlendirilecek.

## Admin Manual Reservation Flow

Telefonla gelen rezervasyon icin admin panelinde manuel rezervasyon olusturma akisi eklenir.

Admin form alanlari:

- Fiziksel arac secimi
- Alis ofisi
- Iade ofisi
- Alis tarih/saat
- Iade tarih/saat
- Musteri ad
- Musteri soyad
- Musteri telefon
- Musteri e-posta, opsiyonel
- Not, opsiyonel
- Toplam tutar, opsiyonel

Backend davranisi:

1. Admin manuel rezervasyon istegi admin auth/policy arkasinda calisir.
2. Tarih araligi ve fiziksel arac zorunlu dogrulanir.
3. Sistem ayni fiziksel arac icin stok bloklayan statuslarda cakisan rezervasyon var mi kontrol eder.
4. Cakisma varsa istek reddedilir.
5. Cakisma yoksa rezervasyon `Confirmed` olarak olusur.
6. Telefon rezervasyonu icin 24 saatlik expiry uygulanmaz.
7. E-posta girilmis ise musteri mevcut normalized e-posta ile bulunur veya olusturulur.
8. E-posta girilmemis ise telefon normalize edilir; telefon hash'inden `manual-<hash>@internal.rentacar.local` formatinda internal placeholder e-posta uretilir.
9. Placeholder e-posta UI'da gercek musteri e-postasi gibi gosterilmez; admin detayinda bos/opsiyonel e-posta olarak ele alinir.
10. Toplam tutar girilirse manuel quoted total olarak `TotalAmount` alanina yazilir.
11. Toplam tutar girilmezse mevcut pricing service kullanilarak toplam hesaplanir ve `TotalAmount` bos birakilmaz.
12. Alis ve iade ofisleri rezervasyon uzerinde kalici olarak saklanir; sadece aracin mevcut ofisinden turetilmez.
13. Olusturma aksiyonu audit log'a yazilir.

## Feature Flag Model

Ilk surumde tek anahtar kullanilir:

- `EnableOnlinePayment`

Admin Feature Flags sayfasinda bu flag'in aciklamasi is kullanicisina net olacak sekilde guncellenir:

> Online odeme seceneklerini public rezervasyon aksinda gosterir. Kapaliyken musteriler odeme yapmadan 24 saat stok bloklu talep olusturur.

Public frontend bu flag'i admin feature flag listesinden dogrudan okumaz. Public API yalnizca guvenli, dar bir `onlinePaymentEnabled` boolean'i expose eder. Boylece public client admin flag listesini veya diger operasyonel flag'leri ogrenmez.

Gelecekte genisletilebilecek flag'ler:

- `EnableCreditCardPayment`
- `EnableDebitCardPayment`
- `EnablePayPalPayment`
- `EnableBankTransferPayment`

Bu yontem bazli flag'ler ilk surum kapsaminda degildir.

## Backend Changes

- `ReservationStatus` enum'una `UnpaidRequest` ve `Confirmed` eklenir.
- EF Core migration olusturulur.
- `Reservation` entity'sine `UnpaidRequestExpiresAtUtc` eklenir.
- `Reservation` entity'sine kalici `PickupOfficeId` ve `ReturnOfficeId` alanlari eklenir; mevcut kayitlar aracin ofisiyle backfill edilir.
- Reservation status mapping, DTO ve API response sozlesmeleri yeni statuslari destekler.
- Stok cakisma sorgulari ve FleetService bloklayici status listesi guncellenir.
- Reservation overlap index veya raw SQL filtreleri yeni statuslari kapsar.
- Public odemesiz talep icin `POST /api/v1/reservations/unpaid-requests` endpoint'i eklenir; mevcut 15 dakikalik payment hold akisini kullanmadan atomik sekilde `UnpaidRequest` olusturur.
- 24 saatlik odemesiz talep suresi `UnpaidRequestExpiresAtUtc` ile mevcut 15 dakikalik payment hold suresinden ayri tutulur.
- Worker `Status = UnpaidRequest` ve `UnpaidRequestExpiresAtUtc <= now` kayitlarini `Expired` durumuna tasir.
- Admin onay endpoint'i `UnpaidRequest -> Confirmed` gecisini yapar.
- Admin iptal endpoint'i `UnpaidRequest -> Cancelled` gecisini yapar.
- Admin manuel rezervasyon endpoint'i ayri bir request DTO ile eklenir.
- Admin manuel rezervasyon customer resolver'i e-postali ve e-postasiz telefon musterisi kurallarini ayri ele alir.
- Check-in akisi `Paid` veya `Confirmed` rezervasyonu kabul eder.
- Depozito on provizyonu basarili online odeme yoksa skip sonucuyla doner ve check-in'i engellemez.
- PaymentService, `EnableOnlinePayment=false` iken payment intent olusturmaz.
- Public API, frontend'e sadece `onlinePaymentEnabled` bilgisini verir.
- Admin aksiyonlari audit log'a yazilir.

## Frontend Changes

- Public booking step 4 runtime feature flag okuyacak.
- Next.js App Router tarafinda runtime ayarlar build-time cache'e gomulmeyecek; feature flag fetch'i no-store veya kisa revalidate stratejisiyle yapilacak.
- `EnableOnlinePayment=false` iken:
  - Odeme metodu radio grubu gosterilmez.
  - Kart alanlari gosterilmez.
  - Submit butonu odeme degil talep tamamlama metni kullanir.
  - Confirmation sayfasi `Talep Alindi` mesajini gosterir.
- `EnableOnlinePayment=true` iken:
  - Online odeme birincil aksiyon olarak kalir.
  - Odeme yapmadan talep gonderme ikincil aksiyon olarak gosterilir.
  - Ikincil aksiyon payment intent cagirmadan `UnpaidRequest` olusturur.
- Admin Feature Flags sayfasinda `EnableOnlinePayment` aciklamasi netlestirilir.
- Admin rezervasyon detayinda `UnpaidRequest` icin `Onayla` ve `Iptal Et` aksiyonlari gosterilir.
- Admin rezervasyon detayinda `Confirmed` icin check-in aksiyonu gosterilir.
- Admin manuel rezervasyon olusturma formu eklenir.
- Admin manuel rezervasyon formunda e-posta opsiyonel kalir; backend placeholder e-postayi UI'a gercek e-posta gibi yansitmaz.
- Admin manuel rezervasyon formunda toplam tutar opsiyonel kalir; bos birakilirsa hesaplanan toplam gosterilir.
- Admin rezervasyon liste/detail status etiketleri `UnpaidRequest` ve `Confirmed` icin guncellenir.
- i18n mesajlari en az `tr`, `en`, `de`, `ru`, `ar` dosyalarinda tamamlanir.

## Data Integrity and PostgreSQL Notes

- Fiziksel arac + tarih araligi cakisma kontrolu transaction icinde calismalidir.
- PostgreSQL exclusion constraint nihai stok korumasi olarak kabul edilir; uygulama sorgusu erken hata mesaji icin kullanilir ama tek guvence degildir.
- Stok bloklayan status listesi tek kaynakta toplanmalidir; farkli servislerde kopya liste tutulursa drift riski vardir.
- Mevcut sistemde `ReservationStatus` string olarak saklandigi icin enum sirasi veri anlamini bozmaz; yine de yeni degerler sona eklenmelidir.
- `reservations_no_overlap` exclusion constraint'i drop/recreate edilerek `Hold`, `UnpaidRequest`, `PendingPayment`, `Paid`, `Confirmed`, `Active` durumlarini kapsamalidir.
- `idx_reservations_active_dates` gibi partial index filtreleri yeni operasyonel kesin/bloklayici statuslari kapsayacak sekilde guncellenmelidir.
- Worker sorgusu icin `status = 'UnpaidRequest' AND unpaid_request_expires_at_utc IS NOT NULL` partial index'i eklenmelidir.
- `UnpaidRequest` kayitlarinda `UnpaidRequestExpiresAtUtc` dolu, diger statuslarda temiz olacak sekilde uygulama kurali ve mumkunse DB check constraint'i eklenmelidir.
- Exclusion violation (`23P01`) kullaniciya cakisma hatasi olarak cevrilmelidir; ham PostgreSQL hatasi API'den sizdirilmemelidir.
- Buyuk tablo varsayimi olmasa bile constraint/index degisikligi production'da kilit etkisi acisindan kontrol edilmelidir.
- Raporlarda `Confirmed` operasyonel rezervasyon/occupancy/popular vehicle metriklerine dahil edilebilir, ancak gelir/odeme toplamlarina sadece basarili `PaymentIntent` tutarlari dahil edilir.

## Security and Abuse Controls

- UI gizleme guvenlik siniri sayilmaz; payment intent endpoint'i backend flag kontrolu yapmalidir.
- Admin manuel rezervasyon endpoint'i yalnizca admin auth/policy ile erisilebilir olmalidir.
- Public odemesiz talep endpoint'i rate limit altinda kalmalidir.
- Public odemesiz talep opsiyonu online odeme acikken de abuse riskini tasir; ayni strict rate limit ve idempotency korumasi kullanilmalidir.
- Musteri girdileri mevcut DTO validation kurallariyla dogrulanmalidir.
- Telefon rezervasyonu not alanlari log veya audit ciktilarinda hassas veri tasimayacak sekilde ele alinmalidir.
- E-postasiz manuel musteriler icin uretilen internal placeholder e-posta PII icermemeli; telefonun raw degeri placeholder icinde kullanilmamalidir.
- Public API yalnizca `onlinePaymentEnabled` gibi dar bilgi expose etmeli; admin feature flag listesi public client'a acilmamalidir.
- Admin aksiyonlari audit log ile izlenmelidir.
- 24 saatlik blok kotuye kullanima acik olabilir; gelecekte telefon/e-posta dogrulama veya talep limiti eklenebilir.

## Test Plan

### Backend Unit Tests

- `EnableOnlinePayment=false` iken payment intent olusturma reddedilir.
- `EnableOnlinePayment=true` iken mevcut payment intent davranisi korunur.
- Public odemesiz rezervasyon `UnpaidRequest` olusturur.
- `UnpaidRequest` 24 saat expiry bilgisiyle stok bloklar.
- `UnpaidRequest` expiry bilgisi `UnpaidRequestExpiresAtUtc` alaninda saklanir.
- `UnpaidRequest` cakisan arac/tarih icin yeni rezervasyonu engeller.
- Admin onay `UnpaidRequest -> Confirmed` gecisini yapar.
- Admin iptal `UnpaidRequest -> Cancelled` gecisini yapar ve stok acilir.
- `Confirmed` rezervasyon odeme intent'i olmadan check-in yapilabilir ve `Active` olur.
- `Confirmed` check-in basarili online odeme olmadiginda depozito on provizyonunu skip eder.
- Admin manuel rezervasyon cakismasizsa `Confirmed` olusturur.
- Admin manuel rezervasyon cakismaliysa reddedilir.
- Admin manuel rezervasyonda e-posta bos ise internal placeholder e-posta uretilir ve musteri kaydi olusur.
- Admin manuel rezervasyonda toplam tutar bos ise pricing service toplam tutari hesaplar.
- `Confirmed` stok bloklayan status listesinde yer alir.

### Backend Integration Tests

- PostgreSQL reservation overlap sorgusu `UnpaidRequest` ve `Confirmed` durumlarini dikkate alir.
- PostgreSQL exclusion constraint `UnpaidRequest` ve `Confirmed` cakismalarini engeller.
- Worker 24 saat dolan `UnpaidRequest` kayitlarini `Expired` durumuna tasir.
- Admin manuel rezervasyon endpoint'i auth olmadan erisilemez.
- Audit log admin onay, iptal ve manuel rezervasyon olusturma aksiyonlarini kaydeder.

### Frontend Tests

- Booking step 4 odeme kapaliyken odeme seceneklerini gostermez.
- Booking step 4 odeme acikken mevcut kart/odeme testleri calismaya devam eder.
- Booking step 4 odeme acikken odemesiz talep ikincil aksiyonu gosterir.
- Odeme kapaliyken submit create reservation cagirir, payment intent cagirmaz.
- Odeme acikken ikincil odemesiz talep aksiyonu payment intent cagirmaz.
- Confirmation sayfasi `Talep Alindi` ve 24 saat blok mesajini gosterir.
- Admin Feature Flags sayfasi `EnableOnlinePayment` toggle'ini gunceller.
- Admin rezervasyon detayinda `UnpaidRequest` icin onay/iptal butonlari gorunur.
- Admin rezervasyon detayinda `Confirmed` icin check-in aksiyonu gorunur.
- Admin manuel rezervasyon formu validasyon, basari ve cakisma hatalarini gosterir.

### E2E Smoke

- Public odemesiz rezervasyon talebi olusturma.
- Online odeme acikken odemesiz talep opsiyonunu kullanma.
- Admin web talebini onaylama.
- Onayli odemesiz rezervasyonu check-in yapma.
- Admin telefon rezervasyonu olusturma.
- Ayni arac/tarih icin ikinci manuel rezervasyonun reddedilmesi.

## Rollout Plan

1. Backend status, office alanlari ve `UnpaidRequestExpiresAtUtc` migration'ini ekle.
2. PostgreSQL overlap constraint ve partial index filtrelerini yeni statuslarla guncelle.
3. Feature flag backend enforcement ve public `onlinePaymentEnabled` exposure ekle.
4. Public odemesiz rezervasyon endpoint/servis akisini ekle.
5. Worker expiry davranisini `UnpaidRequestExpiresAtUtc` uzerinden ekle.
6. Admin web talebi onay/iptal aksiyonlarini ekle.
7. `Confirmed` check-in davranisini ekle.
8. Admin manuel rezervasyon endpoint ve UI ekle.
9. Frontend i18n ve confirmation metinlerini tamamla.
10. Backend, frontend ve E2E smoke testlerini calistir.
11. Dokuman ve gate notlarini guncelle.

## Open Risks

- 24 saatlik public talep bloklari yogun sezonda stok kilitleyebilir; ileride admin ayariyla sure yonetimi gerekebilir.
- Online odeme acikken odemesiz talep opsiyonunun acik kalmasi conversion ve abuse etkisi yaratabilir; metin ve rate limit kanitlari izlenmelidir.
- E-postasiz telefon musterilerinde internal placeholder e-posta MVP icin pratik cozumdur, fakat uzun vadede nullable e-posta ve normalize telefon kimligi daha temiz model olabilir.
- Manuel rezervasyonda toplam tutar pricing service veya admin override ile saklansa bile finansal tahsilat kaniti sayilmaz; ileride manuel odeme kaydi eklenebilir.
- Telefon rezervasyonunda musteri dogrulama ilk surum disinda kalir.

## Acceptance Criteria

- Admin `EnableOnlinePayment` kapaliyken public odeme secenekleri gorunmez.
- Payment intent endpoint'i flag kapaliyken backend tarafinda reddeder.
- Web kullanicisi odeme yapmadan talep olusturabilir.
- Online odeme acikken web kullanicisi isterse odeme yapmadan talep olusturabilir.
- Web talebi 24 saat stok bloklar.
- Web talebinin 24 saatlik suresi kalici expiry alaniyla izlenir.
- Admin web talebini onaylayinca rezervasyon `Confirmed` olur.
- Admin web talebini iptal edince stok acilir.
- 24 saat dolan web talebi otomatik `Expired` olur ve stok acar.
- Admin `Confirmed` rezervasyonu odeme almadan check-in yapabilir.
- Admin telefonla gelen rezervasyonu fiziksel arac ve tarih araligiyla manuel olusturabilir.
- Admin telefon rezervasyonunda e-posta girmeden musteri olusturabilir; internal placeholder e-posta UI'da musteri e-postasi gibi gosterilmez.
- Admin telefon rezervasyonunda toplam tutar girmezse backend hesaplanan toplam tutari saklar.
- Ayni fiziksel arac icin cakisan tarih araliginda ikinci rezervasyon olusturulamaz.
- `Confirmed` ile `Paid` UI, backend ve rapor anlaminda karistirilmaz.
