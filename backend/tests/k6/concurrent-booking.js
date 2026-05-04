import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
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
  const searchUrl = `${BASE_URL}/api/v1/vehicles/available?pickupDate=${formatDate(pickup)}&returnDate=${formatDate(returnDate)}&pickupOffice=ala&returnOffice=ayt`;
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
    if (Array.isArray(body) && body.length > 0) {
      vehicleGroupId = body[0].id || body[0].vehicleGroupId;
    }
  } catch {
    vehicleGroupId = '00000000-0000-0000-0000-000000000001';
  }

  // 2. Create reservation
  const customerEmail = `loadtest-${__VU}-${__ITER}@example.com`;
  const reservationPayload = JSON.stringify({
    vehicleGroupId: vehicleGroupId || '00000000-0000-0000-0000-000000000001',
    pickupOfficeId: 'ala',
    returnOfficeId: 'ayt',
    pickupDateTimeUtc: `${formatDate(pickup)}T10:00:00Z`,
    returnDateTimeUtc: `${formatDate(returnDate)}T10:00:00Z`,
    customer: {
      firstName: 'Load',
      lastName: 'Test',
      email: customerEmail,
      phone: '+905551234567',
      birthDate: '1990-01-01',
      nationality: 'TR',
    },
    extraDriverCount: 0,
    childSeatCount: 0,
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
    reservationId = body.id || body.reservationId;
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

  sleep(Math.random() * 3 + 2);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/concurrent-booking.json': JSON.stringify(data, null, 2),
  };
}
