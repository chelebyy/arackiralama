import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./client", () => ({
  get: vi.fn(),
  patch: vi.fn(),
  post: vi.fn(),
}));

import { get, patch, post } from "./client";
import {
  createReservation,
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

    await createReservation(payload);

    expect(mockedPost).toHaveBeenCalledWith("/reservations", payload);
  });

  it("gets reservations by public code", async () => {
    await getReservationByPublicCode("ALN-123");

    expect(mockedGet).toHaveBeenCalledWith("/reservations/ALN-123");
  });

  it("posts hold requests and patches hold extensions", async () => {
    await placeHold("reservation-1", { durationMinutes: 30 });
    await extendHold("reservation-1", { additionalMinutes: 15 });

    expect(mockedPost).toHaveBeenCalledWith("/reservations/reservation-1/hold", { durationMinutes: 30 });
    expect(mockedPatch).toHaveBeenCalledWith("/reservations/reservation-1/hold/extend", { additionalMinutes: 15 });
  });
});
