import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
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

  const url = `${BASE_URL}/api/v1/vehicles/available?pickupDate=${formatDate(pickup)}&returnDate=${formatDate(returnDate)}&pickupOffice=ala&returnOffice=ayt`;

  const res = http.get(url, {
    headers: { Accept: 'application/json' },
  });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
    'no timeout error': (r) => r.status !== 0,
  });

  sleep(Math.random() * 2 + 0.5);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/concurrent-search.json': JSON.stringify(data, null, 2),
  };
}
