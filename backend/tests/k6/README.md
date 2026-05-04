# k6 Load Tests

Load testing scripts for the RentACar backend API.

## Prerequisites

- [k6](https://k6.io/docs/get-started/installation/) installed
- Backend running locally or staging environment

## Quick Start

```bash
# Run all scenarios sequentially
./run-all.sh

# Or run individually
k6 run --env BASE_URL=http://localhost:5000 availability-query.js
k6 run --env BASE_URL=http://localhost:5000 concurrent-search.js
k6 run --env BASE_URL=http://localhost:5000 concurrent-booking.js
k6 run --env BASE_URL=http://localhost:5000 payment-intent.js
k6 run --env BASE_URL=http://localhost:5000 --env ADMIN_EMAIL=admin@rentacar.test --env ADMIN_PASSWORD=password admin-dashboard.js
k6 run --env BASE_URL=http://localhost:5000 --env ADMIN_EMAIL=admin@rentacar.test --env ADMIN_PASSWORD=password mixed-traffic.js
```

## Scenarios

| Script | Duration | Max VUs | Target |
|--------|----------|---------|--------|
| `availability-query.js` | 5m | 50 | p95 < 300ms |
| `concurrent-search.js` | 5m | 100 | p95 < 500ms, cache hit > 80% |
| `concurrent-booking.js` | 10m | 50 | 0 double-booking |
| `payment-intent.js` | 5m | 20 | Idempotency preserved |
| `admin-dashboard.js` | 5m | 20 | p95 < 500ms |
| `mixed-traffic.js` | 10m | 100 | 70% search, 20% booking, 10% admin |

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `BASE_URL` | `http://localhost:5000` | Backend API base URL |
| `ADMIN_EMAIL` | `admin@rentacar.test` | Admin login email |
| `ADMIN_PASSWORD` | `password` | Admin login password |

## Acceptance Criteria

| Metric | Threshold |
|--------|-----------|
| HTTP Error Rate | < 1% |
| p95 Response Time (API) | < 500ms |
| p99 Response Time (API) | < 1000ms |
| Database Connection Pool | < 80% |
| CPU Usage | < 80% |
| Memory Usage | < 80% |
| Double Booking Incidents | 0 |

## Results

Test results are written to `results/*.json` and printed to stdout in summary format.
