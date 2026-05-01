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
      office_id: "ala",
      pickup_datetime: "2026-05-10T10:00:00",
      return_datetime: "2026-05-12T09:00:00",
    });

    expect(mockedGet).toHaveBeenCalledWith(
      "/vehicles/available?office_id=ala&pickup_datetime=2026-05-10T10%3A00%3A00&return_datetime=2026-05-12T09%3A00%3A00"
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
