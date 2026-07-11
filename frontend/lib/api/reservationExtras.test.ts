import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./client", () => ({ get: vi.fn(), post: vi.fn() }));

import { get, post } from "./client";
import { createReservationQuote, getPublicReservationExtraOptions } from "./reservationExtras";

const mockedGet = vi.mocked(get);
const mockedPost = vi.mocked(post);

describe("reservation extra API", () => {
  beforeEach(() => {
    mockedGet.mockReset();
    mockedPost.mockReset();
    mockedGet.mockResolvedValue({ items: [] } as never);
    mockedPost.mockResolvedValue({} as never);
  });

  it("fetches the public catalog without caching", async () => {
    await getPublicReservationExtraOptions("group-1", "en");

    expect(mockedGet).toHaveBeenCalledWith(
      "/reservation-extra-options?vehicleGroupId=group-1&locale=en",
      { cache: "no-store" }
    );
  });

  it("posts server-owned quote inputs with the session header", async () => {
    const payload = {
      vehicleGroupId: "group-1",
      pickupOfficeId: "office-1",
      returnOfficeId: "office-2",
      pickupDateTimeUtc: "2026-05-10T10:00:00Z",
      returnDateTimeUtc: "2026-05-12T09:00:00Z",
      fullCoverageWaiver: false,
      locale: "en",
      selectedExtras: [{ optionId: "option-1", optionVersion: 4, quantity: 2 }],
    };

    await createReservationQuote(payload, "session-1");

    expect(mockedPost).toHaveBeenCalledWith("/pricing/quote", payload, {
      cache: "no-store",
      headers: { "X-Session-Id": "session-1" },
    });
  });
});
