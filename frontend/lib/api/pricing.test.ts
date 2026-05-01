import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./client", () => ({
  get: vi.fn(),
  post: vi.fn(),
}));

import { get, post } from "./client";
import { getPriceBreakdown, validateCampaign } from "./pricing";

const mockedGet = vi.mocked(get);
const mockedPost = vi.mocked(post);

describe("pricing API", () => {
  beforeEach(() => {
    mockedGet.mockReset();
    mockedPost.mockReset();
    mockedGet.mockResolvedValue(undefined as never);
    mockedPost.mockResolvedValue(undefined as never);
  });

  it("builds a price-breakdown query string from booking params", async () => {
    await getPriceBreakdown({
      vehicle_group_id: "vehicle-1",
      pickup_office_id: "ala",
      pickup_datetime: "2026-05-10T10:00:00",
      return_office_id: "gzp",
      return_datetime: "2026-05-12T09:00:00",
      campaign_code: "SUMMER15",
    });

    expect(mockedGet).toHaveBeenCalledWith(
      "/pricing/breakdown?vehicle_group_id=vehicle-1&pickup_office_id=ala&return_office_id=gzp&pickup_datetime=2026-05-10T10%3A00%3A00&return_datetime=2026-05-12T09%3A00%3A00&campaign_code=SUMMER15"
    );
  });

  it("posts campaign validation requests", async () => {
    await validateCampaign({
      code: "WELCOME10",
      vehicleGroupId: "vehicle-group-1",
      rentalDays: 4,
      pickupDate: "2026-05-10",
    });

    expect(mockedPost).toHaveBeenCalledWith("/pricing/campaigns/validate", {
      code: "WELCOME10",
      vehicleGroupId: "vehicle-group-1",
      rentalDays: 4,
      pickupDate: "2026-05-10",
    });
  });
});
