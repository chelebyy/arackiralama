import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./client", () => ({
  get: vi.fn(),
}));

import { get } from "./client";
import { getAvailableVehicles, getOffices, getVehicleById, getVehicleGroups } from "./vehicles";

const mockedGet = vi.mocked(get);

describe("vehicles API", () => {
  beforeEach(() => {
    mockedGet.mockReset();
    mockedGet.mockResolvedValue(undefined as never);
  });

  it("builds the available-vehicles query string without empty filters", async () => {
    await getAvailableVehicles({
      pickupOfficeId: "ala",
      pickupDate: "2026-05-10",
      pickupTime: "10:00",
      returnOfficeId: "gzp",
      returnDate: "2026-05-12",
      returnTime: "09:00",
      campaignCode: "SUMMER15",
    });

    expect(mockedGet).toHaveBeenCalledWith(
      "/vehicles/available?pickupOfficeId=ala&pickupDate=2026-05-10&pickupTime=10%3A00&returnOfficeId=gzp&returnDate=2026-05-12&returnTime=09%3A00&campaignCode=SUMMER15"
    );
  });

  it("requests vehicle groups, details, and offices from the expected endpoints", async () => {
    await getVehicleGroups();
    await getVehicleById("vehicle-1");
    await getOffices();

    expect(mockedGet).toHaveBeenNthCalledWith(1, "/vehicles/groups");
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/vehicles/vehicle-1");
    expect(mockedGet).toHaveBeenNthCalledWith(3, "/offices");
  });
});
