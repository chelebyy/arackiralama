import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || 'admin@rentacar.test';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'password';

export const options = {
  stages: [
    { duration: '1m', target: 5 },
    { duration: '2m', target: 20 },
    { duration: '1m', target: 20 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  const loginRes = http.post(`${BASE_URL}/api/admin/v1/auth/login`, JSON.stringify({
    email: ADMIN_EMAIL,
    password: ADMIN_PASSWORD,
  }), {
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
  });

  const success = check(loginRes, {
    'admin login successful': (r) => r.status === 200,
  });

  let token = '';
  if (success) {
    try {
      token = loginRes.json('token') || loginRes.json('accessToken') || '';
    } catch {
      token = '';
    }
  }

  return { token };
}

export default function (data) {
  const headers = {
    Authorization: `Bearer ${data.token}`,
    Accept: 'application/json',
  };

  // 1. List reservations
  const listRes = http.get(`${BASE_URL}/api/admin/v1/reservations?page=1&pageSize=20`, { headers });
  check(listRes, {
    'list status is 200': (r) => r.status === 200,
    'list response time < 500ms': (r) => r.timings.duration < 500,
  });

  let reservationId;
  try {
    const body = JSON.parse(listRes.body);
    const items = body.items || body.data || [];
    if (items.length > 0) {
      reservationId = items[0].id;
    }
  } catch {
    reservationId = null;
  }

  // 2. Get detail if available
  if (reservationId) {
    const detailRes = http.get(`${BASE_URL}/api/admin/v1/reservations/${reservationId}`, { headers });
    check(detailRes, {
      'detail status is 200': (r) => r.status === 200,
      'detail response time < 500ms': (r) => r.timings.duration < 500,
    });
  }

  sleep(Math.random() * 2 + 1);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/admin-dashboard.json': JSON.stringify(data, null, 2),
  };
}
