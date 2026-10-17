import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./client", () => ({
  get: vi.fn(),
  patch: vi.fn(),
  post: vi.fn(),
}));

import { get, patch, post } from "./client";
import type { PublicReservationCreated } from "./types";
import {
  createReservation,
  createUnpaidReservationRequest,
  extendHold,
  getReservationByPublicCode,
  placeHold,
} from "./reservations";

const mockedGet = vi.mocked(get);
const mockedPatch = vi.mocked(patch);
const mockedPost = vi.mocked(post);

describe("reservations API", () => {
  beforeEach(() => {
    mockedGet.mockReset();
    mockedPatch.mockReset();
    mockedPost.mockReset();
    mockedGet.mockResolvedValue(undefined as never);
    mockedPatch.mockResolvedValue(undefined as never);
    mockedPost.mockResolvedValue(undefined as never);
  });

  it("posts reservation creation payloads", async () => {
    const payload = {
      vehicleGroupId: "vehicle-1",
      pickupOfficeId: "ala",
      returnOfficeId: "gzp",
      pickupDateTimeUtc: "2026-05-10T10:00:00Z",
      returnDateTimeUtc: "2026-05-12T09:00:00Z",
      customer: { firstName: "Jane", lastName: "Doe", email: "jane@example.com", phone: "+905551234567" },
    };

    const created: PublicReservationCreated = { id: "reservation-1", publicCode: "ALN-123", status: "Draft" };
    mockedPost.mockResolvedValue(created);

    const result = await createReservation(payload);

    expect(mockedPost).toHaveBeenCalledWith("/reservations", payload);
    expect(result).toEqual(created);
  });

  it("passes the quote session and idempotency headers when supplied", async () => {
    const payload = {
      vehicleGroupId: "vehicle-1",
      pickupOfficeId: "ala",
      returnOfficeId: "gzp",
      pickupDateTimeUtc: "2026-05-10T10:00:00Z",
      returnDateTimeUtc: "2026-05-12T09:00:00Z",
      customer: { firstName: "Jane", lastName: "Doe", email: "jane@example.com", phone: "+905551234567" },
      quoteId: "quote-1",
      locale: "en",
    };

    await createReservation(payload, { sessionId: "session-1", idempotencyKey: "key-1" });

    expect(mockedPost).toHaveBeenCalledWith("/reservations", payload, {
      headers: { "X-Session-Id": "session-1", "Idempotency-Key": "key-1" },
    });
  });

  it("posts unpaid reservation request payloads", async () => {
    const payload = {
      vehicleGroupId: "vehicle-1",
      pickupOfficeId: "ala",
      returnOfficeId: "gzp",
      pickupDateTimeUtc: "2026-05-10T10:00:00Z",
      returnDateTimeUtc: "2026-05-12T09:00:00Z",
      customer: { firstName: "Jane", lastName: "Doe", email: "jane@example.com", phone: "+905551234567" },
    };

    const created: PublicReservationCreated = { id: "reservation-2", publicCode: "ALN-124", status: "Confirmed" };
    mockedPost.mockResolvedValue(created);

    const result = await createUnpaidReservationRequest(payload);

    expect(mockedPost).toHaveBeenCalledWith("/reservations/unpaid-requests", payload);
    expect(result).toEqual(created);
  });

  it("gets reservations by public code", async () => {
    await getReservationByPublicCode("ALN-123");

    expect(mockedGet).toHaveBeenCalledWith("/reservations/ALN-123");
  });

  it("posts hold requests and patches hold extensions", async () => {
    await placeHold("reservation-1", "test-session-id", { durationMinutes: 30 });
    await extendHold("reservation-1", { additionalMinutes: 15 });

    expect(mockedPost).toHaveBeenCalledWith("/reservations/reservation-1/hold", { durationMinutes: 30 }, { headers: { 'X-Session-Id': 'test-session-id' } });
    expect(mockedPatch).toHaveBeenCalledWith("/reservations/reservation-1/hold/extend", { additionalMinutes: 15 });
  });
});
