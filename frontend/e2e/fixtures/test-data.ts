/**
 * Playwright E2E Test Fixtures
 *
 * Provides test data and utilities for E2E tests.
 * Admin auth credentials from integration test seeds:
 *   integration-admin@rentacar.test / IntegrationTestPassword123!
 *
 * IMPORTANT: These tests require:
 *   1. Backend running (docker compose up)
 *   2. Frontend dev server running (pnpm dev)
 *   3. Database seeded with test data
 */

import { test as base, Page } from "@playwright/test";
export { AdminLoginPage } from "../pages/AdminLoginPage";
export { AdminReservationsPage } from "../pages/AdminReservationsPage";
export { AdminReservationDetailPage } from "../pages/AdminReservationDetailPage";

export interface AdminUser {
  email: string;
  password: string;
  name: string;
}

export interface TestReservation {
  publicCode?: string;
  customerEmail?: string;
  pickupOffice?: string;
  returnOffice?: string;
  vehicleGroup?: string;
  pickupDate: string;
  returnDate: string;
}

/** Default admin user from integration test seeds */
export const ADMIN_USER: AdminUser = {
  email: "integration-admin@rentacar.test",
  password: "IntegrationTestPassword123!",
  name: "Integration Admin",
};

/** Pickup/return dates for booking (7 days from now) */
export function getTestDates() {
  const pickup = new Date();
  pickup.setDate(pickup.getDate() + 7);
  const returnDate = new Date(pickup);
  returnDate.setDate(returnDate.getDate() + 3);

  return {
    pickup: pickup.toISOString().split("T")[0],
    returnDate: returnDate.toISOString().split("T")[0],
  };
}

/** Expected offices from seed data */
export const OFFICES = {
  alanya: { id: expect.any(String), name: "Alanya Merkez" },
  gazipasa: { id: expect.any(String), name: "Gazipaşa Havalimanı" },
  antalya: { id: expect.any(String), name: "Antalya Havalimanı" },
};

/** Expected vehicle groups from seed data */
export const VEHICLE_GROUPS = {
  economy: { id: expect.any(String), name: "Ekonomik" },
  compact: { id: expect.any(String), name: "Kompet" },
  sedan: { id: expect.any(String), name: "Sedan" },
  suv: { id: expect.any(String), name: "SUV" },
  luxury: { id: expect.any(String), name: "Lüks" },
  van: { id: expect.any(String), name: "Van" },
};

// Extend Playwright test with custom fixtures
export interface TestFixtures {
  adminUser: AdminUser;
  testDates: { pickup: string; returnDate: string };
}

export const test = base.extend<TestFixtures>({
  adminUser: async ({}, use) => {
    await use(ADMIN_USER);
  },

  testDates: async ({}, use) => {
    await use(getTestDates());
  },
});

export { expect } from "@playwright/test";
