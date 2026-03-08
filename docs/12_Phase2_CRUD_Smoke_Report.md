# Faz 2 CRUD Smoke Report

**Calisma Tarihi (UTC):** 2026-03-08T11:31:56Z  
**Base URL:** `http://127.0.0.1:5099`  
**Veritabani:** Docker Compose PostgreSQL (`localhost:5433`)  
**Kimlik Dogrulama:** Gercek HTTP istekleri + HS256 admin JWT smoke token

## Ozet

Bu smoke turunda Faz 2 kapsamindaki admin CRUD endpointleri gercek HTTP cagrilari ile dogrulandi. Arac grubu, ofis ve arac create/read/update akislari; arac delete, status, transfer ve maintenance endpointleri; ayrica public availability endpointi ayni oturum icinde test edildi.

## Endpoint Sonuclari

| Adim | Method | Path | HTTP | Gozlem |
|---|---|---|---|---|
| Admin auth context | `GET` | `/api/admin/v1/auth/me` | `200` | `role=Admin; email=smoke-admin@local.test` |
| Vehicle groups list | `GET` | `/api/admin/v1/vehicle-groups` | `200` | `count=3` |
| Vehicle group create | `POST` | `/api/admin/v1/vehicle-groups` | `200` | `id=02713daa-2360-4f02-97ed-48bb71670319` |
| Vehicle group update | `PUT` | `/api/admin/v1/vehicle-groups/02713daa-2360-4f02-97ed-48bb71670319` | `200` | `deposit=8000; minAge=26` |
| Office list | `GET` | `/api/admin/v1/offices` | `200` | `count=4` |
| Office create A | `POST` | `/api/admin/v1/offices` | `200` | `id=fb030a3b-d989-44bf-ba34-36b9c0a8721e` |
| Office update A | `PUT` | `/api/admin/v1/offices/fb030a3b-d989-44bf-ba34-36b9c0a8721e` | `200` | `openingHours=07:00-23:00` |
| Office create B | `POST` | `/api/admin/v1/offices` | `200` | `id=86571d35-4422-4318-b574-cb4cedba8a32` |
| Vehicle list before create | `GET` | `/api/admin/v1/vehicles` | `200` | `count=0` |
| Vehicle create | `POST` | `/api/admin/v1/vehicles` | `200` | `id=2d6a8854-5fb2-43e8-9b85-ebdde27416cb; plate=34SMK20260308143155` |
| Vehicle update | `PUT` | `/api/admin/v1/vehicles/2d6a8854-5fb2-43e8-9b85-ebdde27416cb` | `200` | `model=Clio Touch; color=Black` |
| Vehicle status -> Reserved | `PATCH` | `/api/admin/v1/vehicles/2d6a8854-5fb2-43e8-9b85-ebdde27416cb/status` | `200` | `status=1` |
| Vehicle status -> Available | `PATCH` | `/api/admin/v1/vehicles/2d6a8854-5fb2-43e8-9b85-ebdde27416cb/status` | `200` | `status=0` |
| Vehicle list after create | `GET` | `/api/admin/v1/vehicles` | `200` | `containsCreated=True; count=1` |
| Public availability before transfer | `GET` | `/api/v1/vehicles/available` | `200` | `officeA.availableCount=1` |
| Vehicle transfer | `POST` | `/api/admin/v1/vehicles/2d6a8854-5fb2-43e8-9b85-ebdde27416cb/transfer` | `200` | `officeId=86571d35-4422-4318-b574-cb4cedba8a32` |
| Availability after transfer | `GET` | `/api/v1/vehicles/available` | `200` | `officeA.availableCount=0; officeB.availableCount=1` |
| Vehicle maintenance schedule | `POST` | `/api/admin/v1/vehicles/2d6a8854-5fb2-43e8-9b85-ebdde27416cb/maintenance` | `200` | `status=3` |
| Availability after maintenance | `GET` | `/api/v1/vehicles/available` | `200` | `officeB.availableCount=0` |
| Vehicle delete | `DELETE` | `/api/admin/v1/vehicles/2d6a8854-5fb2-43e8-9b85-ebdde27416cb` | `200` | `deletedId=2d6a8854-5fb2-43e8-9b85-ebdde27416cb` |
| Vehicle list after delete | `GET` | `/api/admin/v1/vehicles` | `200` | `containsDeleted=False; count=0` |

## Sonuc

Faz 2 kapsamindaki CRUD smoke testi gercek HTTP istekleriyle basarili sekilde tamamlandi. Vehicle availability akisi da ayni turda dogrulandi:

- create sonrasi kaynak ofiste sayim `1`
- transfer sonrasi kaynak ofiste `0`, hedef ofiste `1`
- maintenance sonrasi hedef ofiste `0`
