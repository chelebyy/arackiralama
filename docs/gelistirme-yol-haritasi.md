# Müşteri deneyimi: kod karşılaştırması ve geliştirme sırası

**PR #447 ikinci inceleme düzeltmeleri:** `efefb21` üzerindeki üç yeni Codex bulgusu giderildi: toplu müsaitlik sorgusu, güncellemede hazırlık süresi çakışma kontrolü ve araca özel teklifte zorunlu sürücü yaşı. 857 backend birim testi ve 32 gerçek PostgreSQL/Redis API testi geçti; 1 ve 50 araç için sorgu sayısı 2 kaldı. Lint hatasız, önceden var olan tek uyarı sürüyor. [Düzeltme kanıtı](Vehicle_Reservation_Package_2_PR447_Review.md); yeni commit'in CI/inceleme sonucu ayrıca kontrol edilmelidir. Bu turda frontend kaynakları değişmedi; önceki tarayıcı kanıtları kendi kapsamını korur. Merge/dağıtım yapılmadı.

## Güncel durum — 26 Eylül 2026

**Paket 2'nin yerel uygulaması ve kapsam içindeki kabul kontrolleri tamamlandı.** Gerçek araç seçimi, araç bazlı fiyat/koşullar ve ek hizmet uygunluğu, grupsuz araç ekleme, zorunlu admin işletme ayarları ve ödeme teslimde alınacak şekilde otomatik kesinleşme uygulandı.

Kullanıcının onayladığı kararlar: Türkiye takvimine göre gün hesabı korunur; mevcut fiyat/koşullar araçlara kopyalanıp araç üzerinden düzenlenir; değişen fiyat/koşullar yeni teklif kabulü gerektirir; geçmiş rezervasyonlar değişmez; ilk sürüm mevcut ofis/teslim kapsamını korur. Ön süre, çalışma saatleri ve hazırlık süresi bilinmediği için gerçek değerleri admin girmelidir. Eksik ayarda otomatik rezervasyon açılmaz.

Doğrulama: **853 backend birim testi**, tam turda **66 PostgreSQL/Redis API entegrasyon testi**, son teklif uç noktası turunda **11 test** geçti. Son tur bir yeni tarihli katalog testi ve genişletilmiş kesinleşmiş rezervasyon süre aşımı kontrolü içerir; toplam 67 farklı API testi doğrulandı. Son tam turda **70 dosyada 332 frontend testi**, ardından yeni hata kurtarma testi dahil **4 onay ekranı testi** geçti; bu turlar toplam 333 farklı testi kapsar. Son üretim derlemesi/TypeScript ve lint geçti (mevcut tek lint uyarısı sürüyor). Veri göçü yalnızca izole test veritabanında denendi; eski rezervasyon tutarı ve fiyat dökümü korundu.

**Tarayıcı kabulü tamamlandı:** Beş dilde onay ekranı, Arapça RTL, telefon/tablet/masaüstü görünümü, klavye akışı, grupsuz araç kaydetme ve rezervasyon, fiyat/koşul değişikliğinde açık teklif kabulü, iki sekmenin aynı araç için yarışması ve başarılı yanıt kaybından sonra aynı rezervasyona dönüş doğrulandı. Çalışma saatleri, kapalı gün, ön süre ve hazırlık sınırları gerçek yerel API ile denendi. Veritabanında üç kesinleşmiş test rezervasyonu ve sıfır ödeme işlemi var. Dil değişiminde seçim kaybı, hata çevirileri, sabit ehliyet tarihleri, formdan geri dönüş ve özet hatasından kurtarma düzeltildi. Header/hero kaynakları değişmedi. Bu sonuç fiziksel cihaz sertifikasyonu veya genel güvenlik denetimi değildir.

[Son kabul kanıtı](Vehicle_Reservation_Package_2_Acceptance.md), [uygulama](Vehicle_Reservation_Package_2_Implementation.md), [plan](Vehicle_Reservation_Package_2_Plan.md), [odaklı güvenlik incelemesi](Vehicle_Reservation_Package_2_Completion_Review.md) ve [kontrol listesi](13_Local_Docker_Browser_Test_Checklist.md) aynı durumu yansıtıyor. Canlı açılış için gerçek işletme ayarları, temsilî veri göçü/yedekten dönüş incelemesi ve yetkilendirilmiş PR/CI/yayın süreci ayrı olarak bekliyor.

Paket 1, [PR #446](https://github.com/chelebyy/arackiralama/pull/446) ile `f1c34fe` commit'inde birleştirilmişti. Paket 2 aynı tabandan `codex/catalogue-next-plan` dalındaki ayrı worktree'de `22e20c8` uygulama commit'iyle kaydedilip [PR #447](https://github.com/chelebyy/arackiralama/pull/447) açıldı. PR'ın güncel commit'i için CI ve inceleme sonucu bekleniyor. Merge, canlı dağıtım veya üretim veri göçü yapılmadı; ana çalışma dizinindeki mevcut işler korundu.

Aşağıdaki ilk plan ve eski incelemeler tarihsel bağlamdır. Paket 2 durumunda bu bölüm ve bağlantılı güncel belgeler önceliklidir.

## İlk planın başlangıç önerisi — tarihsel

İlk geliştirme paketi **gerçek araç kataloğu** olmalı: admin paneline bir aracın gerçek bilgilerini girince müşteri aynı bilgileri liste ve detay sayfasında görmeli. Bunun ardından seçilen aracın fiyat, müsaitlik ve rezervasyon boyunca değişmeden korunmasını tamamlayacağız. Yalnızca sayfadaki “grup” etiketini kaldırmak yeterli değil; mevcut fiyatlandırma ve rezervasyon kodu da gruplara bağlı.

Bu paragraf 24 Eylül uygulama öncesi incelemesini anlatır: o incelemede uygulama kodu değiştirilmedi; test, tarayıcı kabulü, dağıtım veya veri göçü çalıştırılmadı. Paket 1'in sonraki uygulama ve birleşim durumu yukarıdaki güncel durum bölümündedir.

## Korunacak kararlar

- Müşteri belirli bir aracı kiralar. Aynı gruptan başka araç sessizce atanmaz. Hedef admin deneyimi araç üzerinden yönetimdir; grup oluşturmak zorunlu değildir.
- Uygun rezervasyon admin onayı beklemeden kesinleşir. Rezervasyonun kesinleşmesi, ödeme alınmış olması anlamına gelmez.
- Aynı gün rezervasyon mümkündür. Çalışma saatleri, minimum hazırlık süresi ve iki kiralama arasındaki hazırlık aralığı ayrı kurallardır; sayısal değerleri henüz seçilmedi.
- Üyelik yoktur. İletişim için gerekli bilgiler alınır; TC kimlik ve ehliyet numarası web sitesinde tutulmaz. Yaş/ehliyet uygunluğu için gerekli beyan, tam doğum tarihi toplamadan tasarlanır.
- Beş dil korunur. Ana sayfada 3–5 araç gösterilir; bu sayı tüm araç listesinin sınırı değildir.
- **Ekran görüntüsünde korunan üst alan değişmez:** logo, menü, dil seçimi, rezervasyon takip, koyu hero, soldaki yazı/rozet/buton/hizmet ikonları ve sağdaki arama kartının görünümü. Bu bölgedeki mock metinler de bu çalışma kapsamında değiştirilmez.
- Henüz ödeme sağlayıcısı, ücretler, iptal kesintileri, depozito ve sigorta koşulları kesinleşmedi. Bunlar uydurulmaz.
- Canlıya geçiş bu çalışmanın hedefi değildir.

## Kodda doğrulanan farklar

Aşağıdaki yollar incelenen kaynak ağacına göredir. Satırlar inceleme anındaki sürümü gösterir.

| Konu | Mevcut durum ve kanıt | Yapılması gereken |
|---|---|---|
| Araç bilgileri | `backend/src/RentACar.Core/Entities/Vehicle.cs:7` plaka, marka/model/yıl/renk, grup, ofis, durum ve tek fotoğraf tutuyor. Vites, yakıt, koltuk ve bagaj alanları bu modelde yok. | Gerçek araç özellikleri, fotoğraf galerisi ve doğrulanmış donanım için veri modeli oluşturmak. |
| Zorunlu grup | `frontend/components/admin/dialogs/VehicleDialog.tsx:47` grup seçimini zorunlu kılıyor. Yaş, ehliyet süresi ve depozito `VehicleGroup.cs:10` üzerinden geliyor. | Son kullanıcıya grup yönetimi yüklemeyen, ortak koşullar ve araç istisnalarıyla çalışan hedef model. Eski bağımlılıkları aşamalı taşımak. |
| Sabit özellikler | `FeaturedVehicles.tsx:97` 5 koltuk/otomatik/benzin/200 km; `vehicles/page.tsx:50` 2 bagaj/otomatik/benzin; `vehicles/[id]/page.tsx:63` benzer sabit değerler kullanıyor. Detayda `:76` sabit 4,5 puan ve 0 yorum var. | Kaynaktan gelmeyen özellik, puan ve kilometre vaadini kaldırmak. Bilinmeyen bilgiye tahmini değer atamamak. |
| Tarihsiz gezinme | `frontend/app/(public)/[locale]/vehicles/page.tsx:112` ve `vehicles/[id]/page.tsx:101` tarih yoksa Nisan 2025 değerlerini kullanıyor. | Tarihsiz katalog ile tarihli aramayı ayırmak. Tarih seçmeden fiyat/müsaitlik sözü vermemek. |
| Ana sayfa kartları | `frontend/components/public/FeaturedVehicles.tsx:57` zaten 4 araçla sınırlı; `:33` varsayılan tarih/saat ve grup kimliğiyle rezervasyona gönderiyor. | 4 kart sayısı 3–5 kararına uyuyor. Kartların gerçek araç detayına gitmesi ve seçilmiş arama bilgilerinin korunması gerekir. |
| Araç seçiminin kaybı | Liste `vehicles/page.tsx:146`, detay `vehicles/[id]/page.tsx:132` rezervasyona grup kimliği taşıyor. `ReservationService.cs:428` o gruptan uygun araç arayıp bulduğunu atıyor. | Aracın kimliğini baştan sona korumak; seçilen araç dolarsa müşteriye alternatif sunmak, kendi kendine değiştirmemek. |
| Fiyat kaynağı | `PricingRule.cs:5` ve `Contracts/Pricing/ReservationQuoteDtos.cs:12` grup tabanlı. `FleetService.cs:199` liste fiyatını bugün için gruptan hesaplıyor. | Seçilen araç ve tüm kiralama aralığı için sunucuda fiyat hesaplamak; tarihsiz/tarih dışı günlük fiyatı kesin toplam gibi sunmamak. |
| Otomatik kesinleşme | `ReservationsController.cs:100` ödemesiz talebi oluşturuyor; `ReservationService.cs:457` durum `UnpaidRequest`, `:463` süre 24 saat. Ön yüzde `booking/step4/page.tsx:416` bu yola bağlanıyor. | Kesin rezervasyon ve ödeme durumunu ayırmak. Yeni onaysız akışı 24 saatlik talebin yalnızca adını değiştirerek kurmamak. |
| Gereksiz kişisel veri | `Reservation.cs:22` doğum tarihi, `:23` ehliyet numarası alanları; `ReservationService.cs:2278` ve `:2280` bu değerleri kayda aktarabiliyor. `Customer.cs:25` kimlik numarası alanı da var. | Form, istek sözleşmeleri, admin, veri tabanı, bildirim ve kayıt tutma yollarını birlikte sadeleştirmek. Alanın bulunması, gerçek kişiye ait veri bulunduğunu kanıtlamaz. |
| Müşteri API’si | `Contracts/Fleet/PublicVehicleDto.cs:5` plaka içeriyor; `Controllers/VehiclesController.cs:148` gerçek plaka alanını yanıt nesnesine aktarıyor. | Müşteri yanıtlarını açık bir izinli alan listesiyle sınırlamak. Plakayı yalnızca ekrandan gizlemek yeterli olmaz. |

## Yeniden kullanılacak parçalar

- Araç listeleme, araç düzenleme, fotoğraf yükleme ve çok dil altyapısı mevcut; bunların üzerine ilerlenebilir.
- Fiyat teklifi için tutar dökümü, süre, fiyat anlık kaydı ve tekrar kullanım doğrulamaları var. Araç kimliğine geçerken bunlar korunmalı ve yeni bağlama göre test edilmeli.
- İstek tekrarlarını kontrol eden middleware ve `[Idempotent]` kullanımı var. Bunların bulunması tüm eşzamanlılık senaryolarının kanıtlandığı anlamına gelmez.
- PostgreSQL göçünde aynı araç için çakışan stok bloke eden rezervasyonları engelleyen `reservations_no_overlap` kısıtı var: `20260607163225_AddUnpaidRequestAndManualReservationFields.cs:99`. Gerçek PostgreSQL ile korunması doğrulanmalı; bellek içi test tek başına yeterli değildir.
- Bildirim kuyruğu, şablonlar, işleyici ve SMTP sağlayıcısı var. Yeni e-posta sağlayıcısı eklemek için sıfırdan bildirim sistemi yazmak gerekmiyor. Kuyruk ile rezervasyon kaydının hata anındaki tutarlılığı ayrıca sınanmalı.
- `PublicSiteSettings` firma iletişimi, çalışma saatleri ve sayfa içeriklerini merkezileştirebiliyor. Tekrarlanan iletişim bilgileri buradan yönetilebilir.
- `frontend/hooks/useBooking.ts:128` müşteri/sürücü verisini eski kalıcı kayıttan çıkarıyor; `:134` kalıcılaştırılan alanlara bunları dahil etmiyor. Bu koruma korunmalı.
- Rezervasyon takip özeti `ReservationService.cs:244` izinli alanlarla kişisel veriyi daraltıyor. Bunu yeni kişisel yönetim ekranının yetkilendirmesiyle karıştırmamak gerekir.

## Paket 1 — Gerçek araç kataloğu

**Durum (25 Eylül 2026):** Uygulama ve yerel kabul kontrolleri tamamlandı; [PR #446](https://github.com/chelebyy/arackiralama/pull/446) incelemeye açıldı. Uzak CI kontrolleri yayın anında devam ediyor. Merge ve canlıya dağıtım yapılmadı. Grup tabanlı akışın kaldırılması Paket 2 kapsamındadır.

Uygulama, test kanıtları ve sınırlar: [Paket 1 raporu](Vehicle_Catalogue_Package_1_Implementation.md). Devam edecek oturum için: [oturum devir belgesi](handoffs/2026-09-25-catalog-package1-pr-handoff.md).

**Müşteriye etkisi:** Aracın fotoğrafı ve özellikleri güvenilir olur. **İşletmeye etkisi:** Aynı bilgiyi farklı sayfalarda tekrar düzenlemez.

1. Araca vites, yakıt türü, koltuk sayısı, yaklaşık bagaj kapasitesi, doğrulanmış donanım ve sıralı fotoğraflar eklemek. Kasa/kapı bilgisi ile isteğe bağlı motor/güç bilgileri detay içindir. Mevcut tek fotoğraf ilk galeri görseli olarak korunabilir.
2. Admin formuna bu alanları eklemek. Eski araçların bilinmeyen alanlarına otomatik “5 koltuk/benzin/otomatik” yazmamak; eksikleri işletmenin doldurabilmesini sağlamak. Model/yıl ve görseller korunmalı.
3. Admin ve müşteri API yanıtlarını ayırmak. Müşteri yanıtından plakayı çıkarmak; iç notlar, bakım ayrıntıları ve ileride eklenebilecek şasi bilgileri de bu yanıta girmemeli.
4. Liste, kart ve detay görünümünü gerçek verilere bağlamak. Sabit puan/yorum ve doğrulanmamış kilometre vaadini kaldırmak. Donanımı ücretli ekstralardan ayırmak.
5. Tarihsiz doğrudan erişimde eski tarih üretmemek. Araç detayına bakılabilmeli; fiyat ve müsaitlik için tarih seçimi istenmeli. Ana sayfa kartları arama bağlamını koruyarak araç detayına yönlenmeli.
6. Ana sayfa için mevcut 4 kartla ilerlemek. Korumalı üst bölüme dokunmadan, aynı araç bilgisini diğer müşteri ekranlarıyla paylaşmak. Daha kapsamlı alt bölüm düzeni Paket 4’te.

**Bu paketin sınırı:** Grup tabanlı fiyat ve rezervasyon bağlantısı henüz tamamen kalkmaz. Eski ilişkiler geçiş için korunur; tüm grup tablolarını silmek veya her araç için gizli bir grup üretmek kalıcı çözüm olarak kabul edilmez. Grup yönetimi zorunluluğunun tamamen kalkması Paket 2’nin kabul koşuludur. Paket 1 tek başına yeni kiralama akışının tamamlandığı ya da yayına hazır olduğu anlamına gelmez.

**Kabul ölçütleri:**

- Admin’de değiştirilmiş vites/yakıt/koltuk/fotoğraf ilgili kart ve detayda aynı görünür; başka araç etkilenmez.
- Bilinmeyen alanlar yanlış varsayımla doldurulmaz. Eski kayıtlar okunabilir kalır; mevcut rezervasyon ve fiyat kayıtları silinmez.
- Herkese açık API yanıtı plaka içermez. Admin yetkili görünümü çalışır.
- Tarihsiz liste ve detay, eski tarihlere dayanarak fiyat veya “uygun” sonucu üretmez.
- Fotoğraf yükleme yetkisi, dosya türü/boyutu ve geçersiz dosya davranışı kontrol edilir. Galeri silme işlemi referans verilen başka görseli yanlışlıkla silmez.
- Beş dil, Arapça sağdan sola düzen ve telefon/masaüstü görünümü kontrol edilir. Header/hero için görsel karşılaştırma yapılır.

## Paket 2 — Seçilen araç, doğru fiyat ve otomatik rezervasyon

- Fiyat kurallarını, ek hizmet uygunluğunu ve koşulları araç üzerinden yönetmek; ortak işletme varsayımları ve araç istisnaları kullanmak. Admin yeni araç eklemek için grup açmak zorunda kalmamalı.
- Eski grup fiyatlarını/aracın koşullarını kontrollü taşımak. Çok araca ortak kuralların hangi araçlara uygulanacağı açıkça eşlenmeli. Geçmiş rezervasyon tutarı ve kabul edilmiş koşullar sonradan değişmemeli.
- Araç kimliği, teslim/iadeye ilişkin seçimler, tarih/saat, ekstralar ve geçerli fiyat teklifi tüm adımlarda birlikte taşınmalı. Sunucu bunları yeniden doğrulamalı; URL’den gelen fiyat esas alınmamalı.
- Seçilen aracı kesinleştirme anında yeniden kontrol etmek. İki müşteri aynı aracı aynı saatlere isterse yalnızca biri başarılı olmalı; kaybeden akışta bilgiler korunmalı.
- Aynı gün, çalışma saatleri, minimum ön süre ve dönüş sonrası hazırlık aralığını ortak sunucu kurallarıyla yönetmek. İşletme saat dilimi ile UTC dönüşümü tutarlı olmalı.
- Rezervasyon durumu ile ödeme durumunu ayırmak. Ödemesiz modelde uygun rezervasyon otomatik kesinleşebilir; “ödendi” sayılmaz ve eski 24 saatlik talep temizliği tarafından iptal edilmez. Ödeme kararı verilene kadar sağlayıcı seçilmez.
- Dolu araç, fiyat değişimi, yükleme hatası ve gerçek boş sonuç ayrı mesajlarla gösterilmeli. Fiyat değişirse müşteri yeni toplamı yeniden kabul etmeli.

**Kabul ölçütleri:** Aynı grup içindeki farklı iki araç karışmaz; değiştirilmiş teklif reddedilir; çift tıklama/yeniden deneme ikinci rezervasyon üretmez; gerçek PostgreSQL eşzamanlı çakışma testinde tek rezervasyon kabul edilir; bakım/kapalı saat/hazırlık aralığı ihlal edilemez; otomatik onaylı rezervasyon ödeme yapılmadı diye talep süresi dolunca kaybolmaz.

## Paket 3 — Kısa form, üyelik olmadan güvenli yönetim ve e-posta

- Önerilen akış: **iletişim ve gerekiyorsa teslim bilgisi → tüm tutar ve koşulların özeti → kesinleşme sonucu**. Bu iki form adımı uygulama sırasında netleştirilecek çalışma önerisidir.
- Tam ad, e-posta, telefon; teslim türüne göre gerekli adres/uçuş bilgisi. TC kimlik, ehliyet numarası ve tam doğum tarihi toplama yollarını form ve sunucudan kaldırmak; gereksiz veriyi hata kayıtlarına veya e-postaya da taşımamak.
- Eski veri var mı, hangi alanlarda ve hangi yedeklerde bulunduğunu değerleri dışarı dökmeden envanterlemek. Eski kayıtların temizliği ayrı, kapsamı belli bir veri işlemi olmalı; form alanını kaldırmak eski veriyi silmez.
- Saklama/silme süresi, gerekli kişisel alanların şifrelenmesi, anahtarların veri tabanından ayrılması, yetki ve yedek koruması için uygulanabilir tasarım oluşturmak. Şifreleme tek başına veri minimizasyonunun veya erişim kontrolünün yerini almaz.
- Rezervasyonun görüntülenmesi, iptali ve tarih değişikliği üyelik olmadan yapılabilmeli. E-posta doğrulaması ve kısa süreli rezervasyona özel erişim öneridir; erişim yöntemi henüz kesin karar değildir. Rezervasyon numarası tek başına değişiklik yetkisi vermemeli.
- Tarih değişikliği yeni müsaitlik/fiyat doğrulamasıyla atomik yapılmalı. Yeni seçim kabul edilmezse mevcut rezervasyon bozulmamalı.
- Kesinleşme ve değişiklik e-postaları tekrar denenebilir ve mükerrer gönderime dayanıklı olmalı. E-posta arızası başarılı rezervasyonu başarısız gibi göstermemeli.
- Resend düşünülen sağlayıcıdır; hesap, güncel ücretsiz limit, veri işleme/aktarım koşulları ve alan adı ayarları bu incelemede doğrulanmadı. Gönderim etkinleştirilmeden önce değerlendirilir.

**Güvenlik takvimi:** Gereksiz kişisel veri toplama yolları ve şifreleme tasarımı, Paket 1/2 geliştirilirken erken ele alınmalı. Paket 3’ün tamamlanmasını bekleyerek gerçek müşteri verisi toplamaya başlanmamalı.

## Paket 4 — Sayfaları sadeleştirme ve uçtan uca kabul

- Korunan üst alanın altında: araçlar → kısa kiralama adımları → gerçekten sunulan teslim seçenekleri → kısa SSS → iletişim/footer düzeni.
- Hakkımızda gerçek işletmeyi anlatmalı. Korumalı alan dışındaki mock büyüklük iddiaları ve gereksiz tekrarlar temizlenmeli. İletişim bilgileri merkezi kaynaktan gelmeli.
- Araç detayında kiralama şartları formdan önce görülebilmeli: yaş/ehliyet süresi, kilometre, yakıt iadesi, depozito, sigorta kapsamı/istisnaları, teslim ve iptal/değişiklik koşulları. Kesinleşmemiş ücretler uydurulmaz.
- Beş dilde aynı anlam, Arapça düzen, klavye kullanımı, hatadan dönüş, mobil akış ve e-posta bağlantıları kontrol edilmeli.
- Kiralama koşulları ve aydınlatma metni gerçek işleyişe göre hazırlanmalı. Bu belge hukuki uygunluk görüşü veya hazır KVKK metni değildir.

## Kararlar ve işletme aktivasyonu

Paket 2 kararları kapandı: mevcut Türkiye gün hesabı ve fiyat/koşullar korunur; araç bazında düzenleme yapılır; ödeme teslimde alınır ve uygun araç otomatik kesinleşir; fiyat/koşul değişirse yeni teklif kabul edilir; geçmiş kayıtlar korunur. Çalışma saatleri, minimum ön süre ve hazırlık aralığı admin tarafından girilecek zorunlu işletme verileridir. Girilmeden exact araç rezervasyonu alınmaz. Aşağıdaki ilk karar envanterinin Paket 3 kapsamı hâlâ geçerlidir; Paket 2 için güncel karar kaydı bağlantılı plandadır.

| Karar | Gerektiği aşama |
|---|---|
| Gün hesabı, farklı saatlerde iade ve gecikme yaklaşımı | Paket 2 fiyatlama |
| Aynı gün minimum ön süre, çalışma saatleri, iki kiralama arası hazırlık | Paket 2 müsaitlik |
| Ödeme yöntemi ve kesin rezervasyonda müşterinin yükümlülüğü | Paket 2 akış / Paket 3 metinler |
| İptal, gelmeme ve tarih değiştirme koşulları | Paket 3 müşteri yönetimi |
| Depozito, sigorta, yaş/ehliyet, kilometre ve teslim ücretleri | Fiyat/koşul ekranları tamamlanmadan |
| Gerçek teslim bölgeleri ve adres/uçuş bilgisinin ne zaman istendiği | Paket 2–3 |
| Verilerin saklanma/silinme süresi, e-posta sağlayıcısı ve erişim doğrulaması | Paket 3, gerçek veri toplanmadan |

## İlk planlama güvenlik incelemesi — tarihsel

Codex Sentinel planlama yaklaşımıyla, yalnızca incelenen kaynak yolları ve önerilen değişiklikler üzerinden değerlendirildi. Bunlar bir sızma testi sonucu değildir.

| Kimlik / önem / güven | Alan ve kanıt | Risk ve plana eklenen kontrol |
|---|---|---|
| SEC-01 / yüksek / yüksek | Veri minimizasyonu; `ReservationService.cs:2278` ve `:2280`, `Customer.cs:25` | Tasarım hedefinde olmayan kimlik/ehliyet verisi tutulabilir. Form, API, kayıt, admin, bildirim ve eski veri envanteri birlikte ele alınmalı; gereksiz alan gönderen istemci de doğrulanmalı. |
| SEC-02 / orta / yüksek | Açık yanıt kapsamı; `PublicVehicleDto.cs:5`, `VehiclesController.cs:148` | Müşteri için gereksiz plaka alanı API yanıtında bulunur. Paket 1’de kaldırılıp yanıt sözleşmesi testiyle korunmalı. Bu bulgu tek başına bir hesap ele geçirme açığı iddiası değildir. |
| SEC-03 / yüksek / yüksek | Rezervasyon bütünlüğü; grup seçimi, fiyat teklifi ve araç çakışma kısıtı | Araç kimliğine geçiş fiyat/tekrar kullanım/çakışma korumalarını zayıflatabilir. Kimlik, tarih ve ekstra bağlarını test etmek; gerçek PostgreSQL ile eşzamanlı kabulü doğrulamak gerekir. |
| SEC-04 / yüksek / orta | Planlanan misafir değişiklik/iptal yetkisi | Yeni yönetim ekranında başkasının rezervasyonuna işlem yapılmamalı. Süreli rezervasyon erişimi, deneme sınırı, yetkisiz erişim ve tekrar kullanım testleri gerekir. Mevcut sistemde kanıtlanmış açık olarak sunulmuyor. |
| SEC-05 / orta / orta | Yeni galeri ve e-posta kapsamı | Dosya yüklemede yetki/tür/boyut kontrolleri; e-postada minimum veri, güvenli bağlantılar ve yeniden deneme tutarlılığı gerekir. Mevcut kontrollerin tamamı bu turda denetlenmedi. |

**İncelenen:** araç/grup alanları, fiyat ve rezervasyon sözleşmeleri, ilgili servis yolları, müşteri kart/liste/detay ve rezervasyon durumu, kalıcı tarayıcı durumu, bazı veri tabanı kısıtları, bildirim ve ayar altyapısının varlığı.

**İncelenmeyen:** çalışan sistemde saldırı/erişim testleri, gerçek veri tabanı içeriği, şifreleme anahtarı yönetimi, sunucu/yedek şifreleme ayarları, tüm admin/guest yetkilendirme yolları, e-posta sağlayıcı hesabı, bağımlılık güvenlik taraması ve hukuki metinlerin uygunluğu.

**Varsayımlar:** gerçek müşteri kullanımı henüz başlamayacak; uygulama parça parça yerelde doğrulanacak; ücret ve işletme kuralları belirlenmeden kesin taahhüt yazılmayacak.

**Çalıştırılan araçlar:** salt okunur Git ref/diff/durum kontrolleri, takip edilen dosya envanteri, kaynak kod arama/okuma ve önceki kararların hatırlanması. Bu turda test veya aktif güvenlik taraması çalıştırılmadı.

Her uygulama paketinin sonunda değişen yollar için odaklı güvenlik incelemesi önerilir. Devir/yayın öncesi ASP.NET Core, PostgreSQL ve Next.js akışını kapsayan kontrol planı; açık DTO, yetkisiz rezervasyon işlemi, teklif değiştirme, çift rezervasyon, dosya yükleme ve e-posta hata senaryolarını içermelidir. Bu öneri, ayrı bir taramanın yapılmış olduğu anlamına gelmez.

## Kaynak sürümü ve yerel durum

- Uzak varsayılan dal `main`; `git ls-remote --symref origin HEAD` ile görülen uç: `9c777158e6188e99594f25b84ec49edf641547c3`.
- Yerel `main` ve mevcut `origin/main` referansları aynı committe. Bu incelemede `fetch` çalıştırıldığı iddia edilmiyor.
- Ana çalışma klasörünün aktif dalı `codex/docs-db-ops-phase0-doc-contract`, HEAD `e81eb08d07e33cfcf2979566604f82f72e56034f`; main ile aynı çalışma klasörü olduğu söylenemez. Takip edilmeyen `.worktrees/` korunmuştur.
- İnceleme temiz `C:/All_Project/Araç Kiralama/.worktrees/local-validation-20260923` klasöründe yapıldı. HEAD `073da36890d4576c0a31248c34581bf11520ba72`; incelenen `main` ile dosya farkı yoktur.
- Yeni uygulama başlamadan uzak varsayılan dal yeniden fetch edilmeli, yerel varsayılan dal güvenli biçimde kontrol edilmeli ve yeni dal güncel tabandan açılmalıdır. Bu turda hiçbir çalışma ağacı kaldırılmadı veya dal değiştirilmedi.

## Paket 1 — Gün hesabı ve arama varsayılanları (26 Eylül 2026)

- Rezervasyon/müsaitlik gün sayısı teklif veya kayıtlı fiyat günleriyle eşitlendi; bunlar yoksa ortak Türkiye takvimi kullanılır. Geçmiş fiyat kayıtları değiştirilmez.
- Ana sayfa araması Türkiye saatine göre gelecekteki ilk 10:00'u, dönüş için yedi gün sonrasını önerir. Header/Hero dosyaları ve SearchForm görünümü değişmedi; yalnızca varsayılan tarih mantığı düzeltildi.
- 827 backend, 54 API entegrasyon ve 308 frontend testi; üretim derlemesi/TypeScript ve lint geçti (önceden bulunan tek uyarı sürüyor). Gerçek yerel API ile varsayılan tarih akışı ve tarifeli Ekim tarihleri için fiyat/devam bağlantısı doğrulandı.
- Yeni commit için CI ve Codex sonucu ayrıca kontrol edilmelidir. Merge, dağıtım veya gerçek rezervasyon yapılmadı.

## Paket 1 — Üçüncü inceleme düzeltmeleri (25 Eylül 2026)

- Onay ve takip ekranları kayıtlı UTC zamanını Europe/Istanbul saat diliminde gösterir; gece teslimlerinde gün kayması ve ziyaretçinin saat dilimine bağımlılık giderildi.
- Araç detayına API kaynaklı minimum yaş ve ehliyet süresi eklendi; beş dil desteklenir.
- 304 frontend testi (America/Los_Angeles ortamı), üretim derlemesi/TypeScript ve lint geçti; lintte önceden var olan tek uyarı sürüyor. Yerel tarayıcıda sentetik API yanıtlarıyla 10 Haziran 2030 01:00 teslimi, 14 Haziran 10:00 dönüşü ve 25 yaş / 4 yıl koşulları doğrulandı.
- Header/hero değişmedi. Backend bu turda değiştirilmedi veya yeniden test edilmedi. CI ve Codex incelemesi yeni commit için ayrıca izlenmelidir; merge/dağıtım yapılmadı.
