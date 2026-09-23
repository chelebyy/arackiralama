import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  RESERVATION_EXTRA_KEYS,
  mutateCreateReservationExtraOption,
  mutateDeleteReservationExtraOption,
  mutateRestoreReservationExtraOption,
  mutateUpdateReservationExtraOption,
  mutateUpdateReservationExtraOptionStatus,
  useAdminReservationExtras
} from "./useAdminReservationExtras";

const useSWRMock = vi.fn();
const api = vi.hoisted(() => ({
  getReservationExtraOptions: vi.fn(),
  createReservationExtraOption: vi.fn(),
  updateReservationExtraOption: vi.fn(),
  updateReservationExtraOptionStatus: vi.fn(),
  deleteReservationExtraOption: vi.fn(),
  restoreReservationExtraOption: vi.fn()
}));

vi.mock("swr", () => ({ default: (...args: unknown[]) => useSWRMock(...args) }));
vi.mock("@/lib/api/admin/reservationExtras", () => api);

describe("useAdminReservationExtras", () => {
  beforeEach(() => {
    useSWRMock.mockReset();
    Object.values(api).forEach((mock) => mock.mockReset());
  });

  it("uses a stable primitive filter key and maps pagination", async () => {
    const mutate = vi.fn();
    const params = {
      search: "koltuk",
      status: "active" as const,
      vehicleGroupId: "group-1",
      includeArchived: false,
      page: 2,
      pageSize: 20
    };
    useSWRMock.mockReturnValue({
      data: { totalCount: 41, page: 2, pageSize: 20, items: [{ id: "option-1" }] },
      error: undefined,
      isLoading: false,
      mutate
    });

    expect(useAdminReservationExtras(params)).toEqual({
      options: [{ id: "option-1" }],
      pagination: { page: 2, pageSize: 20, totalCount: 41, totalPages: 3 },
      isLoading: false,
      isError: undefined,
      mutate
    });
    expect(useSWRMock.mock.calls[0][0]).toEqual([
      "admin",
      "reservationExtras",
      "list",
      "koltuk",
      "active",
      "group-1",
      false,
      2,
      20
    ]);

    await useSWRMock.mock.calls[0][1]();
    expect(api.getReservationExtraOptions).toHaveBeenCalledWith(params);
    expect(RESERVATION_EXTRA_KEYS.list(params)).toEqual(useSWRMock.mock.calls[0][0]);
  });

  it("returns safe empty fallbacks while loading", () => {
    const mutate = vi.fn();
    useSWRMock.mockReturnValue({ data: undefined, error: undefined, isLoading: true, mutate });

    expect(useAdminReservationExtras()).toEqual({
      options: [],
      pagination: null,
      isLoading: true,
      isError: undefined,
      mutate
    });
  });

  it("delegates all mutations without optimistic cache writes", async () => {
    const payload = { unitPrice: 0 } as never;
    api.createReservationExtraOption.mockResolvedValue({ id: "created" });
    api.updateReservationExtraOption.mockResolvedValue({ id: "updated" });
    api.updateReservationExtraOptionStatus.mockResolvedValue({ id: "status" });
    api.deleteReservationExtraOption.mockResolvedValue({ disposition: "Deleted" });
    api.restoreReservationExtraOption.mockResolvedValue({ id: "restored" });

    await mutateCreateReservationExtraOption(payload);
    await mutateUpdateReservationExtraOption("option-1", payload);
    await mutateUpdateReservationExtraOptionStatus("option-1", payload);
    await mutateDeleteReservationExtraOption("option-1", 7);
    await mutateRestoreReservationExtraOption("option-1", 8);

    expect(api.createReservationExtraOption).toHaveBeenCalledWith(payload);
    expect(api.updateReservationExtraOption).toHaveBeenCalledWith("option-1", payload);
    expect(api.updateReservationExtraOptionStatus).toHaveBeenCalledWith("option-1", payload);
    expect(api.deleteReservationExtraOption).toHaveBeenCalledWith("option-1", 7);
    expect(api.restoreReservationExtraOption).toHaveBeenCalledWith("option-1", 8);
  });
});
