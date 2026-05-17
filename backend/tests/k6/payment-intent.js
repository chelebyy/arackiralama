import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const HOST_HEADER = __ENV.HOST_HEADER || '';
const SMOKE_MODE = __ENV.SMOKE_MODE === '1';
const OFFICE_ID = __ENV.OFFICE_ID || '11111111-1111-1111-1111-111111111111';
const RETURN_OFFICE_ID = __ENV.RETURN_OFFICE_ID || OFFICE_ID;
const VEHICLE_GROUP_ID = __ENV.VEHICLE_GROUP_ID || '22222222-2222-2222-2222-222222222221';
const RESERVATION_ID = __ENV.RESERVATION_ID || '';
const PICKUP_HOURS = Number(__ENV.PICKUP_HOURS || 48);
const RENTAL_DAYS = Number(__ENV.RENTAL_DAYS || 3);

export const options = {
  stages: SMOKE_MODE
    ? undefined
    : [
        { duration: '1m', target: 5 },
        { duration: '2m', target: 20 },
        { duration: '1m', target: 20 },
        { duration: '1m', target: 0 },
      ],
  scenarios: SMOKE_MODE
    ? {
        default: {
          executor: 'shared-iterations',
          vus: 1,
          iterations: 1,
          maxDuration: '1m',
        },
      }
    : undefined,
  thresholds: {
    http_req_duration: SMOKE_MODE ? ['p(95)<30000'] : ['p(95)<1500'],
    http_req_failed: ['rate<0.01'],
  },
};

function parseApiBody(response) {
  try {
    return JSON.parse(response.body);
  } catch {
    return null;
  }
}

function unwrapData(response) {
  const body = parseApiBody(response);
  if (!body) {
    return null;
  }

  return body.data ?? body;
}

function isoUtc(hoursFromNow) {
  return new Date(Date.now() + hoursFromNow * 60 * 60 * 1000).toISOString();
}

function iterationSuffix() {
  const vu = typeof __VU === 'undefined' ? 0 : __VU;
  const iter = typeof __ITER === 'undefined' ? 0 : __ITER;
  return `${vu}-${iter}`;
}

function requestHeaders(extra = {}) {
  const headers = { Accept: 'application/json', ...extra };
  if (HOST_HEADER) {
    headers.Host = HOST_HEADER;
  }
  return headers;
}

function createReservation() {
  const pickupDateTimeUtc = isoUtc(PICKUP_HOURS);
  const returnDateTimeUtc = isoUtc(PICKUP_HOURS + RENTAL_DAYS * 24);
  const searchRes = http.get(
    `${BASE_URL}/api/v1/vehicles/available?office_id=${OFFICE_ID}&pickup_datetime=${encodeURIComponent(pickupDateTimeUtc)}&return_datetime=${encodeURIComponent(returnDateTimeUtc)}&vehicle_group_id=${VEHICLE_GROUP_ID}`,
    { headers: requestHeaders() },
  );

  check(searchRes, {
    'availability lookup succeeded': (r) => r.status === 200,
  });

  const availability = unwrapData(searchRes);
  const vehicleGroup = Array.isArray(availability) ? availability[0] : null;
  if (!vehicleGroup) {
    return null;
  }

  const payload = JSON.stringify({
    vehicleGroupId: vehicleGroup.id || VEHICLE_GROUP_ID,
    pickupOfficeId: OFFICE_ID,
    returnOfficeId: RETURN_OFFICE_ID,
    pickupDateTimeUtc,
    returnDateTimeUtc,
    customer: {
      firstName: 'Payment',
      lastName: 'Smoke',
      email: `payment-${iterationSuffix()}@example.com`,
      phone: '+905551234567',
      identityNumber: '12345678901',
      driverLicenseNumber: 'TR-LIC-10001',
      dateOfBirth: '1990-01-01T00:00:00Z',
    },
    extraDriverCount: 0,
    childSeatCount: 0,
    driverAge: 35,
    fullCoverageWaiver: false,
    notes: 'k6 payment smoke test',
    sessionId: `payment-${iterationSuffix()}`,
  });

  const createRes = http.post(`${BASE_URL}/api/v1/reservations`, payload, {
    headers: requestHeaders({ 'Content-Type': 'application/json' }),
  });

  check(createRes, {
    'reservation creation succeeded': (r) => r.status === 200 || r.status === 201,
  });

  const reservation = unwrapData(createRes);
  return reservation?.id || reservation?.reservationId || null;
}

function createHeldReservation() {
  const reservationId = createReservation();
  if (!reservationId) {
    return null;
  }

  const holdRes = http.post(
    `${BASE_URL}/api/v1/reservations/${reservationId}/hold`,
    JSON.stringify({ durationMinutes: 15 }),
    {
      headers: requestHeaders({
        'Content-Type': 'application/json',
        'X-Session-Id': `payment-smoke-${Date.now()}`,
      }),
    },
  );

  check(holdRes, {
    'setup hold succeeded': (r) => r.status === 200,
  });

  return holdRes.status === 200 ? reservationId : null;
}

export function setup() {
  if (RESERVATION_ID) {
    return { reservationId: RESERVATION_ID };
  }

  if (SMOKE_MODE) {
    const reservationId = createHeldReservation();
    return reservationId ? { reservationId } : {};
  }

  return {};
}

export default function (data) {
  const reservationId = RESERVATION_ID || data?.reservationId || createHeldReservation();
  if (!reservationId) {
    sleep(1);
    return;
  }

  const payload = JSON.stringify({
    reservationId,
    idempotencyKey: `load-${__VU}-${__ITER}-${Date.now()}`,
    installmentCount: 1,
    card: {
      holderName: 'Test User',
      number: '4111111111111111',
      expiryMonth: '12',
      expiryYear: '30',
      cvv: '123',
    },
  });

  const res = http.post(`${BASE_URL}/api/v1/payments/intents`, payload, {
    headers: requestHeaders({ 'Content-Type': 'application/json' }),
  });

  check(res, {
    'status is 200 or 201': (r) => r.status === 200 || r.status === 201,
    'response time within limit': (r) => r.timings.duration < (SMOKE_MODE ? 30000 : 1500),
  });

  sleep(Math.random() * 2 + 1);
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'results/payment-intent.json': JSON.stringify(data, null, 2),
  };
}
