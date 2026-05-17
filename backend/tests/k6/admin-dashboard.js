import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || 'integration-admin@rentacar.test';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'IntegrationTestPassword123!';
const SMOKE_MODE = __ENV.SMOKE_MODE === '1';
const LIST_RESPONSE_TIME_LIMIT_MS = SMOKE_MODE ? 30000 : 500;
const DETAIL_RESPONSE_TIME_LIMIT_MS = SMOKE_MODE ? 30000 : 500;

export const options = {
  stages: SMOKE_MODE
    ? [
        { duration: '10s', target: 1 },
        { duration: '20s', target: 2 },
        { duration: '10s', target: 0 },
      ]
    : [
        { duration: '1m', target: 5 },
        { duration: '2m', target: 20 },
        { duration: '1m', target: 20 },
        { duration: '1m', target: 0 },
      ],
  thresholds: {
    http_req_duration: SMOKE_MODE ? ['p(95)<1500'] : ['p(95)<500'],
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

  const token = loginRes.status === 200
    ? (() => {
        try {
          const body = JSON.parse(loginRes.body);
          const data = body.data ?? body;
          return data?.accessToken || data?.token || '';
        } catch {
          return '';
        }
      })()
    : '';
  return { token };
}

export default function (data) {
  if (!data.token) {
    sleep(1);
    return;
  }

  const headers = {
    Authorization: `Bearer ${data.token}`,
    Accept: 'application/json',
  };

  // 1. List reservations
  const listRes = http.get(`${BASE_URL}/api/admin/v1/reservations?page=1&pageSize=20`, { headers });
  check(listRes, {
    'list status is 200': (r) => r.status === 200,
    'list response time within limit': (r) => r.timings.duration < LIST_RESPONSE_TIME_LIMIT_MS,
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
      'detail response time within limit': (r) => r.timings.duration < DETAIL_RESPONSE_TIME_LIMIT_MS,
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
