import { execFileSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

const apiBaseUrl = process.env.E2E_API_BASE_URL || "http://localhost:5000";
const password = "CodexReservationBoundary123!";
const passwordHash = "$2a$12$GN6XV3bIxgjSMbyAY1k0cOg.kCxbd3j5zZcf0rHBPmcBgg7qzpmCu";
const locales = ["tr", "en", "ru", "ar", "de"] as const;
const publicReservationKeys = [
  "currency",
  "depositAmount",
  "pickupDateTime",
  "pickupOfficeName",
  "publicCode",
  "returnDateTime",
  "returnOfficeName",
  "status",
  "totalAmount",
  "vehicleGroupName",
] as const;

function runSql(sql: string) {
  return execFileSync(
    "docker",
    ["exec", "rentacar-postgres", "psql", "-U", "postgres", "-d", "rentacar", "-At", "-v", "ON_ERROR_STOP=1", "-c", sql],
    { encoding: "utf8" }
  ).trim();
}

function unwrapData<T>(payload: unknown): T {
  if (payload && typeof payload === "object" && "data" in payload) {
    return (payload as { data: T }).data;
  }

  return payload as T;
}

async function readAccessToken(response: { json(): Promise<unknown> }) {
  const payload = await response.json();
  return unwrapData<{ accessToken: string }>(payload).accessToken;
}

test.describe.configure({ mode: "serial" });
test.use({ trace: "off", screenshot: "off", video: "off" });

test("public reservation response is allowlisted and cancellation stays owner-only", async ({ page, request }) => {
  const runId = randomUUID();
  const ownerEmail = `codex-reservation-owner-${runId.slice(0, 8)}@example.test`;
  const otherEmail = `codex-reservation-other-${runId.slice(0, 8)}@example.test`;
  const ownerName = `Codex Owner ${runId.slice(0, 8)}`;
  const ownerPhone = `+9000${runId.replaceAll("-", "").slice(0, 8)}`;
  const publicCode = `SEC-${runId.replaceAll("-", "").slice(0, 16).toUpperCase()}`;
  const reservationId = randomUUID();
  const ownerId = randomUUID();
  const otherId = randomUUID();

  try {
    runSql(`
      INSERT INTO customers (
        id, full_name, phone, email, license_year, identity_number, nationality,
        created_at, updated_at, failed_login_count, normalized_email, token_version, password_hash
      ) VALUES (
        '${ownerId}', '${ownerName}', '${ownerPhone}', '${ownerEmail}', 0, '', 'TR',
        now(), now(), 0, upper('${ownerEmail}'), 0, '${passwordHash}'
      ), (
        '${otherId}', 'Codex Other', '', '${otherEmail}', 0, '', 'TR',
        now(), now(), 0, upper('${otherEmail}'), 0, '${passwordHash}'
      );
    `);

    const [vehicleId, officeId] = runSql(`
      SELECT vehicle.id::text || '|' || vehicle.office_id::text
      FROM vehicles AS vehicle
      WHERE vehicle.office_id IS NOT NULL
      ORDER BY vehicle.created_at
      LIMIT 1;
    `).split("|");
    expect(vehicleId).toBeTruthy();
    expect(officeId).toBeTruthy();

    runSql(`
      INSERT INTO reservations (
        id, public_code, customer_id, vehicle_id, pickup_office_id, return_office_id,
        pickup_datetime, return_datetime, status, total_amount, notes, created_at, updated_at
      ) VALUES (
        '${reservationId}', '${publicCode}', '${ownerId}', '${vehicleId}', '${officeId}', '${officeId}',
        now() + interval '730 days', now() + interval '733 days', 'Draft', 12345.67,
        'PRIVATE-NOTE-${runId}', now(), now()
      );
    `);

    const ownerLogin = await request.post(`${apiBaseUrl}/api/customer/v1/auth/login`, {
      headers: { "X-Session-Id": `reservation-boundary-owner-login-${runId}` },
      data: { email: ownerEmail, password },
    });
    expect(ownerLogin.ok()).toBe(true);
    const ownerAccessToken = await readAccessToken(ownerLogin);

    const otherLogin = await request.post(`${apiBaseUrl}/api/customer/v1/auth/login`, {
      headers: { "X-Session-Id": `reservation-boundary-other-login-${runId}` },
      data: { email: otherEmail, password },
    });
    expect(otherLogin.ok()).toBe(true);
    const otherAccessToken = await readAccessToken(otherLogin);

    for (const locale of locales) {
      const responsePromise = page.waitForResponse(
        (response) => response.url().endsWith(`/api/v1/reservations/${publicCode}`) && response.request().method() === "GET"
      );
      await page.goto(`/${locale}/booking/confirmation?code=${publicCode}`);
      const response = await responsePromise;
      expect(response.ok()).toBe(true);
      expect(response.headers()["cache-control"]).toContain("no-store");

      const payload = await response.json();
      const publicReservation = unwrapData<Record<string, unknown>>(payload);
      expect(Object.keys(publicReservation).sort()).toEqual([...publicReservationKeys].sort());
      expect(publicReservation.publicCode).toBe(publicCode);

      const serializedPayload = JSON.stringify(payload);
      for (const forbiddenValue of [
        reservationId,
        ownerId,
        vehicleId,
        officeId,
        ownerEmail,
        ownerName,
        ownerPhone,
        `PRIVATE-NOTE-${runId}`,
      ]) {
        expect(serializedPayload).not.toContain(forbiddenValue);
      }

      await expect(page.getByText(publicCode, { exact: true })).toBeVisible();
      await expect(page.getByText(String(publicReservation.vehicleGroupName), { exact: true })).toBeVisible();
    }

    const stateBeforeRejectedWrites = runSql(`
      SELECT status || '|' || xmin::text || '|' || updated_at::text
      FROM reservations
      WHERE id = '${reservationId}';
    `);

    const anonymousCancellation = await page.evaluate(async (id) => {
      const response = await fetch(`/api/v1/reservations/${id}/cancel`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify("anonymous cancellation attempt"),
      });
      return { status: response.status, body: await response.text() };
    }, reservationId);
    expect([404, 405]).toContain(anonymousCancellation.status);
    expect(runSql(`SELECT status || '|' || xmin::text || '|' || updated_at::text FROM reservations WHERE id = '${reservationId}';`))
      .toBe(stateBeforeRejectedWrites);

    const nonOwnerCancellation = await page.evaluate(async ({ baseUrl, id, token }) => {
      const response = await fetch(`${baseUrl}/api/customer/v1/reservations/${id}/cancel`, {
        method: "POST",
        headers: {
          authorization: `Bearer ${token}`,
          "content-type": "application/json",
        },
        body: JSON.stringify("non-owner cancellation attempt"),
      });
      return { status: response.status, body: await response.text() };
    }, { baseUrl: apiBaseUrl, id: reservationId, token: otherAccessToken });
    expect(nonOwnerCancellation.status).toBe(404);
    expect(runSql(`SELECT status || '|' || xmin::text || '|' || updated_at::text FROM reservations WHERE id = '${reservationId}';`))
      .toBe(stateBeforeRejectedWrites);

    const ownerCancellation = await page.evaluate(async ({ baseUrl, id, token }) => {
      const response = await fetch(`${baseUrl}/api/customer/v1/reservations/${id}/cancel`, {
        method: "POST",
        headers: {
          authorization: `Bearer ${token}`,
          "content-type": "application/json",
        },
        body: JSON.stringify("owner cancellation acceptance"),
      });
      return { status: response.status, body: await response.text() };
    }, { baseUrl: apiBaseUrl, id: reservationId, token: ownerAccessToken });
    expect(ownerCancellation.status).toBe(200);
    expect(runSql(`SELECT status FROM reservations WHERE id = '${reservationId}';`)).toBe("Cancelled");
  } finally {
    runSql(`
        DELETE FROM auth_sessions WHERE principal_id IN ('${ownerId}', '${otherId}');
        DELETE FROM background_jobs WHERE payload LIKE '%${ownerEmail}%' OR payload LIKE '%${otherEmail}%' OR payload LIKE '%${reservationId}%';
        DELETE FROM audit_logs WHERE entity_id IN (
          '${ownerId}',
          '${otherId}',
          '${reservationId}',
          upper('${ownerEmail}'),
          upper('${otherEmail}')
        );
        DELETE FROM reservations WHERE id = '${reservationId}';
        DELETE FROM customers WHERE id IN ('${ownerId}', '${otherId}');
      `);
  }
});
