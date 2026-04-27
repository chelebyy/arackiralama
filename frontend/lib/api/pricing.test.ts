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
      vehicleId: "vehicle-1",
      pickupOfficeId: "ala",
      pickupDate: "2026-05-10",
      pickupTime: "10:00",
      returnOfficeId: "gzp",
      returnDate: "2026-05-12",
      returnTime: "09:00",
      campaignCode: "SUMMER15",
    });

    expect(mockedGet).toHaveBeenCalledWith(
      "/pricing/breakdown?vehicleId=vehicle-1&pickupOfficeId=ala&pickupDate=2026-05-10&pickupTime=10%3A00&returnOfficeId=gzp&returnDate=2026-05-12&returnTime=09%3A00&campaignCode=SUMMER15"
    );
  });

  it("posts campaign validation requests", async () => {
    await validateCampaign("WELCOME10");

    expect(mockedPost).toHaveBeenCalledWith("/pricing/campaigns/validate", { code: "WELCOME10" });
  });
});
