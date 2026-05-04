import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || 'admin@rentacar.test';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'password';

export const options = {
  stages: [
    { duration: '2m', target: 25 },
    { duration: '5m', target: 100 },
    { duration: '2m', target: 100 },
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

export function setup() {
  const loginRes = http.post(`${BASE_URL}/api/admin/v1/auth/login`, JSON.stringify({
    email: ADMIN_EMAIL,
    password: ADMIN_PASSWORD,
  }), {
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
  });

  let token = '';
  if (loginRes.status === 200) {
    try {
      token = loginRes.json('token') || loginRes.json('accessToken') || '';
    } catch {
      token = '';
    }
  }

  return { token };
}

export default function (data) {
  const now = new Date();
  const pickup = new Date(now.getTime() + 2 * 24 * 60 * 60 * 1000);
  const returnDate = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);

  const trafficRoll = Math.random();

  if (trafficRoll < 0.7) {
    // 70% Search traffic
    group('Search', () => {
      const url = `${BASE_URL}/api/v1/vehicles/available?pickupDate=${formatDate(pickup)}&returnDate=${formatDate(returnDate)}&pickupOffice=ala&returnOffice=ayt`;
      const res = http.get(url, { headers: { Accept: 'application/json' } });
      check(res, {
        'search status 200': (r) => r.status === 200,
      });
    });
  } else if (trafficRoll < 0.9) {
    // 20% Booking traffic
    group('Booking', () => {
      const searchUrl = `${BASE_URL}/api/v1/vehicles/available?pickupDate=${formatDate(pickup)}&returnDate=${formatDate(returnDate)}&pickupOffice=ala&returnOffice=ayt`;
      const searchRes = http.get(searchUrl, { headers: { Accept: 'application/json' } });

      if (searchRes.status !== 200) {
        sleep(1);
        return;
      }

      let vehicleGroupId;
      try {
        const body = JSON.parse(searchRes.body);
        vehicleGroupId = body[0]?.id || body[0]?.vehicleGroupId;
      } catch {
        vehicleGroupId = null;
      }

      const customerEmail = `mixed-${__VU}-${__ITER}@example.com`;
      const payload = JSON.stringify({
        vehicleGroupId: vehicleGroupId || '00000000-0000-0000-0000-000000000001',
        pickupOfficeId: 'ala',
        returnOfficeId: 'ayt',
        pickupDateTimeUtc: `${formatDate(pickup)}T10:00:00Z`,
        returnDateTimeUtc: `${formatDate(returnDate)}T10:00:00Z`,
        customer: {
          firstName: 'Mixed',
          lastName: 'Traffic',
          email: customerEmail,
          phone: '+905551234567',
          birthDate: '1990-01-01',
          nationality: 'TR',
        },
        extraDriverCount: 0,
        childSeatCount: 0,
      });

      const createRes = http.post(`${BASE_URL}/api/v1/reservations`, payload, {
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      });
      check(createRes, {
        'booking status 201/200': (r) => r.status === 201 || r.status === 200,
      });
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
