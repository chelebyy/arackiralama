# Kod İncelemesi Sonrası Görev Önerileri

Aşağıdaki görevler, mevcut kod tabanı üzerinde hızlı bir tarama sonrası tespit edilen somut sorunlara göre hazırlanmıştır.

## 1) Yazım/Metin Hatası Düzeltme Görevi
**Başlık:** Admin giriş/çıkış başarı mesajlarındaki bozuk Türkçe karakterleri düzelt

- **Sorun:** `AdminAuthController` içinde başarı mesajları bozuk encoding ile görünüyor (`Giri� ba�ar�l�.`, `��k�� ba�ar�l�.`).
- **Etkisi:** Kullanıcıya dönen API mesajları okunabilir değil; panel UX kalitesi düşüyor.
- **Kapsam:** `backend/src/RentACar.API/Controllers/AdminAuthController.cs`
- **Kabul Kriterleri:**
  - Giriş mesajı `Giriş başarılı.`
  - Çıkış mesajı `Çıkış başarılı.`
  - UTF-8 encoding ile commit edilir.

## 2) Hata Düzeltme Görevi
**Başlık:** Admin login e-posta eşleşmesini büyük/küçük harf duyarsız hale getir

- **Sorun:** Login sorgusu e-postayı doğrudan `user.Email == email` ile karşılaştırıyor; `Admin@Test.com` ve `admin@test.com` farklı kabul ediliyor.
- **Etkisi:** Geçerli kullanıcılar yalnızca kayıtlı casing ile giriş yapabiliyor; gereksiz başarısız login üretiyor.
- **Kapsam:**
  - `backend/src/RentACar.API/Controllers/AdminAuthController.cs`
  - Gerekirse DB tarafında normalize edilmiş alan veya case-insensitive collation düzeni
- **Kabul Kriterleri:**
  - Aynı e-postanın farklı casing varyasyonları ile login başarılı.
  - Güvenlik davranışı (yanlış parola, pasif kullanıcı) korunur.

## 3) Kod Yorumu / Dokümantasyon Tutarsızlığı Düzeltme Görevi
**Başlık:** Backend README faz bilgisi ve güncel kapsamını hizala

- **Sorun:** README'de proje "Faz 1 foundation backend iskeleti" olarak geçiyor; ancak kodda `Phase12DatabaseSchema` gibi çok daha ileri faz migration'ları bulunuyor.
- **Etkisi:** Yeni geliştiriciler için proje olgunluğu yanlış anlaşılıyor, onboarding kalitesi düşüyor.
- **Kapsam:** `backend/README.md`
- **Kabul Kriterleri:**
  - Faz bilgisi güncel durumu yansıtacak şekilde revize edilir.
  - README'de güncel modüller ve geliştirme akışı kısa ve tutarlı şekilde anlatılır.

## 4) Test İyileştirme Görevi
**Başlık:** AdminAuthController testlerine başarı mesajı doğrulaması ekle

- **Sorun:** Başarılı login testi tokenı doğruluyor ama kullanıcıya dönen `Message` alanını doğrulamıyor.
- **Etkisi:** Bozuk/yanlış metinler (encoding dahil) testlerden kaçabiliyor.
- **Kapsam:** `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs`
- **Kabul Kriterleri:**
  - Başarılı login için `ApiResponse.Message` assert edilir.
  - Logout endpointi için de mesaj doğrulayan test eklenir.
  - Testler yerelde yeşil çalışır.
