import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PICKUP_OFFICE_ID = __ENV.PICKUP_OFFICE_ID || '11111111-1111-1111-1111-111111111111';
const RETURN_OFFICE_ID = __ENV.RETURN_OFFICE_ID || '11111111-1111-1111-1111-111111111112';
const DEFAULT_VEHICLE_GROUP_ID = __ENV.VEHICLE_GROUP_ID || '22222222-2222-2222-2222-222222222221';
const SMOKE_MODE = __ENV.SMOKE_MODE === '1';

export const options = SMOKE_MODE
  ? {
      stages: [
        { duration: '20s', target: 1 },
        { duration: '60s', target: 1 },
        { duration: '20s', target: 1 },
        { duration: '10s', target: 0 },
      ],
      thresholds: {
        http_req_duration: ['p(95)<1500'],
        http_req_failed: ['rate<0.01'],
      },
    }
  : {
      stages: [
        { duration: '2m', target: 10 },
        { duration: '5m', target: 50 },
        { duration: '2m', target: 50 },
        { duration: '1m', target: 0 },
      ],
      thresholds: {
        http_req_duration: ['p(95)<1000'],
        http_req_failed: ['rate<0.01'],
      },
    };

function formatDate(d) {
  return d.toISOString().split('T')[0];
}

function randomUUID() {
  // Deterministic UUID for load testing — unique per VU/iteration.
  // Not cryptographically secure; acceptable for test session IDs.
  const vu = String(__VU).padStart(4, '0');
  const iter = String(__ITER).padStart(8, '0');
  const ts = Date.now();
  return `load-${vu}-${iter}-${ts}`;
}

export default function () {
  const now = new Date();
  const pickup = new Date(now.getTime() + 2 * 24 * 60 * 60 * 1000);
  const returnDate = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
  const sessionId = randomUUID();

  // 1. Search availability
  const searchQuery = [
    `office_id=${encodeURIComponent(PICKUP_OFFICE_ID)}`,
    `pickup_datetime=${encodeURIComponent(`${formatDate(pickup)}T10:00:00Z`)}`,
    `return_datetime=${encodeURIComponent(`${formatDate(returnDate)}T10:00:00Z`)}`,
  ];
  const searchUrl = `${BASE_URL}/api/v1/vehicles/available?${searchQuery.join('&')}`;
  const searchRes = http.get(searchUrl, { headers: { Accept: 'application/json' } });

  check(searchRes, {
    'search status is 200': (r) => r.status === 200,
  });

  if (searchRes.status !== 200) {
    sleep(1);
    return;
  }

  let vehicleGroupId;
  try {
    const body = JSON.parse(searchRes.body);
    const items = body.data || body;
    if (Array.isArray(items) && items.length > 0) {
      vehicleGroupId = items[0].groupId || items[0].id || items[0].vehicleGroupId;
    }
  } catch {
    vehicleGroupId = DEFAULT_VEHICLE_GROUP_ID;
  }

  // 2. Create reservation
  const customerEmail = `loadtest-${__VU}-${__ITER}@example.com`;
  const reservationPayload = JSON.stringify({
    vehicleGroupId: vehicleGroupId || DEFAULT_VEHICLE_GROUP_ID,
    pickupOfficeId: PICKUP_OFFICE_ID,
    returnOfficeId: RETURN_OFFICE_ID,
    pickupDateTimeUtc: `${formatDate(pickup)}T10:00:00Z`,
    returnDateTimeUtc: `${formatDate(returnDate)}T10:00:00Z`,
    customer: {
      FirstName: 'Load',
      LastName: 'Test',
      Email: customerEmail,
      Phone: '+905551234567',
      DateOfBirth: '1990-01-01',
      IdentityNumber: '11111111111',
      DriverLicenseNumber: 'TR-123456',
    },
    extraDriverCount: 0,
    childSeatCount: 0,
    sessionId,
  });

  const createRes = http.post(`${BASE_URL}/api/v1/reservations`, reservationPayload, {
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
  });

  check(createRes, {
    'create status is 201 or 200': (r) => r.status === 201 || r.status === 200,
  });

  if (createRes.status !== 201 && createRes.status !== 200) {
    sleep(1);
    return;
  }

  let reservationId;
  try {
    const body = JSON.parse(createRes.body);
    const data = body.data || body;
    reservationId = data.id || data.reservationId;
  } catch {
    sleep(1);
    return;
  }

  // 3. Place hold
  const holdPayload = JSON.stringify({ durationMinutes: 15 });
  const holdRes = http.post(`${BASE_URL}/api/v1/reservations/${reservationId}/hold`, holdPayload, {
    headers: {
      'Content-Type': 'application/json',
      'X-Session-Id': sessionId,
      Accept: 'application/json',
    },
  });

  check(holdRes, {
    'hold status is 200': (r) => r.status === 200,
  });

  const releaseRes = http.del(`${BASE_URL}/api/v1/reservations/${reservationId}/hold`, null, {
    headers: {
      'X-Session-Id': sessionId,
      Accept: 'application/json',
    },
  });

  check(releaseRes, {
    'release status is 200 or 204': (r) => r.status === 200 || r.status === 204,
  });

  sleep(SMOKE_MODE ? 40 + Math.random() * 5 : Math.random() * 3 + 2);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/concurrent-booking.json': JSON.stringify(data, null, 2),
  };
}
