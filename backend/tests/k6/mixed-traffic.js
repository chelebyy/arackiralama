import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || 'integration-admin@rentacar.test';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'IntegrationTestPassword123!';
const OFFICE_ID = __ENV.OFFICE_ID || '11111111-1111-1111-1111-111111111111';
const VEHICLE_GROUP_ID = __ENV.VEHICLE_GROUP_ID || '22222222-2222-2222-2222-222222222221';
const SMOKE_MODE = __ENV.SMOKE_MODE === '1';
let smokeBookingUsed = false;

export const options = {
  stages: SMOKE_MODE
    ? [
        { duration: '10s', target: 1 },
        { duration: '20s', target: 2 },
        { duration: '10s', target: 0 },
      ]
    : [
        { duration: '2m', target: 25 },
        { duration: '5m', target: 100 },
        { duration: '2m', target: 100 },
        { duration: '1m', target: 0 },
      ],
  thresholds: {
    http_req_duration: SMOKE_MODE ? ['p(95)<2000'] : ['p(95)<1000'],
    http_req_failed: ['rate<0.01'],
  },
};

function formatDate(d) {
  return d.toISOString().split('T')[0];
}

function unwrapData(response) {
  try {
    const body = JSON.parse(response.body);
    return body.data ?? body;
  } catch {
    return null;
  }
}

export function setup() {
  if (SMOKE_MODE) {
    return { token: '' };
  }

  const loginRes = http.post(`${BASE_URL}/api/admin/v1/auth/login`, JSON.stringify({
    email: ADMIN_EMAIL,
    password: ADMIN_PASSWORD,
  }), {
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
  });

  const token = loginRes.status === 200
    ? (() => {
        const body = unwrapData(loginRes);
        return body?.accessToken || body?.token || '';
      })()
    : '';

  return { token };
}

export default function (data) {
  const now = new Date();
  const pickupDateTimeUtc = new Date(now.getTime() + 2 * 24 * 60 * 60 * 1000).toISOString();
  const returnDateTimeUtc = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000).toISOString();
  const pickupDate = formatDate(new Date(pickupDateTimeUtc));
  const returnDate = formatDate(new Date(returnDateTimeUtc));

  const trafficRoll = Math.random();

  if (trafficRoll < 0.7) {
    // 70% Search traffic
    group('Search', () => {
      const url = `${BASE_URL}/api/v1/vehicles/available?office_id=${OFFICE_ID}&pickup_datetime=${encodeURIComponent(pickupDateTimeUtc)}&return_datetime=${encodeURIComponent(returnDateTimeUtc)}&vehicle_group_id=${VEHICLE_GROUP_ID}`;
      const res = http.get(url, { headers: { Accept: 'application/json' } });
      check(res, {
        'search status 200': (r) => r.status === 200,
      });
    });
  } else if (trafficRoll < 0.9) {
    // 20% Booking traffic
    group('Booking', () => {
      if (SMOKE_MODE && smokeBookingUsed) {
        const fallbackUrl = `${BASE_URL}/api/v1/vehicles/available?office_id=${OFFICE_ID}&pickup_datetime=${encodeURIComponent(pickupDateTimeUtc)}&return_datetime=${encodeURIComponent(returnDateTimeUtc)}&vehicle_group_id=${VEHICLE_GROUP_ID}`;
        const fallbackRes = http.get(fallbackUrl, { headers: { Accept: 'application/json' } });
        check(fallbackRes, {
          'booking fallback search 200': (r) => r.status === 200,
        });
        return;
      }

      const searchUrl = `${BASE_URL}/api/v1/vehicles/available?office_id=${OFFICE_ID}&pickup_datetime=${encodeURIComponent(pickupDateTimeUtc)}&return_datetime=${encodeURIComponent(returnDateTimeUtc)}&vehicle_group_id=${VEHICLE_GROUP_ID}`;
      const searchRes = http.get(searchUrl, { headers: { Accept: 'application/json' } });

      if (searchRes.status !== 200) {
        sleep(1);
        return;
      }

      const body = unwrapData(searchRes);
      const vehicleGroupId = Array.isArray(body) ? body[0]?.id || body[0]?.vehicleGroupId : null;

      const customerEmail = `mixed-${__VU}-${__ITER}@example.com`;
      const payload = JSON.stringify({
        vehicleGroupId: vehicleGroupId || VEHICLE_GROUP_ID,
        pickupOfficeId: OFFICE_ID,
        returnOfficeId: OFFICE_ID,
        pickupDateTimeUtc,
        returnDateTimeUtc,
        customer: {
          firstName: 'Mixed',
          lastName: 'Traffic',
          email: customerEmail,
          phone: '+905551234567',
          dateOfBirth: '1990-01-01T00:00:00Z',
          identityNumber: '12345678901',
          driverLicenseNumber: 'TR-LIC-10001',
        },
        extraDriverCount: 0,
        childSeatCount: 0,
        sessionId: `mixed-${__VU}-${__ITER}`,
      });

      const createRes = http.post(`${BASE_URL}/api/v1/reservations`, payload, {
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      });
      check(createRes, {
        'booking status 201/200': (r) => r.status === 201 || r.status === 200,
      });

      if (SMOKE_MODE && (createRes.status === 201 || createRes.status === 200)) {
        smokeBookingUsed = true;
      }
    });
  } else {
    // 10% Admin traffic
    group('Admin', () => {
      if (!data.token) {
        sleep(1);
        return;
      }
      const headers = { Authorization: `Bearer ${data.token}`, Accept: 'application/json' };
      const res = http.get(`${BASE_URL}/api/admin/v1/reservations?page=1&pageSize=20`, { headers });
      check(res, {
        'admin list status 200': (r) => r.status === 200,
      });
    });
  }

  sleep(Math.random() * 2 + 0.5);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/mixed-traffic.json': JSON.stringify(data, null, 2),
  };
}
