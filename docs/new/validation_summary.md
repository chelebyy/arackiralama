# Codex Security Bulgulari Guncellik Dogrulamasi

Tarih: 2026-07-12
Checkout: `ad6dcb7d359d66c4fc3d97fd6094f6667d02d4bb`
Karsilastirilan uzak dal: `origin/main` (`5e36a7f5de5f4f55cb08c2d7c77babd9c3365646`)
Kaynak rapor: `codex-security-findings-2026-07-12T16-07-36.816Z.csv`

## Sonuc

Yedi rapor satirinin yedisi de guncel kodda ilgili kok kontrol veya riskli davranis mevcut oldugu icin yasamaya devam ediyor. Iki PII kaydi ve iki odeme kaydi birbiriyle ortusen kok nedenlere sahip olsa da rapordaki her aday ayri ayri kontrol edildi. Secret bulgusunda depo ici ifsa halen kesin; anahtarlarin servis tarafinda iptal edilip edilmedigi yerel depodan belirlenemez.

## Dogrulama rubrigi

- [x] Raporun isaret ettigi route/config/artifact guncel HEAD'de mevcut mu?
- [x] Saldirgan girdisi ile hassas sink arasinda eksik kontrol devam ediyor mu?
- [x] Yakin kodda iddiayi bozan auth, sahiplik, imza veya production fail-closed kontrolu var mi?
- [x] Ayni durum guncel `origin/main` agacinda da var mi?
- [x] Mumkun olan davranislar mevcut odakli testlerle calistirildi mi?

## Kapanis tablosu

| Aday | Kok kontrol | Giris / sink | Disposition | Yasiyor | Guven |
|---|---|---|---|---|---|
| `05d3e4b9686c819191d93d5eda4dfc0a` Guest email claim | `CustomerAuthController.cs:32-99` | Dogrulanmamis e-posta -> mevcut passwordless Customer'a parola yazma | reportable | evet | yuksek |
| `a6b2507e06dc8191a6ac5903fe47d4ea` Public driver PII | `ReservationsController.cs:121-138`, `ReservationDtos.cs:5-43,62-71` | Public code -> genis internal DTO | reportable | evet | yuksek |
| `1654270941dc8191b5b11c8c4914c536` Production mock provider | `PaymentOptions.cs:3-17`, `ServiceCollectionExtensions.cs:113-124`, `docker-compose.yml:60-74` | Eksik provider config -> Mock fallback | reportable | evet | yuksek |
| `3f489d713edc81919d8ecd72c4cf1d9c` Secret artifacts | `.ship-safe/context.json`, `ship-safe-report.html` | Redakte edilmemis tarayici eslesmeleri -> Git tarafindan izlenen raporlar | historical repository-hygiene finding | hayir | yuksek artefakt riski; Resend/Upstash adaylari credential degil |
| `151e6b3c84008191953fd8b2022ef645` Forged 3DS | `PaymentsController.cs:45-67`, `PaymentService.cs:132-178`, provider `VerifyPaymentAsync` | Public BankResponse -> reservation `Paid` | reportable | evet | yuksek |
| `ca53a46832248191b84683a09238392d` Dependabot auto-merge | `.github/workflows/dependabot-auto-merge.yml:3-69` | Bot patch/minor PR -> write token ile auto approve/merge | reportable | evet | yuksek (politika riski) |
| `6bf4756eec7081919ff3de3060492da7` Public PII and cancellation | `ReservationsController.cs:121-138,201-214`, `ReservationDtos.cs:5-43` | Public code/GUID -> PII okuma veya iptal | reportable | evet | yuksek |

## Kanit ozeti

### 1. Kritik misafir hesap ele gecirme

`Register` anonimdir. Normalized e-posta ile mevcut musteri bulunuyor; `HasPassword` false ise e-posta sahipligi kanitlanmadan yeni parola hash'i ayni kayda yaziliyor. Mevcut `Register_WhenGuestCustomerExists_UpgradesExistingCustomer` testi bu davranisi dogruluyor. Sonraki customer endpoint'leri CustomerOnly policy ve token subject ile calissa da token yanlis kisi tarafindan ayni CustomerId icin alinabildiginden bu kontroller kok sorunu gidermiyor.

### 2 ve 7. Public rezervasyon ifsasi ve iptal

Public `GET /api/v1/reservations/{publicCode}` auth/sahiplik kontrolu olmadan `ReservationDto` donduruyor. DTO e-posta, telefon, plaka, musteri istatistikleri, surucu dogum tarihi ve ehliyet alanlarini tasiyor. Public `POST /api/v1/reservations/{reservationId}/cancel` de auth, oturum veya sahiplik kontrolu olmadan servis iptalini cagiriyor. Ayrica guvenli customer route'u mevcut, fakat guvensiz public sibling route'u kaldigi icin counterevidence degil.

### 3 ve 5. Mock production ve sahte 3DS

Provider varsayilani `Mock`; DI taninmayan veya eksik provider degerinde Mock'a dusuyor. Production compose payment provider ayarlarini gecmiyor. Public 3DS return yalnizca BankResponse'un bos olmamasini kontrol ediyor. Mock provider fail/cancel icermeyen yaniti basarili sayiyor; Iyzico provider ise banka/provider dogrulamasi yapmadan her yaniti `Succeeded` donduruyor. PaymentService bu sonucu dogrudan rezervasyon `Paid` durumuna yaziyor. Odakli payment testleri dahil toplam 8 test basarili oldu ve mevcut davranisi dogruladi.

### 4. Secret artefaktlari

Ilk tarama aninda `.ship-safe/context.json` ve `ship-safe-report.html` tracked durumdaydi ve tarayici eslesmelerini redakte etmeden tasiyordu. Bu, gecerli bir repository-hygiene bulgusuydu. Artefaktlar daha sonra Git'ten kaldirildi ve uretim yollari ignore edildi; takip eden kaynak/provenans triage'i eslesmelerin proje credential'i olmadigini gostermistir.

**16 Temmuz 2026 provider-candidate triage ek notu:** Resend bicimli aday `frontend/tsconfig.tsbuildinfo` ignored/generated cache dosyasina kadar izlendi. Bu kaynak dosya Git'e hic eklenmemisti; aday commit agacinda yalnizca `.ship-safe/context.json` ve `ship-safe-report.html` icinde, tarayicinin kopyaladigi bulgu olarak bulunuyordu. Kaynak/env/deploy/hesap bagi yoktur ve depo sahibi Resend kurulmadigini dogrulamistir. Uc Upstash bicimli aday ise `.dotnet/.dotnet/TelemetryStorageService/*.trn` dosyalarindaki Base64 satirlarinin icinde bulunan rastgele alt dizilerdi. Satirlar Base64'ten cozuldugunde gzip (`1F8B`) ve ardindan Application Insights JSON elde edildi; eslesen dizilerin hicbiri acilmis telemetri metninde yoktu. Upstash paket/env/kaynak/deploy bagi da bulunmadi. Her iki provider adayi icin sonuc `not_actionable`; rotasyon veya provider log incelemesi gerekmiyor. Asil gecerli sorun tarama artefaktlarinin eslesen degeri redakte etmeden Git'e eklenmesiydi.

### 6. Dependabot

Workflow halen `pull_request_target`, `contents: write`, `pull-requests: write` kullaniyor ve patch/minor Dependabot PR'larinda `gh pr merge --auto --squash` calistiriyor. PR branch checkout gorulmedigi icin klasik `pull_request_target` kod-calistirma zinciri bu dosyada kanitlanmadi; raporun asil insan dependency review'unu kaldirma/tedarik zinciri riski ise guncel.

## Calistirilan dogrulama

`dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore` komutu ilk incelemede ilgili auth/payment filtreleriyle calistirildi: 8/8 basarili. Sonraki odakli dogrulama tam backend ve entegrasyon paketlerini de kapsamistir. Gercek Iyzico hesabi kullanilmadi; Resend/Upstash icin canli token denemesi yapilmadi, cunku statik provenans triage'i eslesmelerin proje credential'i olmadigini gostermistir.

## Sinirlar

- Bu calisma kaynak agaci, mevcut testler, deploy config ve tracked artifact kanitina dayanir.
- Canli production konfiguru incelenmedi; production'da elle `Payment__Provider` verilmis olabilir. Ancak repository'nin production deploy varsayilani yine guvensizdir.
- Secret degerlerinin servis tarafinda rotate edilip edilmedigi bilinmiyor; degerler repoda kalmaya devam ediyor.
- `origin/main`, HEAD'den bir commit ileride olsa da guvenlikle ilgili incelenen dosyalarda fark yoktur.
