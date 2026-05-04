import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  stages: [
    { duration: '1m', target: 5 },
    { duration: '2m', target: 20 },
    { duration: '1m', target: 20 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<1500'],
    http_req_failed: ['rate<0.01'],
  },
};

function randomUUID() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export default function () {
  // 50% of requests reuse the same idempotency key to test idempotency
  const reuseKey = Math.random() < 0.5;
  const idempotencyKey = reuseKey ? 'static-test-key-001' : randomUUID();

  const payload = JSON.stringify({
    reservationId: '00000000-0000-0000-0000-000000000001',
    idempotencyKey,
    card: {
      holderName: 'Test User',
      number: '4111111111111111',
      expiryMonth: '12',
      expiryYear: '30',
      cvv: '123',
    },
  });

  const res = http.post(`${BASE_URL}/api/v1/payments/intents`, payload, {
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
  });

  check(res, {
    'status is 200 or 201 or 400': (r) =>
      r.status === 200 || r.status === 201 || r.status === 400,
    'response time < 1500ms': (r) => r.timings.duration < 1500,
    'idempotency preserved': (r) => {
      // If reusing key, should get same response (not duplicate error)
      if (reuseKey && r.status === 400) {
        try {
          const body = JSON.parse(r.body);
          return !body.message?.toLowerCase().includes('duplicate');
        } catch {
          return true;
        }
      }
      return true;
    },
  });

  sleep(Math.random() * 2 + 1);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/payment-intent.json': JSON.stringify(data, null, 2),
  };
}
