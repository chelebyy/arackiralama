# Error Handling & Observability Specification

Date: 2026-02-25
Version: 2.0.0 - Enterprise
Status: Production Ready

---

## Table of Contents

1. [Error Response Format](#1-error-response-format)
2. [Error Code Categories](#2-error-code-categories)
3. [Circuit Breaker Configuration](#3-circuit-breaker-configuration)
4. [Retry Strategies](#4-retry-strategies)
5. [Alerting Rules](#5-alerting-rules)
6. [Client-Side Error Handling](#6-client-side-error-handling)
7. [Logging Standards](#7-logging-standards)
8. [Monitoring Requirements](#8-monitoring-requirements)

---

## 1. Error Response Format

### 1.1 Standard Error Response Schema

```json
{
  "success": false,
  "error": {
    "code": "MODULE_ERROR_DESCRIPTION",
    "message": "Human-readable error message (EN)",
    "message_tr": "Insan tarafindan okunabilir hata mesaji (TR)",
    "correlation_id": "550e8400-e29b-41d4-a716-446655440000",
    "timestamp": "2026-03-15T14:30:00.000Z",
    "details": {
      "field": "optional_field_name",
      "value": "optional_field_value",
      "suggestion": "How to fix this error"
    },
    "request_id": "req_abc123",
    "documentation_url": "https://docs.alanyarentacar.com/errors/MODULE_ERROR_DESCRIPTION"
  },
  "meta": {
    "api_version": "1.0.0",
    "environment": "production"
  }
}
```

### 1.2 Validation Error Response Schema

```json
{
  "success": false,
  "error": {
    "code": "VAL_VALIDATION_FAILED",
    "message": "Multiple validation errors occurred",
    "message_tr": "Bircok dogrulama hatasi olustu",
    "correlation_id": "550e8400-e29b-41d4-a716-446655440001",
    "timestamp": "2026-03-15T14:30:00.000Z",
    "validation_errors": [
      {
        "field": "pickup_datetime",
        "code": "VAL_PAST_DATE",
        "message": "Pickup date cannot be in the past",
        "message_tr": "Teslim alma tarihi gecmiste olamaz"
      },
      {
        "field": "email",
        "code": "VAL_INVALID_EMAIL",
        "message": "Invalid email format",
        "message_tr": "Gecersiz e-posta formati"
      }
    ]
  }
}
```

---

## 2. Error Code Categories

### 2.1 Authentication Errors (AUTH_xxx)

HTTP 401 Unauthorized / 403 Forbidden

| Error Code | HTTP | Retryable | Log Level | Description (EN) | Description (TR) |
|------------|------|-----------|-----------|------------------|------------------|
| AUTH_TOKEN_MISSING | 401 | No | WARN | JWT token not provided in Authorization header | Authorization headerinda JWT token bulunamadi |
| AUTH_TOKEN_EXPIRED | 401 | No | WARN | JWT token has expired. Please login again | JWT token suresi doldu. Lutfen tekrar giris yapin |
| AUTH_TOKEN_INVALID | 401 | No | WARN | JWT token format is invalid or malformed | JWT token formati gecersiz veya bozuk |
| AUTH_TOKEN_REVOKED | 401 | No | INFO | JWT token has been revoked by user or admin | JWT token kullanici veya admin tarafindan iptal edildi |
| AUTH_REFRESH_TOKEN_EXPIRED | 401 | No | WARN | Refresh token has expired. Full re-authentication required | Yenileme tokeni suresi doldu. Tam yeniden kimlik dogrulama gerekli |
| AUTH_REFRESH_TOKEN_INVALID | 401 | No | WARN | Refresh token is invalid or does not exist | Yenileme tokeni gecersiz veya mevcut degil |
| AUTH_INVALID_CREDENTIALS | 401 | No | WARN | Email or password is incorrect | E-posta veya sifre yanlis |
| AUTH_ACCOUNT_LOCKED | 403 | No | WARN | Account is temporarily locked due to multiple failed login attempts | Hesap, birden fazla basarisiz giris denemesi nedeniyle gecici olarak kilitlendi |
| AUTH_ACCOUNT_DISABLED | 403 | No | INFO | Account has been disabled. Contact support | Hesap devre disi birakildi. Destek ile iletisime gecin |
| AUTH_ACCOUNT_NOT_VERIFIED | 403 | No | INFO | Account email or phone not verified | Hesap e-postasi veya telefonu dogrulanmamis |
| AUTH_INSUFFICIENT_PERMISSIONS | 403 | No | WARN | User does not have required permissions for this action | Kullanicinin bu islem icin gerekli izinleri yok |
| AUTH_ROLE_REQUIRED | 403 | No | WARN | Required role not assigned to user | Gerekli rol kullaniciya atanmamis |
| AUTH_ADMIN_ONLY | 403 | No | WARN | This action requires admin privileges | Bu islem yonetici yetkileri gerektirir |
| AUTH_SUPERADMIN_ONLY | 403 | No | WARN | This action requires super admin privileges | Bu islem super yonetici yetkileri gerektirir |
| AUTH_SESSION_EXPIRED | 401 | No | INFO | User session has expired | Kullanici oturumu suresi doldu |
| AUTH_SESSION_INVALIDATED | 401 | No | INFO | Session was invalidated due to security concerns | Guvenlik endiseleri nedeniyle oturum gecersiz kilindi |
| AUTH_MFA_REQUIRED | 401 | No | INFO | Multi-factor authentication required | Cok faktorlu kimlik dogrulama gerekli |
| AUTH_MFA_INVALID_CODE | 401 | No | WARN | Invalid MFA code provided | Gecersiz MFA kodu saglandi |
| AUTH_MFA_SETUP_INCOMPLETE | 403 | No | INFO | MFA setup is incomplete. Please complete setup | MFA kurulumu tamamlanmamis. Lutfen kurulumu tamamlayin |
| AUTH_DEVICE_NOT_RECOGNIZED | 401 | No | INFO | Login from unrecognized device. Verification required | Taninmamis cihazdan giris. Dogrulama gerekli |
| AUTH_IP_BLOCKED | 403 | No | WARN | Access blocked from this IP address | Bu IP adresinden erisim engellendi |
| AUTH_RATE_LIMIT_LOGIN | 429 | Yes | WARN | Too many login attempts. Please try again later | Cok fazla giris denemesi. Lutfen daha sonra tekrar deneyin |
| AUTH_RATE_LIMIT_TOKEN_REFRESH | 429 | Yes | WARN | Too many token refresh attempts | Cok fazla token yenileme denemesi |
| AUTH_OAUTH_PROVIDER_ERROR | 500 | Yes | ERROR | OAuth provider returned an error | OAuth saglayicisi bir hata dondurdu |
| AUTH_OAUTH_STATE_MISMATCH | 400 | No | WARN | OAuth state parameter mismatch - possible CSRF attack | OAuth state parametresi uyusmazligi - olasi CSRF saldirisi |

### 2.2 Reservation Errors (RES_xxx)

HTTP 400 Bad Request / 409 Conflict / 410 Gone

| Error Code | HTTP | Retryable | Log Level | Description (EN) | Description (TR) |
|------------|------|-----------|-----------|------------------|------------------|
| RES_VEHICLE_UNAVAILABLE | 409 | No | INFO | Selected vehicle is no longer available for these dates | Secilen arac artik bu tarihler icin musait degil |
| RES_OVERLAPPING_RESERVATION | 409 | No | INFO | Vehicle already has an overlapping reservation | Aracin cakisan bir rezervasyonu var |
| RES_VEHICLE_IN_MAINTENANCE | 409 | No | INFO | Vehicle is under maintenance for selected dates | Arac secili tarihlerde bakimda |
| RES_VEHICLE_NOT_ACTIVE | 409 | No | INFO | Vehicle is not active and cannot be reserved | Arac aktif degil ve rezerve edilemez |
| RES_HOLD_EXPIRED | 410 | No | INFO | Reservation hold has expired. Please start over | Rezervasyon tutma suresi doldu. Lutfen bastan baslayin |
| RES_HOLD_CONFLICT | 409 | No | INFO | Another user is currently holding this vehicle | Baska bir kullanici su anda bu araci tutuyor |
| RES_INVALID_STATUS_TRANSITION | 400 | No | WARN | Invalid reservation status transition requested | Gecersiz rezervasyon durum gecisi istendi |
| RES_ALREADY_PAID | 409 | No | WARN | Reservation is already paid | Rezervasyon zaten odendi |
| RES_ALREADY_CANCELLED | 409 | No | WARN | Reservation has already been cancelled | Rezervasyon zaten iptal edildi |
| RES_ALREADY_COMPLETED | 409 | No | WARN | Reservation has already been completed | Rezervasyon zaten tamamlandi |
| RES_PAYMENT_PENDING | 409 | No | INFO | Payment is still pending for this reservation | Bu rezervasyon icin odeme hala bekleniyor |
| RES_MIN_RENTAL_DAYS_NOT_MET | 400 | No | INFO | Minimum rental days requirement not met | Minimum kiralama gunu sarti karsilanmadi |
| RES_MAX_RENTAL_DAYS_EXCEEDED | 400 | No | INFO | Maximum rental days limit exceeded | Maksimum kiralama gunu limiti asildi |
| RES_INVALID_DATE_RANGE | 400 | No | INFO | Return date must be after pickup date | Donus tarihi teslim alma tarihinden sonra olmali |
| RES_PICKUP_IN_PAST | 400 | No | INFO | Pickup date cannot be in the past | Teslim alma tarihi gecmiste olamaz |
| RES_PICKUP_TOO_FAR_FUTURE | 400 | No | INFO | Pickup date is too far in the future | Teslim alma tarihi cok ileride |
| RES_OFFICE_CLOSED | 400 | No | INFO | Selected office is closed during pickup/return time | Secilen ofis teslim alma/donus saatlerinde kapali |
| RES_OFFICE_NOT_FOUND | 404 | No | INFO | Pickup or return office not found | Teslim alma veya donus ofisi bulunamadi |
| RES_VEHICLE_GROUP_NOT_FOUND | 404 | No | INFO | Vehicle group not found | Arac grubu bulunamadi |
| RES_CAMPAIGN_INVALID | 400 | No | INFO | Campaign code is invalid or expired | Kampanya kodu gecersiz veya suresi dolmus |
| RES_CAMPAON_MINIMUM_DAYS_NOT_MET | 400 | No | INFO | Campaign requires minimum rental days not met | Kampanya karsilanmamis minimum kiralama gunu gerektiriyor |
| RES_CAMPAIGN_VEHICLE_GROUP_EXCLUDED | 400 | No | INFO | Campaign does not apply to this vehicle group | Kampanya bu arac grubu icin gecerli degil |
| RES_CUSTOMER_MIN_AGE_NOT_MET | 400 | No | INFO | Customer does not meet minimum age requirement | Musteri minimum yas sartini karsilamiyor |
| RES_CUSTOMER_LICENSE_YEARS_NOT_MET | 400 | No | INFO | Customer does not meet minimum license years requirement | Musteri minimum ehliyet yili sartini karsilamiyor |
| RES_CUSTOMER_BLACKLISTED | 403 | No | WARN | Customer is blacklisted from making reservations | Musteri rezervasyon yapma kara listesinde |
| RES_DUPLICATE_RESERVATION | 409 | No | WARN | Similar reservation already exists for this customer | Bu musteri icin benzer bir rezervasyon zaten var |
| RES_MAX_ACTIVE_RESERVATIONS_REACHED | 400 | No | INFO | Customer has reached maximum active reservations limit | Musteri maksimum aktif rezervasyon limitine ulasti |
| RES_PRICE_CALCULATION_FAILED | 500 | Yes | ERROR | Failed to calculate reservation price | Rezervasyon fiyati hesaplanamadi |
| RES_DEPOSIT_CALCULATION_FAILED | 500 | Yes | ERROR | Failed to calculate deposit amount | Depozito tutari hesaplanamadi |
| RES_SEASONAL_PRICING_NOT_FOUND | 500 | Yes | ERROR | Seasonal pricing configuration not found | Mevsimsel fiyatlandirma yapilandirmasi bulunamadi |
| RES_PRICE_CHANGED_DURING_HOLD | 409 | No | INFO | Price changed during hold period. Please review | Tutma suresi icinde fiyat degisti. Lutfen gozden gecirin |
| RES_CANCEL_TOO_LATE | 400 | No | INFO | Cancellation window has passed | Iptal penceresi kapanmistir |
| RES_CANCEL_FEE_CALCULATION_FAILED | 500 | Yes | ERROR | Failed to calculate cancellation fee | Iptal ucreti hesaplanamadi |
| RES_REFUND_NOT_ALLOWED | 400 | No | WARN | Refund not allowed for this reservation status | Bu rezervasyon durumu icin para iadesine izin verilmez |
| RES_ALREADY_REFUNDED | 409 | No | WARN | Reservation has already been fully refunded | Rezervasyon zaten tamamen iade edildi |
| RES_PARTIAL_REFUND_NOT_SUPPORTED | 400 | No | INFO | Partial refunds are not supported for this payment | Bu odeme icin kismi iadeler desteklenmiyor |
| RES_DEPOSIT_ALREADY_RELEASED | 409 | No | WARN | Deposit has already been released | Depozito zaten serbest birakildi |
| RES_DEPOSIT_RELEASE_FAILED | 500 | Yes | ERROR | Failed to release deposit | Depozito serbest birakilamadi |
| RES_DEPOSIT_NOT_PREAUTHORIZED | 400 | No | WARN | No deposit pre-authorization exists for this reservation | Bu rezervasyon icin depozito on yetkilendirmesi yok |
| RES_VEHICLE_ASSIGNMENT_FAILED | 500 | Yes | ERROR | Failed to assign vehicle to reservation | Rezervasyona arac atanamadi |
| RES_VEHICLE_ALREADY_ASSIGNED | 409 | No | WARN | Another vehicle is already assigned to this reservation | Bu rezervasyona baska bir arac zaten atanmis |
| RES_CHECK_IN_FAILED | 500 | Yes | ERROR | Vehicle check-in process failed | Arac teslim alma islemi basarisiz oldu |
| RES_CHECK_OUT_FAILED | 500 | Yes | ERROR | Vehicle check-out process failed | Arac teslim etme islemi basarisiz oldu |
| RES_LATE_RETURN_DETECTED | 400 | No | INFO | Late return detected. Additional charges may apply | Gec donus tespit edildi. Ek ucretler uygulanabilir |
| RES_DAMAGE_REPORT_FAILED | 500 | Yes | ERROR | Failed to submit damage report | Hasar raporu gonderilemedi |
| RES_EXTRA_CHARGE_CALCULATION_FAILED | 500 | Yes | ERROR | Failed to calculate extra charges | Ek ucretler hesaplanamadi |
| RES_FLIGHT_NUMBER_INVALID | 400 | No | INFO | Flight number format is invalid | Ucus numarasi formati gecersiz |
| RES_FLIGHT_TRACKING_UNAVAILABLE | 503 | Yes | WARN | Flight tracking service temporarily unavailable | Ucus takip servisi gecici olarak kullanilamiyor |
| RES_EXTRA_SERVICE_UNAVAILABLE | 409 | No | INFO | Requested extra service is not available | Istek ekstra hizmet musait degil |
| RES_ONE_WAY_RENTAL_NOT_ALLOWED | 400 | No | INFO | One-way rental not allowed between these offices | Bu ofisler arasinda tek yon kiralama izin verilmez |
| RES_ONE_WAY_FEE_CALCULATION_FAILED | 500 | Yes | ERROR | Failed to calculate one-way rental fee | Tek yon kiralama ucreti hesaplanamadi |

### 2.3 Payment Errors (PAY_xxx)

HTTP 400 Bad Request / 402 Payment Required / 422 Unprocessable Entity

| Error Code | HTTP | Retryable | Log Level | Description (EN) | Description (TR) |
|------------|------|-----------|-----------|------------------|------------------|
| PAYMENT_PROVIDER_UNAVAILABLE | 503 | Yes | ERROR | Payment provider service is temporarily unavailable | Odeme saglayici servisi gecici olarak kullanilamiyor |
| PAYMENT_PROVIDER_TIMEOUT | 504 | Yes | ERROR | Payment provider request timed out | Odeme saglayici istegi zaman asimina ugradi |
| PAYMENT_PROVIDER_ERROR | 502 | Yes | ERROR | Payment provider returned an error | Odeme saglayicisi bir hata dondurdu |
| PAYMENT_CARD_DECLINED | 402 | No | INFO | Card was declined by the bank | Kart banka tarafindan reddedildi |
| PAYMENT_INSUFFICIENT_FUNDS | 402 | No | INFO | Card has insufficient funds | Kartta yetersiz bakiye var |
| PAYMENT_CARD_EXPIRED | 400 | No | INFO | Card has expired | Kart suresi dolmus |
| PAYMENT_INVALID_CARD_NUMBER | 400 | No | INFO | Card number is invalid | Kart numarasi gecersiz |
| PAYMENT_INVALID_CVV | 400 | No | INFO | CVV code is invalid | CVV kodu gecersiz |
| PAYMENT_INVALID_EXPIRY_DATE | 400 | No | INFO | Card expiry date is invalid | Kart son kullanma tarihi gecersiz |
| PAYMENT_CARD_NOT_SUPPORTED | 400 | No | INFO | Card type is not supported | Kart tipi desteklenmiyor |
| PAYMENT_3DS_FAILED | 402 | No | INFO | 3D Secure authentication failed | 3D Secure kimlik dogrulamasi basarisiz oldu |
| PAYMENT_3DS_TIMEOUT | 402 | No | INFO | 3D Secure authentication timed out | 3D Secure kimlik dogrulama zaman asimina ugradi |
| PAYMENT_3DS_CANCELLED | 402 | No | INFO | 3D Secure authentication was cancelled by user | 3D Secure kimlik dogrulamasi kullanici tarafindan iptal edildi |
| PAYMENT_3DS_UNSUPPORTED | 400 | No | INFO | Card does not support 3D Secure | Kart 3D Secure desteklemiyor |
| PAYMENT_3DS_ENROLLMENT_FAILED | 400 | No | INFO | Failed to verify 3D Secure enrollment | 3D Secure kaydi dogrulanamadi |
| PAYMENT_AMOUNT_INVALID | 400 | No | WARN | Payment amount is invalid or zero | Odeme tutari gecersiz veya sifir |
| PAYMENT_AMOUNT_TOO_SMALL | 400 | No | INFO | Payment amount is below minimum allowed | Odeme tutari izin verilen minimumun altinda |
| PAYMENT_AMOUNT_TOO_LARGE | 400 | No | INFO | Payment amount exceeds maximum allowed | Odeme tutari izin verilen maksimumu asiyor |
| PAYMENT_CURRENCY_NOT_SUPPORTED | 400 | No | INFO | Currency is not supported by the payment provider | Para birimi odeme saglayicisi tarafindan desteklenmiyor |
| PAYMENT_CURRENCY_MISMATCH | 400 | No | WARN | Payment currency does not match reservation currency | Odeme para birimi rezervasyon para birimiyle eslesmiyor |
| PAYMENT_IDEMPOTENCY_KEY_MISSING | 400 | No | WARN | Idempotency key is required for this operation | Bu islem icin idempotency anahtari gerekli |
| PAYMENT_IDEMPOTENCY_KEY_REUSED | 409 | No | WARN | Idempotency key has already been used with different parameters | Idempotency anahtari zaten farkli parametrelerle kullanildi |
| PAYMENT_INTENT_NOT_FOUND | 404 | No | WARN | Payment intent not found | Odeme niyeti bulunamadi |
| PAYMENT_INTENT_EXPIRED | 410 | No | INFO | Payment intent has expired | Odeme niyeti suresi dolmus |
| PAYMENT_INTENT_ALREADY_USED | 409 | No | WARN | Payment intent has already been used | Odeme niyeti zaten kullanildi |
| PAYMENT_INTENT_STATUS_INVALID | 400 | No | WARN | Payment intent is in invalid status for this operation | Odeme niyeti bu islem icin gecersiz durumda |
| PAYMENT_REFUND_NOT_ALLOWED | 400 | No | WARN | Refund not allowed for this payment | Bu odeme icin iadeye izin verilmez |
| PAYMENT_REFUND_AMOUNT_INVALID | 400 | No | WARN | Refund amount is invalid | Iade tutari gecersiz |
| PAYMENT_REFUND_EXCEEDS_PAYMENT | 400 | No | WARN | Refund amount exceeds original payment | Iade tutari orijinal odemeyi asiyor |
| PAYMENT_REFUND_FAILED | 502 | Yes | ERROR | Refund processing failed | Iade islemi basarisiz oldu |
| PAYMENT_PARTIAL_REFUND_NOT_SUPPORTED | 400 | No | INFO | Partial refunds not supported by provider | Saglayici tarafindan kismi iadeler desteklenmiyor |
| PAYMENT_DEPOSIT_PREAUTH_FAILED | 402 | No | INFO | Deposit pre-authorization failed | Depozito on yetkilendirmesi basarisiz oldu |
| PAYMENT_DEPOSIT_PREAUTH_AMOUNT_INVALID | 400 | No | INFO | Deposit pre-authorization amount is invalid | Depozito on yetkilendirme tutari gecersiz |
| PAYMENT_DEPOSIT_CAPTURE_FAILED | 502 | Yes | ERROR | Deposit capture failed | Depozito tahsilati basarisiz oldu |
| PAYMENT_DEPOSIT_RELEASE_FAILED | 502 | Yes | ERROR | Deposit release failed | Depozito serbest birakma basarisiz oldu |
| PAYMENT_DEPOSIT_ALREADY_CAPTURED | 409 | No | WARN | Deposit has already been captured | Depozito zaten tahsil edildi |
| PAYMENT_DEPOSIT_ALREADY_RELEASED | 409 | No | WARN | Deposit has already been released | Depozito zaten serbest birakildi |
| PAYMENT_INSTALLMENT_COUNT_INVALID | 400 | No | INFO | Invalid installment count | Gecersiz taksit sayisi |
| PAYMENT_INSTALLMENTS_NOT_SUPPORTED | 400 | No | INFO | Installments not supported for this card | Bu kart icin taksit desteklenmiyor |
| PAYMENT_INSTALLMENT_MIN_AMOUNT_NOT_MET | 400 | No | INFO | Purchase amount does not meet minimum for installments | Satis tutari taksitler icin minimumu karsilamiyor |
| PAYMENT_BIN_CHECK_FAILED | 500 | Yes | ERROR | Card BIN check failed | Kart BIN kontrolu basarisiz oldu |
| PAYMENT_FRAUD_SUSPECTED | 402 | No | WARN | Transaction flagged as potential fraud | Islem potansiyel dolandiricilik olarak isaretlendi |
| PAYMENT_VELOCITY_CHECK_FAILED | 429 | No | WARN | Payment velocity limit exceeded | Odeme hiz limiti asildi |
| PAYMENT_WEBHOOK_SIGNATURE_MISSING | 400 | No | WARN | Webhook signature header is missing | Webhook imza basligi eksik |
| PAYMENT_WEBHOOK_SIGNATURE_INVALID | 400 | No | WARN | Webhook signature verification failed | Webhook imza dogrulamasi basarisiz oldu |
| PAYMENT_WEBHOOK_EVENT_INVALID | 400 | No | WARN | Invalid webhook event type | Gecersiz webhook olay turu |
| PAYMENT_WEBHOOK_PAYLOAD_INVALID | 400 | No | WARN | Invalid webhook payload format | Gecersiz webhook payload formati |
| PAYMENT_WEBHOOK_DUPLICATE_EVENT | 200 | No | INFO | Duplicate webhook event received. Ignored | Yinelenen webhook olayi alindi. Yok sayildi |
| PAYMENT_WEBHOOK_PROCESSING_FAILED | 500 | Yes | ERROR | Failed to process webhook event | Webhook olayi islenemedi |
| PAYMENT_TRANSACTION_NOT_FOUND | 404 | No | WARN | Transaction not found | Islem bulunamadi |
| PAYMENT_TRANSACTION_ALREADY_PROCESSED | 409 | No | INFO | Transaction has already been processed | Islem zaten islenmis |
| PAYMENT_SETTLEMENT_FAILED | 502 | Yes | ERROR | Payment settlement failed | Odeme uzlastirmasi basarisiz oldu |
| PAYMENT_BANK_ACCOUNT_INVALID | 400 | No | INFO | Bank account information is invalid | Banka hesap bilgileri gecersiz |
| PAYMENT_BANK_TRANSFER_FAILED | 502 | Yes | ERROR | Bank transfer failed | Banka transferi basarisiz oldu |
| PAYMENT_CASH_PAYMENT_NOT_ALLOWED | 400 | No | INFO | Cash payment not allowed for online reservations | Online rezervasyonlar icin nakit odemeye izin verilmez |
| PAYMENT_RETRY_LIMIT_EXCEEDED | 429 | No | INFO | Maximum payment retry attempts exceeded | Maksimum odeme yeniden deneme limiti asildi |
| PAYMENT_SESSION_EXPIRED | 410 | No | INFO | Payment session has expired | Odeme oturumu suresi doldu |
| PAYMENT_CONFIGURATION_ERROR | 500 | No | ERROR | Payment provider configuration error | Odeme saglayici yapilandirma hatasi |
| PAYMENT_METHOD_NOT_ALLOWED | 400 | No | INFO | This payment method is not allowed for this reservation | Bu odeme yontemi bu rezervasyon icin izin verilmez |

### 2.4 Vehicle & Fleet Errors (VEH_xxx)

HTTP 400 Bad Request / 404 Not Found / 409 Conflict

| Error Code | HTTP | Retryable | Log Level | Description (EN) | Description (TR) |
|------------|------|-----------|-----------|------------------|------------------|
| VEH_NOT_FOUND | 404 | No | INFO | Vehicle not found | Arac bulunamadi |
| VEH_PLATE_ALREADY_EXISTS | 409 | No | WARN | Vehicle with this plate number already exists | Bu plaka numarali arac zaten mevcut |
| VEH_PLATE_INVALID_FORMAT | 400 | No | INFO | Vehicle plate number format is invalid | Arac plaka numarasi formati gecersiz |
| VEH_GROUP_NOT_FOUND | 404 | No | INFO | Vehicle group not found | Arac grubu bulunamadi |
| VEH_GROUP_NAME_ALREADY_EXISTS | 409 | No | WARN | Vehicle group with this name already exists | Bu isimde arac grubu zaten mevcut |
| VEH_GROUP_HAS_VEHICLES | 400 | No | WARN | Cannot delete vehicle group with assigned vehicles | Atanmis araclari olan arac grubu silinemez |
| VEH_STATUS_INVALID | 400 | No | INFO | Invalid vehicle status | Gecersiz arac durumu |
| VEH_STATUS_TRANSITION_INVALID | 400 | No | WARN | Invalid vehicle status transition | Gecersiz arac durum gecisi |
| VEH_NOT_AVAILABLE_FOR_DATES | 409 | No | INFO | Vehicle is not available for selected dates | Arac secili tarihler icin musait degil |
| VEH_MAINTENANCE_SCHEDULE_CONFLICT | 409 | No | INFO | Maintenance schedule conflicts with reservation | Bakim programi rezervasyonla cakisiyor |
| VEH_MAINTENANCE_RECORD_NOT_FOUND | 404 | No | INFO | Maintenance record not found | Bakim kaydi bulunamadi |
| VEH_MAINTENANCE_ALREADY_COMPLETED | 409 | No | WARN | Maintenance record is already marked as completed | Bakim kaydi zaten tamamlandi olarak isaretlendi |
| VEH_MAINTENANCE_CANNOT_CANCEL | 400 | No | WARN | Cannot cancel maintenance that has already started | Baslamis bakim iptal edilemez |
| VEH_OFFICE_NOT_FOUND | 404 | No | INFO | Office not found | Ofis bulunamadi |
| VEH_OFFICE_NAME_ALREADY_EXISTS | 409 | No | WARN | Office with this name already exists | Bu isimde ofis zaten mevcut |
| VEH_OFFICE_HAS_ACTIVE_RESERVATIONS | 400 | No | WARN | Cannot delete office with active reservations | Aktif rezervasyonlari olan ofis silinemez |
| VEH_TRANSFER_SAME_OFFICE | 400 | No | INFO | Source and destination offices are the same | Kaynak ve hedef ofisler ayni |
| VEH_TRANSFER_VEHICLE_NOT_AVAILABLE | 409 | No | INFO | Vehicle is not available for transfer | Arac transfer icin musait degil |
| VEH_TRANSFER_INVALID_DATE | 400 | No | INFO | Transfer date must be in the future | Transfer tarihi gelecekte olmali |
| VEH_TRANSFER_ALREADY_SCHEDULED | 409 | No | INFO | Transfer already scheduled for this vehicle | Bu arac icin transfer zaten planlandi |
| VEH_TRANSFER_CANNOT_CANCEL | 400 | No | WARN | Cannot cancel transfer that is in progress | Devam eden transfer iptal edilemez |
| VEH_TRANSFER_FAILED | 500 | Yes | ERROR | Vehicle transfer operation failed | Arac transfer islemi basarisiz oldu |
| VEH_MILEAGE_RECORD_FAILED | 500 | Yes | ERROR | Failed to record vehicle mileage | Arac kilometre kaydi basarisiz oldu |
| VEH_FUEL_LEVEL_RECORD_FAILED | 500 | Yes | ERROR | Failed to record fuel level | Yakit seviyesi kaydi basarisiz oldu |
| VEH_DAMAGE_RECORD_FAILED | 500 | Yes | ERROR | Failed to record vehicle damage | Arac hasar kaydi basarisiz oldu |
| VEH_IMAGE_UPLOAD_FAILED | 500 | Yes | ERROR | Failed to upload vehicle image | Arac gorseli yuklenemedi |
| VEH_IMAGE_INVALID_FORMAT | 400 | No | INFO | Invalid image format. Allowed: JPG, PNG, WEBP | Gecersiz gorsel formati. Izin verilen: JPG, PNG, WEBP |
| VEH_IMAGE_TOO_LARGE | 400 | No | INFO | Image file size exceeds maximum allowed (5MB) | Gorsel dosya boyutu izin verilen maksimumu asiyor (5MB) |
| VEH_DOCUMENT_UPLOAD_FAILED | 500 | Yes | ERROR | Failed to upload vehicle document | Arac belgesi yuklenemedi |
| VEH_DOCUMENT_NOT_FOUND | 404 | No | INFO | Vehicle document not found | Arac belgesi bulunamadi |
| VEH_INSURANCE_EXPIRED | 400 | No | INFO | Vehicle insurance has expired | Arac sigortasi suresi dolmus |
| VEH_INSPECTION_EXPIRED | 400 | No | INFO | Vehicle inspection has expired | Arac muayenesi suresi dolmus |
| VEH_REGISTRATION_EXPIRED | 400 | No | INFO | Vehicle registration has expired | Arac ruhsati suresi dolmus |
| VEH_BRAND_NOT_FOUND | 404 | No | INFO | Vehicle brand not found | Arac markasi bulunamadi |
| VEH_MODEL_NOT_FOUND | 404 | No | INFO | Vehicle model not found | Arac modeli bulunamadi |
| VEH_COLOR_NOT_SUPPORTED | 400 | No | INFO | Vehicle color is not in supported list | Arac rengi desteklenen listede degil |
| VEH_YEAR_INVALID | 400 | No | INFO | Vehicle year is invalid (must be 1990-current year + 1) | Arac yili gecersiz (1990-guncel yil + 1 arasinda olmali) |
| VEH_CAPACITY_INVALID | 400 | No | INFO | Vehicle passenger capacity is invalid | Arac yolcu kapasitesi gecersiz |
| VEH_TRUNK_CAPACITY_INVALID | 400 | No | INFO | Vehicle trunk capacity is invalid | Arac bagaj kapasitesi gecersiz |
| VEH_FEATURES_INVALID | 400 | No | INFO | One or more vehicle features are invalid | Bir veya daha fazla arac ozelligi gecersiz |
| VEH_DAILY_PRICE_INVALID | 400 | No | INFO | Daily rental price is invalid | Gunluk kiralama fiyati gecersiz |
| VEH_DEPOSIT_AMOUNT_INVALID | 400 | No | INFO | Deposit amount is invalid | Depozito tutari gecersiz |
| VEH_MIN_AGE_REQUIREMENT_INVALID | 400 | No | INFO | Minimum driver age requirement is invalid | Minimum sofor yasi sarti gecersiz |
| VEH_LICENSE_YEARS_REQUIREMENT_INVALID | 400 | No | INFO | Minimum license years requirement is invalid | Minimum ehliyet yili sarti gecersiz |
| VEH_SEARCH_DATE_RANGE_TOO_LARGE | 400 | No | INFO | Search date range exceeds maximum (90 days) | Arama tarih araligi maksimumu asiyor (90 gun) |
| VEH_INVENTORY_SYNC_FAILED | 500 | Yes | ERROR | Vehicle inventory sync with external system failed | Harici sistemle arac envanteri senkronizasyonu basarisiz oldu |
| VEH_BATCH_UPDATE_FAILED | 500 | Yes | ERROR | Batch vehicle update failed | Toplu arac guncelleme basarisiz oldu |

### 2.5 Validation Errors (VAL_xxx)

HTTP 400 Bad Request / 422 Unprocessable Entity

| Error Code | HTTP | Retryable | Log Level | Description (EN) | Description (TR) |
|------------|------|-----------|-----------|------------------|------------------|
| VAL_VALIDATION_FAILED | 422 | No | INFO | Request validation failed | Istek dogrulamasi basarisiz oldu |
| VAL_REQUIRED_FIELD_MISSING | 422 | No | INFO | Required field is missing | Gerekli alan eksik |
| VAL_FIELD_TOO_SHORT | 422 | No | INFO | Field value is too short | Alan degeri cok kisa |
| VAL_FIELD_TOO_LONG | 422 | No | INFO | Field value exceeds maximum length | Alan degeri maksimum uzunlugu asiyor |
| VAL_INVALID_EMAIL_FORMAT | 422 | No | INFO | Email address format is invalid | E-posta adresi formati gecersiz |
| VAL_INVALID_PHONE_FORMAT | 422 | No | INFO | Phone number format is invalid | Telefon numarasi formati gecersiz |
| VAL_INVALID_DATE_FORMAT | 422 | No | INFO | Date format is invalid. Expected: ISO8601 | Tarih formati gecersiz. Beklenen: ISO8601 |
| VAL_INVALID_DATETIME_RANGE | 422 | No | INFO | Date/time range is invalid | Tarih/saat araligi gecersiz |
| VAL_FUTURE_DATE_REQUIRED | 422 | No | INFO | Date must be in the future | Tarih gelecekte olmali |
| VAL_PAST_DATE | 422 | No | INFO | Date cannot be in the past | Tarih gecmiste olamaz |
| VAL_INVALID_UUID_FORMAT | 422 | No | INFO | UUID format is invalid | UUID formati gecersiz |
| VAL_INVALID_ENUM_VALUE | 422 | No | INFO | Value is not in allowed enum list | Deger izin verilen enum listesinde degil |
| VAL_INVALID_BOOLEAN_VALUE | 422 | No | INFO | Boolean value is invalid | Boolean degeri gecersiz |
| VAL_INVALID_INTEGER_VALUE | 422 | No | INFO | Integer value is invalid or out of range | Tamsayi degeri gecersiz veya aralik disi |
| VAL_INVALID_DECIMAL_VALUE | 422 | No | INFO | Decimal value is invalid | Ondalik deger gecersiz |
| VAL_INVALID_CURRENCY_FORMAT | 422 | No | INFO | Currency format is invalid | Para birimi formati gecersiz |
| VAL_NEGATIVE_VALUE_NOT_ALLOWED | 422 | No | INFO | Negative values are not allowed | Negatif degerlere izin verilmez |
| VAL_ZERO_VALUE_NOT_ALLOWED | 422 | No | INFO | Zero value is not allowed for this field | Bu alan icin sifir degerine izin verilmez |
| VAL_VALUE_OUT_OF_RANGE | 422 | No | INFO | Value is outside allowed range | Deger izin verilen araligin disinda |
| VAL_VALUE_NOT_IN_LIST | 422 | No | INFO | Value is not in the allowed list | Deger izin verilen listede degil |
| VAL_PATTERN_MATCH_FAILED | 422 | No | INFO | Value does not match required pattern | Deger gerekli paternle eslesmiyor |
| VAL_INVALID_TURKISH_ID_NUMBER | 422 | No | INFO | Turkish ID number (TC Kimlik) validation failed | Turk ID numarasi (TC Kimlik) dogrulamasi basarisiz oldu |
| VAL_INVALID_VAT_NUMBER | 422 | No | INFO | VAT/Tax number format is invalid | KDV/Vergi numarasi formati gecersiz |
| VAL_INVALID_URL_FORMAT | 422 | No | INFO | URL format is invalid | URL formati gecersiz |
| VAL_INVALID_IP_ADDRESS | 422 | No | INFO | IP address format is invalid | IP adresi formati gecersiz |
| VAL_INVALID_JSON_FORMAT | 400 | No | INFO | Request body is not valid JSON | Istek govdesi gecerli JSON degil |
| VAL_JSON_SCHEMA_VIOLATION | 422 | No | INFO | Request body violates JSON schema | Istek govdesi JSON semasini ihlal ediyor |
| VAL_ARRAY_TOO_SHORT | 422 | No | INFO | Array has fewer items than minimum required | Dizinin ogeleri minimum gerekenden az |
| VAL_ARRAY_TOO_LONG | 422 | No | INFO | Array has more items than maximum allowed | Dizi izin verilen maksimumdan fazla oge iceriyor |
| VAL_DUPLICATE_VALUES_IN_ARRAY | 422 | No | INFO | Array contains duplicate values | Dizi yinelenen degerler iceriyor |
| VAL_INVALID_BASE64_DATA | 422 | No | INFO | Base64 encoded data is invalid | Base64 kodlu veri gecersiz |
| VAL_INVALID_FILE_TYPE | 422 | No | INFO | File type is not allowed | Dosya turune izin verilmez |
| VAL_FILE_TOO_LARGE | 422 | No | INFO | File size exceeds maximum allowed | Dosya boyutu izin verilen maksimumu asiyor |
| VAL_INVALID_TIMEZONE | 422 | No | INFO | Timezone identifier is invalid | Zaman dilimi tanimlayici gecersiz |
| VAL_INVALID_LANGUAGE_CODE | 422 | No | INFO | Language code is not supported | Dil kodu desteklenmiyor |
| VAL_INVALID_COUNTRY_CODE | 422 | No | INFO | Country code is invalid | Ulke kodu gecersiz |
| VAL_INVALID_POSTAL_CODE | 422 | No | INFO | Postal code format is invalid for the country | Ulke icin posta kodu formati gecersiz |
| VAL_INVALID_COORDINATES | 422 | No | INFO | Geographic coordinates are invalid | Cografi koordinatlar gecersiz |
| VAL_INVALID_COLOR_CODE | 422 | No | INFO | Color code format is invalid (expected HEX or RGB) | Renk kodu formati gecersiz (HEX veya RGB bekleniyor) |
| VAL_RESERVED_KEYWORD_USED | 422 | No | WARN | Value uses a reserved keyword | Deger ayrilmis bir anahtar kelime kullaniyor |
| VAL_SENSITIVE_DATA_IN_REQUEST | 400 | No | WARN | Sensitive data detected in request body | Istek govdesinde hassas veri tespit edildi |
| VAL_REQUEST_SIZE_TOO_LARGE | 413 | No | INFO | Request body size exceeds maximum allowed (10MB) | Istek govdesi boyutu izin verilen maksimumu asiyor (10MB) |
| VAL_QUERY_PARAM_INVALID | 400 | No | INFO | Query parameter value is invalid | Sorgu parametresi degeri gecersiz |
| VAL_QUERY_PARAM_TOO_LONG | 400 | No | INFO | Query parameter value is too long | Sorgu parametresi degeri cok uzun |
| VAL_HEADER_MISSING | 400 | No | INFO | Required header is missing | Gerekli baslik eksik |
| VAL_HEADER_INVALID | 400 | No | INFO | Header value is invalid | Baslik degeri gecersiz |
| VAL_CONTENT_TYPE_UNSUPPORTED | 415 | No | INFO | Content-Type is not supported | Content-Type desteklenmiyor |
| VAL_CHARSET_UNSUPPORTED | 415 | No | INFO | Character encoding is not supported | Karakter kodlamasi desteklenmiyor |

### 2.6 System Errors (SYS_xxx)

HTTP 500 Internal Server Error / 503 Service Unavailable / 504 Gateway Timeout

| Error Code | HTTP | Retryable | Log Level | Description (EN) | Description (TR) |
|------------|------|-----------|-----------|------------------|------------------|
| SYS_INTERNAL_ERROR | 500 | Yes | ERROR | An unexpected internal error occurred | Beklenmeyen bir dahili hata olustu |
| SYS_DATABASE_CONNECTION_LOST | 500 | Yes | CRITICAL | Database connection was lost | Veritabani baglantisi kaybedildi |
| SYS_DATABASE_QUERY_FAILED | 500 | Yes | ERROR | Database query execution failed | Veritabani sorgu calistirma basarisiz oldu |
| SYS_DATABASE_DEADLOCK | 500 | Yes | WARN | Database deadlock detected. Retrying... | Veritabani kilitlenmesi tespit edildi. Yeniden deneniyor... |
| SYS_DATABASE_TIMEOUT | 504 | Yes | ERROR | Database query timed out | Veritabani sorgusu zaman asimina ugradi |
| SYS_DATABASE_CONSTRAINT_VIOLATION | 500 | No | ERROR | Database constraint violation | Veritabani kisit ihlali |
| SYS_DATABASE_MIGRATION_FAILED | 500 | No | CRITICAL | Database migration failed | Veritabani gocu basarisiz oldu |
| SYS_REDIS_CONNECTION_LOST | 500 | Yes | CRITICAL | Redis connection was lost | Redis baglantisi kaybedildi |
| SYS_REDIS_TIMEOUT | 504 | Yes | ERROR | Redis operation timed out | Redis islemi zaman asimina ugradi |
| SYS_REDIS_COMMAND_FAILED | 500 | Yes | ERROR | Redis command execution failed | Redis komut calistirma basarisiz oldu |
| SYS_CACHE_UNAVAILABLE | 503 | Yes | WARN | Cache service is temporarily unavailable | Önbellek servisi gecici olarak kullanilamiyor |
| SYS_CACHE_WRITE_FAILED | 500 | Yes | WARN | Failed to write to cache | Önbellege yazma basarisiz oldu |
| SYS_CACHE_READ_FAILED | 500 | Yes | WARN | Failed to read from cache | Önbellekten okuma basarisiz oldu |
| SYS_EXTERNAL_API_TIMEOUT | 504 | Yes | ERROR | External API request timed out | Harici API istegi zaman asimina ugradi |
| SYS_EXTERNAL_API_ERROR | 502 | Yes | ERROR | External API returned an error | Harici API bir hata dondurdu |
| SYS_EXTERNAL_API_UNAVAILABLE | 503 | Yes | ERROR | External API is unavailable | Harici API kullanilamiyor |
| SYS_SMS_SERVICE_TIMEOUT | 504 | Yes | ERROR | SMS service request timed out | SMS servisi istegi zaman asimina ugradi |
| SYS_SMS_SERVICE_ERROR | 502 | Yes | ERROR | SMS service returned an error | SMS servisi bir hata dondurdu |
| SYS_EMAIL_SERVICE_TIMEOUT | 504 | Yes | ERROR | Email service request timed out | E-posta servisi istegi zaman asimina ugradi |
| SYS_EMAIL_SERVICE_ERROR | 502 | Yes | ERROR | Email service returned an error | E-posta servisi bir hata dondurdu |
| SYS_PUSH_NOTIFICATION_FAILED | 500 | Yes | WARN | Push notification delivery failed | Anlik bildirim iletimi basarisiz oldu |
| SYS_FILE_STORAGE_UNAVAILABLE | 503 | Yes | ERROR | File storage service is unavailable | Dosya depolama servisi kullanilamiyor |
| SYS_FILE_UPLOAD_FAILED | 500 | Yes | ERROR | File upload failed | Dosya yukleme basarisiz oldu |
| SYS_FILE_DOWNLOAD_FAILED | 500 | Yes | ERROR | File download failed | Dosya indirme basarisiz oldu |
| SYS_FILE_DELETE_FAILED | 500 | Yes | ERROR | File deletion failed | Dosya silme basarisiz oldu |
| SYS_STORAGE_QUOTA_EXCEEDED | 507 | No | WARN | Storage quota exceeded | Depolama kotasi asildi |
| SYS_CONFIGURATION_ERROR | 500 | No | CRITICAL | System configuration error | Sistem yapilandirma hatasi |
| SYS_CONFIGURATION_MISSING | 500 | No | CRITICAL | Required configuration value is missing | Gerekli yapilandirma degeri eksik |
| SYS_FEATURE_FLAG_ERROR | 500 | Yes | ERROR | Feature flag evaluation failed | Ozellik bayragi degerlendirmesi basarisiz oldu |
| SYS_RATE_LIMITER_ERROR | 500 | Yes | ERROR | Rate limiter encountered an error | Hiz limitleyici bir hatayla karsilasti |
| SYS_QUEUE_CONNECTION_LOST | 500 | Yes | CRITICAL | Message queue connection lost | Mesaj kuyrugu baglantisi kaybedildi |
| SYS_QUEUE_PUBLISH_FAILED | 500 | Yes | ERROR | Failed to publish message to queue | Kuyruga mesaj yayinlama basarisiz oldu |
| SYS_QUEUE_CONSUME_FAILED | 500 | Yes | ERROR | Failed to consume message from queue | Kuyruktan mesaj tuketme basarisiz oldu |
| SYS_BACKGROUND_JOB_FAILED | 500 | Yes | ERROR | Background job execution failed | Arkaplan isi calistirma basarisiz oldu |
| SYS_SCHEDULED_JOB_FAILED | 500 | Yes | ERROR | Scheduled job execution failed | Planlanmis is calistirma basarisiz oldu |
| SYS_WORKER_POOL_EXHAUSTED | 503 | Yes | CRITICAL | Worker pool is exhausted | Isci havuzu tukendi |
| SYS_THREAD_POOL_EXHAUSTED | 503 | Yes | CRITICAL | Thread pool is exhausted | Iplik havuzu tukendi |
| SYS_MEMORY_LIMIT_EXCEEDED | 503 | Yes | CRITICAL | System memory limit exceeded | Sistem bellek limiti asildi |
| SYS_DISK_SPACE_LOW | 500 | No | CRITICAL | System disk space is critically low | Sistem disk alani kritik dusuk |
| SYS_DISK_SPACE_EXHAUSTED | 507 | No | CRITICAL | System disk space exhausted | Sistem disk alani tukendi |
| SYS_CPU_OVERLOAD | 503 | Yes | WARN | System CPU is overloaded | Sistem CPU'su asiri yuklenmis |
| SYS_SERVICE_UNHEALTHY | 503 | Yes | CRITICAL | Health check indicates service is unhealthy | Saglik kontrolu servisin sagliksiz oldugunu gosteriyor |
| SYS_DEPENDENCY_UNHEALTHY | 503 | Yes | CRITICAL | Critical dependency is unhealthy | Kritik bagimlilik sagliksiz |
| SYS_CIRCUIT_BREAKER_OPEN | 503 | Yes | WARN | Circuit breaker is open for this service | Devre kesici bu servis icin acik |
| SYS_GRACEFUL_SHUTDOWN_INITIATED | 503 | No | INFO | Server is shutting down gracefully | Sunucu duzgun bir sekilde kapatiliyor |
| SYS_MAINTENANCE_MODE | 503 | No | INFO | System is in maintenance mode | Sistem bakim modunda |
| SYS_VERSION_MISMATCH | 500 | No | ERROR | Service version mismatch detected | Servis versiyonu uyusmazligi tespit edildi |
| SYS_INTEGRITY_CHECK_FAILED | 500 | No | CRITICAL | Data integrity check failed | Veri butunlugu kontrolu basarisiz oldu |
| SYS_ENCRYPTION_FAILED | 500 | Yes | ERROR | Data encryption operation failed | Veri sifreleme islemi basarisiz oldu |
| SYS_DECRYPTION_FAILED | 500 | Yes | ERROR | Data decryption operation failed | Veri sifre cozme islemi basarisiz oldu |
| SYS_HASHING_FAILED | 500 | Yes | ERROR | Data hashing operation failed | Veri hashleme islemi basarisiz oldu |
| SYS_AUDIT_LOG_WRITE_FAILED | 500 | Yes | ERROR | Failed to write audit log | Denetim kaydi yazma basarisiz oldu |
| SYS_REQUEST_LOGGING_FAILED | 500 | No | WARN | Failed to log request | Istek kaydetme basarisiz oldu |
| SYS_METRICS_COLLECTION_FAILED | 500 | No | WARN | Metrics collection failed | Metrik toplama basarisiz oldu |
| SYS_TRACING_SPAN_FAILED | 500 | No | WARN | Distributed tracing span creation failed | Dagitik izleme span olusturma basarisiz oldu |
| SYS_LOG_FORWARDING_FAILED | 500 | No | WARN | Log forwarding to external system failed | Harici sisteme log iletme basarisiz oldu |
| SYS_TIME_SYNC_ERROR | 500 | No | WARN | System time synchronization error | Sistem zaman senkronizasyon hatasi |
| SYS_DNS_RESOLUTION_FAILED | 503 | Yes | ERROR | DNS resolution failed for external service | Harici servis icin DNS cozumlemesi basarisiz oldu |
| SYS_NETWORK_CONNECTIVITY_LOST | 503 | Yes | CRITICAL | Network connectivity lost | Ag baglantisi kaybedildi |
| SYS_SSL_CERTIFICATE_ERROR | 500 | No | ERROR | SSL/TLS certificate error | SSL/TLS sertifika hatasi |
| SYS_SSL_CERTIFICATE_EXPIRED | 500 | No | ERROR | SSL/TLS certificate has expired | SSL/TLS sertifikasi suresi dolmus |

### 2.7 Rate Limiting Errors (RAT_xxx)

HTTP 429 Too Many Requests

| Error Code | HTTP | Retryable | Log Level | Description (EN) | Description (TR) |
|------------|------|-----------|-----------|------------------|------------------|
| RATE_LIMIT_GLOBAL | 429 | Yes | WARN | Global rate limit exceeded. Please slow down | Genel hiz limiti asildi. Lutfen yavaslayin |
| RATE_LIMIT_PER_IP | 429 | Yes | WARN | Rate limit exceeded for this IP address | Bu IP adresi icin hiz limiti asildi |
| RATE_LIMIT_PER_USER | 429 | Yes | WARN | Rate limit exceeded for this user | Bu kullanici icin hiz limiti asildi |
| RATE_LIMIT_PER_ENDPOINT | 429 | Yes | WARN | Rate limit exceeded for this endpoint | Bu endpoint icin hiz limiti asildi |
| RATE_LIMIT_LOGIN_ATTEMPTS | 429 | Yes | WARN | Too many login attempts. Try again later | Cok fazla giris denemesi. Daha sonra tekrar deneyin |
| RATE_LIMIT_PASSWORD_RESET | 429 | Yes | WARN | Too many password reset attempts | Cok fazla sifre sifirlama denemesi |
| RATE_LIMIT_REGISTRATION | 429 | Yes | WARN | Too many registration attempts from this IP | Bu IP'den cok fazla kayit denemesi |
| RATE_LIMIT_PAYMENT_ATTEMPTS | 429 | Yes | WARN | Too many payment attempts. Please wait | Cok fazla odeme denemesi. Lutfen bekleyin |
| RATE_LIMIT_API_KEY | 429 | Yes | WARN | API key rate limit exceeded | API anahtari hiz limiti asildi |
| RATE_LIMIT_BURST_EXCEEDED | 429 | Yes | WARN | Burst rate limit exceeded | Patlama hiz limiti asildi |
| RATE_LIMIT_CONCURRENT_REQUESTS | 429 | Yes | WARN | Too many concurrent requests | Cok fazla eszamanli istek |
| RATE_LIMIT_QUOTA_EXHAUSTED | 429 | No | INFO | Monthly API quota exhausted | Aylik API kotasi tukendi |

---

## 3. Circuit Breaker Configuration

### 3.1 Circuit Breaker States

```
CLOSED  -->  OPEN  -->  HALF-OPEN  -->  CLOSED
   |           |            |
   |           |            +-- Success threshold met
   |           +-- Failure threshold reached (5 errors in 60s)
   +-- Normal operation
```

### 3.2 Circuit Breaker Settings

| Parameter | Value | Description |
|-----------|-------|-------------|
| `failure_threshold` | 5 errors | Number of errors to trigger OPEN state |
| `failure_window` | 60 seconds | Time window for counting failures |
| `half_open_timeout` | 30 seconds | Time before attempting HALF-OPEN state |
| `half_open_max_calls` | 3 requests | Max requests allowed in HALF-OPEN state |
| `success_threshold_half_open` | 2 successes | Required successes to close circuit |
| `slow_call_threshold` | 2 seconds | Calls slower than this count as failures |
| `slow_call_rate_threshold` | 50% | Percentage of slow calls to trigger OPEN |

### 3.3 Services with Circuit Breaker Protection

| Service | Failure Threshold | Half-Open Timeout | Recovery Strategy |
|---------|-------------------|-------------------|-------------------|
| Payment Provider | 5 errors/60s | 30s | Exponential backoff |
| SMS Gateway | 3 errors/60s | 20s | Fixed interval 5s |
| Email Service | 5 errors/60s | 30s | Exponential backoff |
| External API | 5 errors/60s | 30s | Exponential backoff |
| Redis Cache | 10 errors/60s | 10s | Immediate retry |
| Database | 3 errors/60s | 5s | Immediate retry |
| File Storage | 5 errors/60s | 30s | Linear backoff |

### 3.4 Errors That Trigger Circuit Breaker

| Error Code | Triggers CB | Service |
|------------|-------------|---------|
| PAYMENT_PROVIDER_UNAVAILABLE | Yes | Payment Provider |
| PAYMENT_PROVIDER_TIMEOUT | Yes | Payment Provider |
| PAYMENT_PROVIDER_ERROR | Yes | Payment Provider |
| PAYMENT_WEBHOOK_PROCESSING_FAILED | Yes | Payment Provider |
| SYS_SMS_SERVICE_TIMEOUT | Yes | SMS Gateway |
| SYS_SMS_SERVICE_ERROR | Yes | SMS Gateway |
| SYS_EMAIL_SERVICE_TIMEOUT | Yes | Email Service |
| SYS_EMAIL_SERVICE_ERROR | Yes | Email Service |
| SYS_EXTERNAL_API_TIMEOUT | Yes | External API |
| SYS_EXTERNAL_API_ERROR | Yes | External API |
| SYS_REDIS_CONNECTION_LOST | Yes | Redis Cache |
| SYS_REDIS_TIMEOUT | Yes | Redis Cache |
| SYS_DATABASE_CONNECTION_LOST | Yes | Database |
| SYS_DATABASE_TIMEOUT | Yes | Database |
| SYS_FILE_STORAGE_UNAVAILABLE | Yes | File Storage |

---

## 4. Retry Strategies

### 4.1 Standard Retry Configuration

```yaml
retry_policies:
  exponential_backoff:
    base_delay: 1s
    max_delay: 30s
    multiplier: 2.0
    max_attempts: 3
    jitter: true
    retryable_status_codes: [500, 502, 503, 504]
  
  fixed_interval:
    delay: 5s
    max_attempts: 3
    retryable_status_codes: [503]
  
  linear_backoff:
    base_delay: 1s
    increment: 2s
    max_delay: 10s
    max_attempts: 3
```

### 4.2 Retryable Errors Matrix

| Error Category | Retryable | Strategy | Max Attempts |
|----------------|-----------|----------|--------------|
| Database Connection Lost | Yes | Immediate | 3 |
| Database Timeout | Yes | Exponential | 3 |
| Redis Connection Lost | Yes | Immediate | 3 |
| External API Timeout | Yes | Exponential | 3 |
| External API Error (5xx) | Yes | Exponential | 3 |
| Payment Provider Timeout | Yes | Exponential | 3 |
| SMS Service Timeout | Yes | Exponential | 3 |
| Email Service Timeout | Yes | Exponential | 3 |
| Validation Errors | No | - | - |
| Authentication Errors | No | - | - |
| Authorization Errors | No | - | - |
| Not Found Errors | No | - | - |
| Conflict Errors | No | - | - |
| Rate Limit Errors | Yes (with delay) | Exponential | 5 |

### 4.3 Retry Headers

When retrying requests, include:

```
X-Retry-Attempt: 1
X-Retry-Max-Attempts: 3
X-Retry-Delay-Ms: 1000
```

---

## 5. Alerting Rules

### 5.1 Critical Alerts (Immediate Response Required)

| Alert Name | Condition | Severity | Notification |
|------------|-----------|----------|--------------|
| `sys_db_connection_failed` | Database connection failure rate > 1% in 5 min | CRITICAL | PagerDuty + SMS |
| `sys_redis_connection_failed` | Redis connection failure rate > 5% in 5 min | CRITICAL | PagerDuty + SMS |
| `sys_payment_provider_down` | Payment provider 100% failure for 3 min | CRITICAL | PagerDuty + SMS |
| `sys_disk_space_critical` | Disk space < 5% | CRITICAL | PagerDuty + Email |
| `sys_memory_critical` | Memory usage > 95% for 5 min | CRITICAL | PagerDuty + Email |
| `sys_5xx_rate_spike` | 5xx error rate > 10% in 5 min | CRITICAL | PagerDuty + Email |
| `sys_circuit_breaker_open` | Multiple circuit breakers open | CRITICAL | PagerDuty + Email |

### 5.2 High Priority Alerts (Response within 15 minutes)

| Alert Name | Condition | Severity | Notification |
|------------|-----------|----------|--------------|
| `api_5xx_rate_high` | 5xx error rate > 2% for 10 min | HIGH | Email + Slack |
| `api_latency_p99_high` | P99 latency > 1s for 10 min | HIGH | Email + Slack |
| `payment_failure_rate_high` | Payment failure rate > 10% for 15 min | HIGH | Email + Slack |
| `auth_failure_rate_high` | Auth failure rate > 20% for 10 min | HIGH | Email + Slack |
| `reservation_conflict_spike` | Reservation conflicts > 10/min for 5 min | HIGH | Email + Slack |
| `background_job_backlog_high` | Job queue depth > 1000 for 10 min | HIGH | Email + Slack |
| `external_api_failure_rate` | External API failure rate > 30% for 10 min | HIGH | Email + Slack |

### 5.3 Medium Priority Alerts (Response within 1 hour)

| Alert Name | Condition | Severity | Notification |
|------------|-----------|----------|--------------|
| `api_5xx_rate_elevated` | 5xx error rate > 1% for 15 min | MEDIUM | Slack |
| `api_latency_p95_high` | P95 latency > 500ms for 15 min | MEDIUM | Slack |
| `rate_limit_hits_spike` | Rate limit responses > 100/min for 10 min | MEDIUM | Slack |
| `validation_error_spike` | Validation errors > 50/min for 10 min | MEDIUM | Slack |
| `sms_delivery_failure_high` | SMS delivery failure rate > 20% | MEDIUM | Slack |
| `email_delivery_failure_high` | Email delivery failure rate > 10% | MEDIUM | Slack |
| `cache_miss_rate_high` | Cache miss rate > 80% for 15 min | MEDIUM | Slack |

### 5.4 Low Priority Alerts (Response within 4 hours)

| Alert Name | Condition | Severity | Notification |
|------------|-----------|----------|--------------|
| `slow_query_detected` | Query duration > 5s | LOW | Slack |
| `circuit_breaker_half_open` | Circuit breaker entered half-open state | LOW | Slack |
| `deprecated_api_usage` | Deprecated endpoint still receiving traffic | LOW | Email |
| `ssl_cert_expiry_warning` | SSL certificate expires in < 30 days | LOW | Email |

### 5.5 Alert Suppression Rules

```yaml
suppression_rules:
  maintenance_window:
    enabled: true
    schedule: "0 3 * * 0"  # Sundays 3 AM
    duration: 2h
    suppressed_alerts:
      - api_5xx_rate_high
      - api_latency_p99_high
  
  dependency_failure_cascade:
    enabled: true
    rule: If payment_provider_down, suppress payment_failure_rate_high
```

---

## 6. Client-Side Error Handling

### 6.1 HTTP Status Code Handling Guide

| Status Code | Client Action | User Message (TR) |
|-------------|---------------|-------------------|
| 400 | Show validation error, focus on field | "Bazi bilgiler eksik veya hatali. Lutfen kontrol edin." |
| 401 | Redirect to login, clear local token | "Oturumunuz sona erdi. Lutfen tekrar giris yapin." |
| 403 | Show permission error, disable action | "Bu islem icin yetkiniz bulunmuyor." |
| 404 | Show not found, suggest alternatives | "Aradiginiz sayfa veya kayit bulunamadi." |
| 409 | Show conflict, suggest alternatives | "Bu islem baska bir kullanici tarafindan engellendi." |
| 410 | Show expired, suggest restart | "Islem suresi doldu. Lutfen bastan baslayin." |
| 422 | Show field-specific errors | "Bazi bilgiler gecersiz. Kirmizi alanlari kontrol edin." |
| 429 | Show rate limit, add countdown timer | "Cok fazla istek gonderildi. Lutfen {seconds} saniye bekleyin." |
| 500 | Show generic error, enable retry | "Bir hata olustu. Lutfen tekrar deneyin." |
| 502 | Show service unavailable, enable retry | "Servis gecici olarak kullanilamiyor. Tekrar deneyin." |
| 503 | Show maintenance message | "Sistem gecici olarak bakimda. Kisa sure sonra tekrar deneyin." |
| 504 | Show timeout, enable retry | "Islem zaman asimina ugradi. Lutfen tekrar deneyin." |

### 6.2 Error Code Specific Handling

```typescript
// Example error handler
function handleApiError(error: ApiError): void {
  const { code, retryable } = error;
  
  switch (code) {
    case 'AUTH_TOKEN_EXPIRED':
      // Refresh token or redirect to login
      authService.refreshToken().catch(() => router.push('/login'));
      break;
      
    case 'RES_HOLD_EXPIRED':
      // Clear reservation state, redirect to search
      reservationStore.clear();
      router.push('/search');
      toast.error('Rezervasyon tutma suresi doldu. Lutfen tekrar arama yapin.');
      break;
      
    case 'PAYMENT_3DS_FAILED':
      // Show payment retry options
      showPaymentRetryModal(error);
      break;
      
    case 'PAYMENT_PROVIDER_UNAVAILABLE':
    case 'PAYMENT_PROVIDER_TIMEOUT':
      // Auto-retry with exponential backoff
      if (retryable && retryCount < 3) {
        setTimeout(() => retryPayment(), 1000 * Math.pow(2, retryCount));
      } else {
        showPaymentAlternativeOptions();
      }
      break;
      
    case 'RATE_LIMIT_GLOBAL':
    case 'RATE_LIMIT_PER_IP':
    case 'RATE_LIMIT_PER_USER':
      // Show countdown and disable submit
      const retryAfter = error.details?.retry_after || 60;
      showRateLimitModal(retryAfter);
      break;
      
    case 'SYS_CIRCUIT_BREAKER_OPEN':
      // Show service temporarily unavailable
      toast.warning('Servis gecici olarak kullanilamiyor. Lutfen biraz bekleyin.');
      break;
      
    default:
      if (retryable) {
        showRetryButton();
      } else {
        showErrorMessage(error.message_tr || error.message);
      }
  }
}
```

### 6.3 Retry Logic Implementation

```typescript
interface RetryConfig {
  maxAttempts: number;
  baseDelay: number;
  maxDelay: number;
  backoffMultiplier: number;
}

async function retryableRequest<T>(
  requestFn: () => Promise<T>,
  config: RetryConfig = {
    maxAttempts: 3,
    baseDelay: 1000,
    maxDelay: 30000,
    backoffMultiplier: 2
  }
): Promise<T> {
  let lastError: Error;
  
  for (let attempt = 1; attempt <= config.maxAttempts; attempt++) {
    try {
      return await requestFn();
    } catch (error) {
      lastError = error;
      
      // Don't retry if not retryable
      if (!error.retryable) {
        throw error;
      }
      
      // Don't retry on last attempt
      if (attempt === config.maxAttempts) {
        break;
      }
      
      // Calculate delay with exponential backoff and jitter
      const delay = Math.min(
        config.baseDelay * Math.pow(config.backoffMultiplier, attempt - 1),
        config.maxDelay
      );
      const jitter = Math.random() * 0.3 * delay; // 30% jitter
      
      await sleep(delay + jitter);
    }
  }
  
  throw lastError;
}
```

### 6.4 Error Tracking for Client

```typescript
// Send error to monitoring service
function trackError(error: ApiError, context: ErrorContext): void {
  monitoring.track({
    type: 'api_error',
    code: error.code,
    correlation_id: error.correlation_id,
    endpoint: context.endpoint,
    method: context.method,
    user_agent: navigator.userAgent,
    timestamp: new Date().toISOString(),
    retryable: error.retryable,
    user_id: authService.getCurrentUser()?.id
  });
}
```

---

## 7. Logging Standards

### 7.1 Structured JSON Log Format

```json
{
  "timestamp": "2026-03-15T14:30:00.000Z",
  "level": "ERROR",
  "correlation_id": "550e8400-e29b-41d4-a716-446655440000",
  "request_id": "req_abc123",
  "service": "reservation-api",
  "environment": "production",
  "error": {
    "code": "RES_VEHICLE_UNAVAILABLE",
    "message": "Vehicle is no longer available for selected dates",
    "retryable": false,
    "http_status": 409
  },
  "context": {
    "endpoint": "POST /api/v1/reservations",
    "user_id": "user_123",
    "vehicle_id": "veh_456",
    "pickup_date": "2026-04-01",
    "return_date": "2026-04-05"
  },
  "performance": {
    "duration_ms": 145,
    "db_query_count": 3,
    "db_query_duration_ms": 45
  },
  "source": {
    "file": "ReservationService.cs",
    "line": 234,
    "function": "CreateReservation"
  }
}
```

### 7.2 Log Levels

| Level | Usage | Retention |
|-------|-------|-----------|
| `CRITICAL` | System down, data corruption, security breach | 1 year |
| `ERROR` | Failed operations, exceptions | 90 days |
| `WARN` | Recoverable issues, degradations | 30 days |
| `INFO` | Successful operations, state changes | 14 days |
| `DEBUG` | Detailed debugging info | 7 days |
| `TRACE` | Request/response details | 3 days |

### 7.3 Sensitive Data Masking

```yaml
masked_fields:
  - password
  - credit_card_number
  - cvv
  - identity_number
  - phone_number
  - email
  - token
  - authorization
  - cookie

masking_rules:
  credit_card_number: "show_last_4"
  phone_number: "show_last_4"
  email: "show_domain_only"
  identity_number: "full_mask"
```

### 7.4 Correlation ID Propagation

```
Client Request
    |
    +-- X-Correlation-ID: abc-123
    |
    v
API Gateway (preserves or generates)
    |
    +-- Correlation-ID: abc-123
    |
    v
Service A
    |
    +-- Logs with correlation_id=abc-123
    +-- Calls Service B with X-Correlation-ID: abc-123
    |
    v
Service B
    |
    +-- Logs with correlation_id=abc-123
    +-- Calls Database
    |
    v
Database Query Log
    +-- correlation_id=abc-123
```

---

## 8. Monitoring Requirements

### 8.1 Key Metrics

| Metric | Type | Alert Threshold | Dashboard |
|--------|------|-----------------|-----------|
| `api_requests_total` | Counter | - | Yes |
| `api_request_duration_seconds` | Histogram | P99 > 300ms | Yes |
| `api_errors_total` | Counter | Rate > 2% | Yes |
| `api_errors_by_code` | Counter | Any spike | Yes |
| `http_requests_by_status` | Counter | 5xx > 1% | Yes |
| `database_query_duration_seconds` | Histogram | P99 > 100ms | Yes |
| `database_connections_active` | Gauge | > 80% of max | Yes |
| `cache_hit_ratio` | Gauge | < 70% | Yes |
| `payment_success_rate` | Gauge | < 95% | Yes |
| `circuit_breaker_state` | Gauge | OPEN state | Yes |
| `background_jobs_pending` | Gauge | > 1000 | Yes |
| `background_jobs_failed` | Counter | Rate > 5/min | Yes |

### 8.2 Health Check Endpoints

```
GET /health/live     # Liveness probe - is the process running?
GET /health/ready    # Readiness probe - can it accept traffic?
GET /health/deep     # Deep health - check all dependencies
```

**Liveness Check Response:**
```json
{
  "status": "alive",
  "timestamp": "2026-03-15T14:30:00.000Z",
  "uptime_seconds": 86400
}
```

**Readiness Check Response:**
```json
{
  "status": "ready",
  "checks": {
    "database": "ok",
    "redis": "ok",
    "payment_provider": "ok"
  }
}
```

**Deep Health Check Response:**
```json
{
  "status": "healthy",
  "timestamp": "2026-03-15T14:30:00.000Z",
  "checks": {
    "database": { "status": "ok", "latency_ms": 15 },
    "redis": { "status": "ok", "latency_ms": 5 },
    "payment_provider": { "status": "ok", "latency_ms": 120 },
    "sms_gateway": { "status": "ok", "latency_ms": 45 },
    "email_service": { "status": "ok", "latency_ms": 80 }
  }
}
```

### 8.3 Distributed Tracing

**Trace Context Headers:**
```
traceparent: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
tracestate: vendor=value
```

**Span Tags:**
- `error.code` - Application error code
- `error.retryable` - Whether error is retryable
- `http.status_code` - HTTP response status
- `db.system` - Database type (postgresql)
- `db.statement` - SQL query (sanitized)
- `cache.operation` - Cache operation type
- `payment.provider` - Payment provider name

### 8.4 Error Budget

| Service | SLO | Error Budget | Period |
|---------|-----|--------------|--------|
| API Availability | 99.9% | 0.1% downtime | 30 days |
| API Latency (P99) | < 300ms | 1% over threshold | 30 days |
| Payment Success Rate | > 95% | 5% failures | 30 days |
| 5xx Error Rate | < 0.1% | 0.1% of requests | 24 hours |

---

## 9. Quick Reference

### 9.1 HTTP Status Codes Summary

| Code | Meaning | Common Error Codes |
|------|---------|-------------------|
| 400 | Bad Request | VAL_*, RES_INVALID_* |
| 401 | Unauthorized | AUTH_TOKEN_* |
| 403 | Forbidden | AUTH_*_ONLY, AUTH_INSUFFICIENT_* |
| 404 | Not Found | VEH_NOT_FOUND, RES_*_NOT_FOUND |
| 409 | Conflict | RES_OVERLAPPING_*, RES_ALREADY_* |
| 410 | Gone | RES_HOLD_EXPIRED, PAYMENT_INTENT_EXPIRED |
| 413 | Payload Too Large | VAL_REQUEST_SIZE_TOO_LARGE |
| 415 | Unsupported Media Type | VAL_CONTENT_TYPE_UNSUPPORTED |
| 422 | Validation Error | VAL_VALIDATION_FAILED |
| 429 | Too Many Requests | RATE_LIMIT_*, AUTH_RATE_LIMIT_* |
| 500 | Internal Error | SYS_INTERNAL_ERROR, SYS_DATABASE_* |
| 502 | Bad Gateway | SYS_EXTERNAL_API_ERROR |
| 503 | Service Unavailable | SYS_*_UNAVAILABLE, SYS_CIRCUIT_BREAKER_OPEN |
| 504 | Gateway Timeout | SYS_*_TIMEOUT, PAYMENT_PROVIDER_TIMEOUT |
| 507 | Insufficient Storage | SYS_STORAGE_QUOTA_EXCEEDED |

### 9.2 Error Code Prefix Guide

| Prefix | Category | Example |
|--------|----------|---------|
| `AUTH_` | Authentication/Authorization | AUTH_TOKEN_EXPIRED |
| `RES_` | Reservation Operations | RES_VEHICLE_UNAVAILABLE |
| `PAYMENT_` | Payment Processing | PAYMENT_CARD_DECLINED |
| `VEH_` | Vehicle/Fleet Management | VEH_NOT_FOUND |
| `VAL_` | Input Validation | VAL_INVALID_EMAIL_FORMAT |
| `SYS_` | System/Infrastructure | SYS_DATABASE_CONNECTION_LOST |
| `RATE_LIMIT_` | Rate Limiting | RATE_LIMIT_GLOBAL |

---

END OF DOCUMENT
