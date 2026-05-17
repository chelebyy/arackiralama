import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const OFFICE_ID = __ENV.OFFICE_ID || '11111111-1111-1111-1111-111111111111';
const VEHICLE_GROUP_ID = __ENV.VEHICLE_GROUP_ID || '';
const SMOKE_MODE = __ENV.SMOKE_MODE === '1';
const RESPONSE_TIME_LIMIT_MS = SMOKE_MODE ? 30000 : 500;

export const options = SMOKE_MODE
  ? {
      stages: [
        { duration: '15s', target: 1 },
        { duration: '45s', target: 2 },
        { duration: '15s', target: 2 },
        { duration: '10s', target: 0 },
      ],
      thresholds: {
        http_req_duration: ['p(95)<1000'],
        http_req_failed: ['rate<0.01'],
      },
    }
  : {
      stages: [
        { duration: '1m', target: 25 },
        { duration: '2m', target: 100 },
        { duration: '1m', target: 100 },
        { duration: '1m', target: 0 },
      ],
      thresholds: {
        http_req_duration: ['p(95)<500'],
        http_req_failed: ['rate<0.01'],
      },
    };

function formatDate(d) {
  return d.toISOString().split('T')[0];
}

export default function () {
  const now = new Date();
  const pickup = new Date(now.getTime() + 2 * 24 * 60 * 60 * 1000);
  const returnDate = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);

  const query = [
    `office_id=${encodeURIComponent(OFFICE_ID)}`,
    `pickup_datetime=${encodeURIComponent(`${formatDate(pickup)}T10:00:00Z`)}`,
    `return_datetime=${encodeURIComponent(`${formatDate(returnDate)}T10:00:00Z`)}`,
  ];

  if (VEHICLE_GROUP_ID) {
    query.push(`vehicle_group_id=${encodeURIComponent(VEHICLE_GROUP_ID)}`);
  }

  const url = `${BASE_URL}/api/v1/vehicles/available?${query.join('&')}`;

  const res = http.get(url, {
    headers: { Accept: 'application/json' },
  });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time within limit': (r) => r.timings.duration < RESPONSE_TIME_LIMIT_MS,
    'response wraps data array': (r) => {
      try {
        const body = JSON.parse(r.body);
        return Array.isArray(body.data || body);
      } catch {
        return false;
      }
    },
  });

  sleep(Math.random() * 2 + 0.5);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/concurrent-search.json': JSON.stringify(data, null, 2),
  };
}
