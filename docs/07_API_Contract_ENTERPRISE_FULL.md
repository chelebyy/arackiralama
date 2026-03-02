# API Contract (OpenAPI Specification)

Date: 2026-02-25
Version: 1.0.0
Base URL: /api/v1

------------------------------------------------------------------------

# 1. Authentication

All endpoints (except public vehicle search) require JWT Bearer token:

```
Authorization: Bearer {jwt_token}
```

Token obtained from: `POST /api/v1/auth/login`

Token expiration: 24 hours
Refresh token expiration: 7 days

------------------------------------------------------------------------

# 2. Common Response Models

## 2.1 Success Response

```json
{
  "success": true,
  "data": { ... },
  "meta": {
    "page": 1,
    "per_page": 20,
    "total": 100,
    "total_pages": 5
  }
}
```

## 2.2 Error Response

```json
{
  "success": false,
  "error": {
    "code": "RESERVATION_CONFLICT",
    "message": "Vehicle unavailable for selected dates",
    "correlationId": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

------------------------------------------------------------------------

# 3. Public Endpoints (No Auth Required)

## GET /api/v1/vehicles/available

Search available vehicles for date range.

### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| pickup_datetime | ISO8601 | Yes | Pickup date/time (UTC) |
| return_datetime | ISO8601 | Yes | Return date/time (UTC) |
| vehicle_group_id | UUID | No | Filter by specific group |

### Response 200 OK

```json
{
  "success": true,
  "data": [
    {
      "group_id": "550e8400-e29b-41d4-a716-446655440001",
      "group_name": "Ekonomi",
      "group_name_en": "Economy",
      "available_count": 3,
      "daily_price": 750.00,
      "currency": "TRY",
      "deposit_amount": 2000.00,
      "min_age": 21,
      "min_license_years": 2,
      "features": ["Otomatik", "Klima", "5 Kişi"],
      "image_url": "/images/groups/economy.jpg"
    }
  ]
}
```

## GET /api/v1/vehicles/groups

List all vehicle groups with details.

### Response 200 OK

```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "name_tr": "Ekonomi",
      "name_en": "Economy",
      "name_ru": "Эконом",
      "name_ar": "اقتصادية",
      "name_de": "Economy",
      "description_tr": "Günlük kullanım için ideal",
      "daily_base_price": 750.00,
      "deposit_amount": 2000.00
    }
  ]
}
```

------------------------------------------------------------------------

# 4. Reservation Endpoints

## POST /api/v1/reservations

Create new reservation (Draft status).

### Request Body

```json
{
  "pickup_datetime": "2026-04-01T10:00:00Z",
  "return_datetime": "2026-04-05T10:00:00Z",
  "pickup_office_id": "550e8400-e29b-41d4-a716-446655440010",
  "return_office_id": "550e8400-e29b-41d4-a716-446655440010",
  "vehicle_group_id": "550e8400-e29b-41d4-a716-446655440001",
  "customer": {
    "full_name": "John Doe",
    "phone": "+905551234567",
    "email": "john@example.com",
    "birth_date": "1990-01-01",
    "license_year": 2015,
    "identity_number": "12345678901",
    "nationality": "TR"
  },
  "flight_number": "TK1234",
  "extras": ["additional_driver", "child_seat"],
  "notes": "Havalimanı teslimatı",
  "campaign_code": "SPRING2026"
}
```

### Response 201 Created

```json
{
  "success": true,
  "data": {
    "reservation_id": "550e8400-e29b-41d4-a716-446655440100",
    "public_code": "ABC123",
    "status": "Draft",
    "created_at": "2026-03-15T14:30:00Z",
    "expires_at": "2026-03-15T14:45:00Z",
    "total_amount": 3750.00,
    "deposit_amount": 2000.00,
    "currency": "TRY",
    "pricing_breakdown": {
      "daily_rate": 750.00,
      "rental_days": 4,
      "base_total": 3000.00,
      "extras_total": 250.00,
      "campaign_discount": -250.00,
      "airport_fee": 250.00,
      "final_total": 3750.00
    }
  }
}
```

### Error Responses

- `422 VALIDATION_ERROR` - Missing required fields
- `409 RESERVATION_CONFLICT` - Customer already has active reservation
- `400 INVALID_DATETIME` - Return before pickup

## POST /api/v1/reservations/{id}/hold

Place 15-minute hold on reservation.

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "hold_expires_at": "2026-03-15T14:45:00Z",
    "checkout_url": "/checkout/ABC123",
    "seconds_remaining": 900
  }
}
```

### Error Responses

- `409 HOLD_UNAVAILABLE` - Vehicle no longer available
- `409 RESERVATION_EXPIRED` - Reservation already expired

## GET /api/v1/reservations/{publicCode}

Track reservation by public code (no auth required).

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "public_code": "ABC123",
    "status": "Paid",
    "pickup_datetime": "2026-04-01T10:00:00Z",
    "return_datetime": "2026-04-05T10:00:00Z",
    "vehicle_group": "Ekonomi",
    "total_amount": 3750.00,
    "deposit_status": "PreAuthorized"
  }
}
```

------------------------------------------------------------------------

# 5. Payment Endpoints

## POST /api/v1/payments/intents

Create payment intent for reservation.

### Request Body

```json
{
  "reservation_id": "550e8400-e29b-41d4-a716-446655440100",
  "idempotency_key": "unique-key-per-attempt-uuid",
  "installment_count": 1,
  "card": {
    "holder_name": "John Doe",
    "number": "4111111111111111",
    "expiry_month": "12",
    "expiry_year": "2028",
    "cvv": "123"
  }
}
```

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "payment_intent_id": "550e8400-e29b-41d4-a716-446655440200",
    "status": "Pending3DS",
    "redirect_url": "https://bank-3ds.example.com/secure?id=xyz",
    "amount": 3750.00,
    "currency": "TRY",
    "expires_at": "2026-03-15T15:00:00Z"
  }
}
```

### Response 200 OK (3D Secure Completed)

```json
{
  "success": true,
  "data": {
    "payment_intent_id": "550e8400-e29b-41d4-a716-446655440200",
    "status": "Succeeded",
    "amount": 3750.00,
    "transaction_id": "bank-txn-12345",
    "reservation_status": "Paid"
  }
}
```

## POST /api/v1/payments/{intentId}/3ds-return

Return URL after 3D Secure authentication.

### Request Body

```json
{
  "bank_response": "encrypted-payload-from-bank"
}
```

## POST /api/v1/payments/webhook/{provider}

Provider webhook endpoint (signature verified).

### Headers

```
X-Webhook-Signature: sha256=signature
X-Webhook-Event: payment.succeeded
```

### Request Body

Provider-specific payload. Signature verified before processing.

### Response 200 OK

```json
{
  "success": true,
  "message": "Webhook processed"
}
```

### Response 400 Bad Request

```json
{
  "success": false,
  "error": {
    "code": "INVALID_WEBHOOK_SIGNATURE",
    "message": "Signature verification failed"
  }
}
```

------------------------------------------------------------------------

# 6. Admin Endpoints (admin.domain.com/api/admin/v1/)

All admin endpoints require `Admin` or `SuperAdmin` role.

## POST /api/admin/v1/auth/login

Admin login.

### Request Body

```json
{
  "email": "admin@alanyarentacar.com",
  "password": "securePassword123"
}
```

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refresh_token": "eyJhbGciOiJIUzI1NiIs...",
    "user": {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "email": "admin@alanyarentacar.com",
      "full_name": "Admin User",
      "role": "Admin"
    }
  }
}
```

## GET /api/admin/v1/reservations

List all reservations with filters.

### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| status | string | Filter by status (Draft, Hold, Paid, Active, Completed, Cancelled) |
| date_from | date | Filter by pickup date (YYYY-MM-DD) |
| date_to | date | Filter by return date (YYYY-MM-DD) |
| search | string | Search by customer name, email, or public code |
| page | integer | Page number (default: 1) |
| per_page | integer | Items per page (default: 20, max: 100) |

### Response 200 OK

```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440100",
      "public_code": "ABC123",
      "customer_name": "John Doe",
      "customer_phone": "+905551234567",
      "vehicle_group": "Ekonomi",
      "pickup_datetime": "2026-04-01T10:00:00Z",
      "return_datetime": "2026-04-05T10:00:00Z",
      "status": "Paid",
      "total_amount": 3750.00,
      "created_at": "2026-03-15T14:30:00Z"
    }
  ],
  "meta": {
    "page": 1,
    "per_page": 20,
    "total": 150,
    "total_pages": 8
  }
}
```

## GET /api/admin/v1/reservations/{id}

Get reservation details.

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440100",
    "public_code": "ABC123",
    "status": "Paid",
    "customer": {
      "full_name": "John Doe",
      "phone": "+905551234567",
      "email": "john@example.com",
      "birth_date": "1990-01-01",
      "license_year": 2015
    },
    "vehicle_group": {
      "id": "...",
      "name": "Ekonomi"
    },
    "assigned_vehicle": {
      "id": "...",
      "plate": "34 ABC 123"
    },
    "pickup": {
      "datetime": "2026-04-01T10:00:00Z",
      "office": "Alanya Havalimanı"
    },
    "return": {
      "datetime": "2026-04-05T10:00:00Z",
      "office": "Alanya Havalimanı"
    },
    "payment": {
      "total": 3750.00,
      "paid": 3750.00,
      "deposit": 2000.00,
      "deposit_status": "PreAuthorized",
      "transaction_id": "bank-txn-12345"
    },
    "audit_log": [
      {
        "action": "ReservationCreated",
        "timestamp": "2026-03-15T14:30:00Z",
        "user": "system"
      },
      {
        "action": "PaymentReceived",
        "timestamp": "2026-03-15T14:35:00Z",
        "user": "system"
      }
    ]
  }
}
```

## POST /api/admin/v1/reservations/{id}/cancel

Cancel reservation with reason.

### Request Body

```json
{
  "reason": "Customer request",
  "cancel_fees": 0.00,
  "refund_amount": 3750.00
}
```

## POST /api/admin/v1/reservations/{id}/refund

Process refund for paid reservation.

### Request Body

```json
{
  "amount": 3750.00,
  "reason": "Cancellation",
  "notify_customer": true
}
```

## POST /api/admin/v1/reservations/{id}/release-deposit

Release deposit pre-authorization.

### Request Body

```json
{
  "reason": "Vehicle returned in good condition"
}
```

## GET /api/admin/v1/vehicles

List all vehicles.

## POST /api/admin/v1/vehicles

Create new vehicle.

### Request Body

```json
{
  "plate": "34 ABC 123",
  "group_id": "550e8400-e29b-41d4-a716-446655440001",
  "brand": "Renault",
  "model": "Clio",
  "year": 2024,
  "color": "Beyaz",
  "current_office_id": "550e8400-e29b-41d4-a716-446655440010",
  "status": "Available",
  "daily_price": 750.00
}
```

## PUT /api/admin/v1/vehicles/{id}

Update vehicle details.

## GET /api/admin/v1/dashboard

Admin dashboard statistics.

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "today": {
      "pickups": 5,
      "returns": 3,
      "new_reservations": 8
    },
    "active_reservations": 45,
    "pending_payments": 3,
    "revenue_today": 15000.00,
    "revenue_this_month": 450000.00
  }
}
```

------------------------------------------------------------------------

# 7. Error Codes Reference

| Code | HTTP Status | Description | When Occurs |
|------|-------------|-------------|-------------|
| VALIDATION_ERROR | 422 | Request validation failed | Missing required fields, invalid format |
| UNAUTHORIZED | 401 | Authentication required | Missing/invalid JWT token |
| FORBIDDEN | 403 | Permission denied | Valid token but insufficient role |
| RESERVATION_CONFLICT | 409 | Vehicle unavailable | Overlapping reservation exists |
| HOLD_UNAVAILABLE | 409 | Cannot place hold | Vehicle no longer available |
| HOLD_EXPIRED | 409 | Hold expired | 15-minute window passed |
| INVALID_CAMPAIGN | 400 | Campaign code invalid | Code not found or expired |
| PAYMENT_FAILED | 402 | Payment declined | Bank declined transaction |
| INVALID_WEBHOOK_SIGNATURE | 400 | Webhook verification failed | Signature mismatch |
| RATE_LIMIT_EXCEEDED | 429 | Too many requests | Exceeded rate limit |
| INTERNAL_ERROR | 500 | Server error | Unexpected system error |

------------------------------------------------------------------------

# 8. Rate Limiting

| Endpoint | Limit | Window |
|----------|-------|--------|
| POST /auth/login | 5 | 1 minute |
| POST /payments/* | 10 | 1 minute |
| All other endpoints | 100 | 1 minute |

Rate limit headers included in response:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1647356400
```

------------------------------------------------------------------------

END OF DOCUMENT
