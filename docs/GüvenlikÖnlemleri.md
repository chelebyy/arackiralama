# Rent-a-car (araç kiralama) sistemlerinde sık görülen güvenlik açıkları

Aşağıda, rent-a-car sistemlerinde sıkça karşılaşılan güvenlik açıklarını sade ve pratik bir şekilde bulabilirsin. Bu bilgiler gerçek bug bounty raporlarından ve pentestlerde sürekli çıkan sorunlardır. Not olarak saklayabilirsin. 🚗🔐

## 1️⃣ IDOR (En yaygın açık)
**Insecure Direct Object Reference**
Başkasının rezervasyonunu görebilmek.

### Örnek:
- `/api/reservations/123`
- `/api/reservations/124`

Eğer kullanıcı sadece ID değiştirerek başkasının rezervasyonunu görebiliyorsa bu açık var.

### Çözüm:
Backend mutlaka kullanıcı kontrolü yapmalı.

```python
if reservation.user_id != current_user.id:
    return 403
```

## 2️⃣ Admin panel erişimi
Bazı sistemlerde admin paneli sadece URL ile korunur.
- `/admin`
- `/admin-panel`
- `/admin/dashboard`

Eğer login kontrolü yoksa büyük bir açık olur.

### Çözüm:
Her admin endpointinde:
- **admin role kontrolü** yapılmalı.

## 3️⃣ Fiyat manipülasyonu
Kullanıcı frontend fiyatını değiştirebilir.

### Örnek:
`POST /api/reservation`
fiyat=1

### Çözüm:
fiyat frontendden değil backendden hesaplanmalı.
```python
daily_price = car.price_per_day
days = ... # hesaplama yapılmalı
total_price = daily_price * days
```

## 4️⃣ Ödeme bypass
Bazı sistemlerde ödeme yapılmadan rezervasyon oluşabiliyor.

### Örnek:
`POST /api/confirm-booking`

### Çözüm:
bbackend ödeme doğrulaması yapılmalı.
e.g., `payment_status == paid` kontrolü şarttır.

## 5️⃣ File upload açıkları
Ehliyet veya kimlik yükleme kısmında riskler bulunuyor.
kullanıcı `shell.php` gibi dosyalar yükleyebilir.

### Çözüm:
sadece belirli uzantılar ve mime type kontrolü yapılmalı,
yüklenen dosya rastgele isimlendirilmelidir,
yükleme klasörü çalıştırılmamalıdır.
```plaintext
directory: upload/
safe_extensions: ['jpg', 'png', 'pdf']
mime_type_check: true
generate_random_filename()
disable_execution_in_upload_folder()
def check_file_extension(file): ...
def generate_random_filename(): ...
def disable_execution_in_upload_folder(): ...
def check_mime_type(file): ...
'thumbnail'