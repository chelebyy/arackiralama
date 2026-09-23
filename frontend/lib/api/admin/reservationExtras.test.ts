import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  createReservationExtraOption,
  deleteReservationExtraOption,
  getReservationExtraOptions,
  restoreReservationExtraOption,
  updateReservationExtraOption,
  updateReservationExtraOptionStatus
} from "./reservationExtras";

const client = vi.hoisted(() => ({
  adminGet: vi.fn(),
  adminPost: vi.fn(),
  adminPut: vi.fn(),
  adminPatch: vi.fn(),
  adminDel: vi.fn()
}));

vi.mock("../client", () => client);

const option = {
  id: "option-1",
  code: "extra-option-1",
  unitPrice: 75,
  pricingMode: "PER_DAY" as const,
  maxQuantity: 3,
  iconKey: "baby" as const,
  sortOrder: 1,
  isActive: false,
  isArchived: false,
  version: 7,
  updatedAt: "2026-07-10T00:00:00Z",
  vehicleGroupIds: ["group-1"],
  translations: []
};

const payload = {
  unitPrice: 75,
  pricingMode: "PER_DAY" as const,
  maxQuantity: 3,
  iconKey: "baby" as const,
  sortOrder: 1,
  vehicleGroupIds: ["group-1"],
  translations: []
};

describe("reservation extra admin API", () => {
  beforeEach(() => {
    Object.values(client).forEach((mock) => mock.mockReset());
  });

  it("builds the filtered list request and unwraps the response", async () => {
    const list = { totalCount: 1, page: 2, pageSize: 10, items: [option] };
    client.adminGet.mockResolvedValue({ data: list });

    await expect(
      getReservationExtraOptions({
        search: "koltuk",
        status: "archived",
        vehicleGroupId: "group-1",
        includeArchived: true,
        page: 2,
        pageSize: 10
      })
    ).resolves.toEqual(list);

    expect(client.adminGet).toHaveBeenCalledWith(
      "/v1/reservation-extra-options?search=koltuk&status=archived&vehicleGroupId=group-1&includeArchived=true&page=2&pageSize=10"
    );
  });

  it("delegates create, update, status, delete, and restore mutations", async () => {
    client.adminPost
      .mockResolvedValueOnce({ data: option })
      .mockResolvedValueOnce({ data: { item: { ...option, isArchived: false, version: 9 } } });
    client.adminPut.mockResolvedValue({ data: { ...option, version: 8 } });
    client.adminPatch.mockResolvedValue({ data: { ...option, isActive: true, version: 8 } });
    client.adminDel.mockResolvedValue({ data: { disposition: "Archived" } });

    await expect(createReservationExtraOption(payload)).resolves.toEqual(option);
    await expect(
      updateReservationExtraOption("option-1", { ...payload, version: 7 })
    ).resolves.toMatchObject({ version: 8 });
    await expect(
      updateReservationExtraOptionStatus("option-1", { version: 7, isActive: true })
    ).resolves.toMatchObject({ isActive: true });
    await expect(deleteReservationExtraOption("option-1", 7)).resolves.toEqual({
      disposition: "Archived"
    });
    await expect(restoreReservationExtraOption("option-1", 8)).resolves.toMatchObject({
      version: 9
    });

    expect(client.adminPost).toHaveBeenNthCalledWith(1, "/v1/reservation-extra-options", payload);
    expect(client.adminPut).toHaveBeenCalledWith("/v1/reservation-extra-options/option-1", {
      ...payload,
      version: 7
    });
    expect(client.adminPatch).toHaveBeenCalledWith(
      "/v1/reservation-extra-options/option-1/status",
      { version: 7, isActive: true }
    );
    expect(client.adminDel).toHaveBeenCalledWith(
      "/v1/reservation-extra-options/option-1?version=7"
    );
    expect(client.adminPost).toHaveBeenNthCalledWith(
      2,
      "/v1/reservation-extra-options/option-1/restore",
      { version: 8 }
    );
  });

  it("preserves client errors for authoritative UI handling", async () => {
    const conflict = new Error("The reservation extra option changed.");
    client.adminPut.mockRejectedValue(conflict);

    await expect(updateReservationExtraOption("option-1", { ...payload, version: 7 })).rejects.toBe(
      conflict
    );
  });
});
