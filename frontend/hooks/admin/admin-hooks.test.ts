import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  mutateCreateOffice,
  mutateCreateVehicle,
  mutateCreateVehicleGroup,
  mutateDeleteOffice,
  mutateDeleteVehicle,
  mutateDeleteVehicleGroup,
  mutateScheduleMaintenance,
  mutateTransferVehicle,
  mutateUpdateOffice,
  mutateUpdateVehicle,
  mutateUpdateVehicleGroup,
  mutateUpdateVehicleStatus,
  useAdminOffices,
  useAdminVehicle,
  useAdminVehicleGroups,
  useAdminVehicles,
} from "./useAdminVehicles";
import {
  mutateCreateCampaign,
  mutateCreatePricingRule,
  mutateDeleteCampaign,
  mutateDeletePricingRule,
  mutateUpdateCampaign,
  mutateUpdatePricingRule,
  useCampaigns,
  usePricingRules,
} from "./useAdminPricing";
import {
  mutateUpdateFeatureFlag,
  mutateUpdatePublicSiteSettings,
  useAuditLogs,
  useFeatureFlags,
  usePublicSiteSettings,
} from "./useAdminSettings";
import {
  mutateCreateAdminUser,
  mutateDeleteAdminUser,
  mutateUpdateAdminUser,
  mutateUpdateAdminUserRole,
  mutateUpdateAdminUserStatus,
  useAdminCustomer,
  useAdminCustomers,
  useAdminUsers,
} from "./useAdminUsers";
import {
  useOccupancyReport,
  usePopularVehicles,
  useRevenueReport,
} from "./useAdminReports";
import {
  mutateAssignVehicle,
  mutateCancelReservation,
  mutateCheckIn,
  mutateCheckOut,
  mutateRefundReservation,
  useAdminReservation,
  useAdminReservations,
} from "./useAdminReservations";

const useSWRMock = vi.fn();

const vehicleApi = vi.hoisted(() => ({
  getVehicles: vi.fn(),
  getVehicleById: vi.fn(),
  getVehicleGroups: vi.fn(),
  getOffices: vi.fn(),
  createVehicle: vi.fn(),
  updateVehicle: vi.fn(),
  deleteVehicle: vi.fn(),
  updateVehicleStatus: vi.fn(),
  transferVehicle: vi.fn(),
  scheduleMaintenance: vi.fn(),
  createVehicleGroup: vi.fn(),
  updateVehicleGroup: vi.fn(),
  deleteVehicleGroup: vi.fn(),
  createOffice: vi.fn(),
  updateOffice: vi.fn(),
  deleteOffice: vi.fn(),
}));

const pricingApi = vi.hoisted(() => ({
  getPricingRules: vi.fn(),
  getCampaigns: vi.fn(),
  createPricingRule: vi.fn(),
  updatePricingRule: vi.fn(),
  deletePricingRule: vi.fn(),
  createCampaign: vi.fn(),
  updateCampaign: vi.fn(),
  deleteCampaign: vi.fn(),
}));

const settingsApi = vi.hoisted(() => ({
  getFeatureFlags: vi.fn(),
  updateFeatureFlag: vi.fn(),
  getAuditLogs: vi.fn(),
  getPublicSiteSettings: vi.fn(),
  updatePublicSiteSettings: vi.fn(),
}));

const usersApi = vi.hoisted(() => ({
  getCustomers: vi.fn(),
  getCustomerById: vi.fn(),
  getAdminUsers: vi.fn(),
  createAdminUser: vi.fn(),
  deleteAdminUser: vi.fn(),
  updateAdminUser: vi.fn(),
  updateAdminUserRole: vi.fn(),
  updateAdminUserStatus: vi.fn(),
}));

const reportsApi = vi.hoisted(() => ({
  getRevenueReport: vi.fn(),
  getOccupancyReport: vi.fn(),
  getPopularVehicles: vi.fn(),
}));

const reservationsApi = vi.hoisted(() => ({
  getReservations: vi.fn(),
  getReservationById: vi.fn(),
  cancelReservation: vi.fn(),
  assignVehicle: vi.fn(),
  checkIn: vi.fn(),
  checkOut: vi.fn(),
  refundReservation: vi.fn(),
}));

vi.mock("swr", () => ({
  default: (...args: unknown[]) => useSWRMock(...args),
}));

vi.mock("@/lib/api/admin/vehicles", () => vehicleApi);
vi.mock("@/lib/api/admin/pricing", () => pricingApi);
vi.mock("@/lib/api/admin/settings", () => settingsApi);
vi.mock("@/lib/api/admin/users", () => usersApi);
vi.mock("@/lib/api/admin/reports", () => reportsApi);
vi.mock("@/lib/api/admin/reservations", () => reservationsApi);

const paginated = (items: unknown[]) => ({
  items,
  page: 2,
  pageSize: 25,
  totalCount: 51,
  totalPages: 3,
});

describe("admin hooks", () => {
  beforeEach(() => {
    useSWRMock.mockReset();
    Object.values(vehicleApi).forEach((mock) => mock.mockReset());
    Object.values(pricingApi).forEach((mock) => mock.mockReset());
    Object.values(settingsApi).forEach((mock) => mock.mockReset());
    Object.values(usersApi).forEach((mock) => mock.mockReset());
    Object.values(reportsApi).forEach((mock) => mock.mockReset());
    Object.values(reservationsApi).forEach((mock) => mock.mockReset());
  });

  it("maps vehicle hook responses and fetchers", async () => {
    const mutate = vi.fn();
    const error = new Error("vehicle error");
    useSWRMock
      .mockReturnValueOnce({
        data: paginated([{ id: "vehicle-1" }]),
        error,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: { id: "vehicle-1" },
        error: undefined,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: paginated([{ id: "group-1" }]),
        error: undefined,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: paginated([{ id: "office-1" }]),
        error: undefined,
        isLoading: true,
        mutate,
      });

    expect(useAdminVehicles({ page: 2 })).toEqual({
      vehicles: [{ id: "vehicle-1" }],
      pagination: { page: 2, pageSize: 25, totalCount: 51, totalPages: 3 },
      isLoading: false,
      isError: error,
      mutate,
    });
    await useSWRMock.mock.calls[0][1]();
    expect(vehicleApi.getVehicles).toHaveBeenCalledWith({ page: 2 });

    expect(useAdminVehicle("vehicle-1").vehicle).toEqual({ id: "vehicle-1" });
    await useSWRMock.mock.calls[1][1]();
    expect(vehicleApi.getVehicleById).toHaveBeenCalledWith("vehicle-1");

    expect(useAdminVehicleGroups().groups).toEqual([{ id: "group-1" }]);
    expect(useAdminOffices()).toMatchObject({
      offices: [{ id: "office-1" }],
      isLoading: true,
    });
  });

  it("uses null detail keys and empty collection fallbacks", () => {
    const mutate = vi.fn();
    useSWRMock.mockReturnValue({
      data: undefined,
      error: undefined,
      isLoading: false,
      mutate,
    });

    expect(useAdminVehicle(null)).toEqual({
      vehicle: undefined,
      isLoading: false,
      isError: undefined,
      mutate,
    });
    expect(useSWRMock).toHaveBeenCalledWith(null, expect.any(Function), {
      revalidateOnFocus: false,
    });
    expect(useAdminVehicles().vehicles).toEqual([]);
    expect(useAdminVehicleGroups().groups).toEqual([]);
    expect(useAdminOffices().offices).toEqual([]);
  });

  it("delegates vehicle mutations to admin API helpers", async () => {
    vehicleApi.createVehicle.mockResolvedValue({ id: "created" });
    vehicleApi.updateVehicle.mockResolvedValue({ id: "updated" });
    vehicleApi.updateVehicleStatus.mockResolvedValue({ id: "status" });
    vehicleApi.transferVehicle.mockResolvedValue({ id: "transfer" });
    vehicleApi.scheduleMaintenance.mockResolvedValue({ id: "maintenance" });
    vehicleApi.createVehicleGroup.mockResolvedValue({ id: "group" });
    vehicleApi.updateVehicleGroup.mockResolvedValue({ id: "group-updated" });
    vehicleApi.createOffice.mockResolvedValue({ id: "office" });
    vehicleApi.updateOffice.mockResolvedValue({ id: "office-updated" });

    await expect(mutateCreateVehicle({ plate: "07 ABC 123" } as any)).resolves.toEqual({
      id: "created",
    });
    await expect(mutateUpdateVehicle("vehicle-1", { color: "Black" } as any)).resolves.toEqual({
      id: "updated",
    });
    await mutateDeleteVehicle("vehicle-1");
    await expect(mutateUpdateVehicleStatus("vehicle-1", { status: 1 } as any)).resolves.toEqual({
      id: "status",
    });
    await expect(mutateTransferVehicle("vehicle-1", { officeId: "office-2" } as any)).resolves.toEqual({
      id: "transfer",
    });
    await expect(mutateScheduleMaintenance("vehicle-1", { reason: "service" } as any)).resolves.toEqual({
      id: "maintenance",
    });
    await expect(mutateCreateVehicleGroup({ name: "Economy" } as any)).resolves.toEqual({
      id: "group",
    });
    await expect(mutateUpdateVehicleGroup("group-1", { name: "SUV" } as any)).resolves.toEqual({
      id: "group-updated",
    });
    await mutateDeleteVehicleGroup("group-1");
    await expect(mutateCreateOffice({ name: "Airport" } as any)).resolves.toEqual({
      id: "office",
    });
    await expect(mutateUpdateOffice("office-1", { name: "Center" } as any)).resolves.toEqual({
      id: "office-updated",
    });
    await mutateDeleteOffice("office-1");

    expect(vehicleApi.deleteVehicle).toHaveBeenCalledWith("vehicle-1");
    expect(vehicleApi.deleteVehicleGroup).toHaveBeenCalledWith("group-1");
    expect(vehicleApi.deleteOffice).toHaveBeenCalledWith("office-1");
  });

  it("maps pricing hooks and delegates pricing mutations", async () => {
    const mutate = vi.fn();
    useSWRMock
      .mockReturnValueOnce({
        data: paginated([{ id: "rule-1" }]),
        error: undefined,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: paginated([{ id: "campaign-1" }]),
        error: new Error("campaign error"),
        isLoading: true,
        mutate,
      });
    pricingApi.createPricingRule.mockResolvedValue({ id: "rule-created" });
    pricingApi.updatePricingRule.mockResolvedValue({ id: "rule-updated" });
    pricingApi.createCampaign.mockResolvedValue({ id: "campaign-created" });
    pricingApi.updateCampaign.mockResolvedValue({ id: "campaign-updated" });

    expect(usePricingRules({ page: 3 })).toEqual({
      rules: [{ id: "rule-1" }],
      pagination: { page: 2, pageSize: 25, totalCount: 51, totalPages: 3 },
      isLoading: false,
      isError: undefined,
      mutate,
    });
    await useSWRMock.mock.calls[0][1]();
    expect(pricingApi.getPricingRules).toHaveBeenCalledWith({ page: 3 });
    expect(useCampaigns()).toMatchObject({
      campaigns: [{ id: "campaign-1" }],
      isLoading: true,
    });

    await expect(mutateCreatePricingRule({ name: "rule" } as any)).resolves.toEqual({
      id: "rule-created",
    });
    await expect(mutateUpdatePricingRule("rule-1", { name: "updated" } as any)).resolves.toEqual({
      id: "rule-updated",
    });
    await mutateDeletePricingRule("rule-1");
    await expect(mutateCreateCampaign({ code: "SUMMER" } as any)).resolves.toEqual({
      id: "campaign-created",
    });
    await expect(mutateUpdateCampaign("campaign-1", { code: "WINTER" } as any)).resolves.toEqual({
      id: "campaign-updated",
    });
    await mutateDeleteCampaign("campaign-1");

    expect(pricingApi.deletePricingRule).toHaveBeenCalledWith("rule-1");
    expect(pricingApi.deleteCampaign).toHaveBeenCalledWith("campaign-1");
  });

  it("maps settings hooks and update mutations", async () => {
    const mutate = vi.fn();
    const error = new Error("audit error");
    const publicSiteSettings = {
      companyName: "Alanya",
      headerLinks: [],
      heroLinks: [],
      quickLinks: [],
      socialLinks: [],
      footerBottomLinks: [],
      contactPageChannels: [],
      contactPageOffices: [],
      contactPageWorkingHours: [],
      contactPageMapTitle: "Map",
      contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=managed",
      contactPageMapIsVisible: true,
      pages: [],
    };
    useSWRMock
      .mockReturnValueOnce({
        data: [{ id: "flag-1" }],
        error: undefined,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: paginated([{ id: "log-1" }]),
        error,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: publicSiteSettings,
        error: undefined,
        isLoading: false,
        mutate,
      });
    settingsApi.updateFeatureFlag.mockResolvedValue({ id: "flag-1", enabled: true });
    settingsApi.updatePublicSiteSettings.mockResolvedValue(publicSiteSettings);

    expect(useFeatureFlags()).toEqual({
      flags: [{ id: "flag-1" }],
      isLoading: false,
      isError: undefined,
      mutate,
    });
    expect(useAuditLogs({ page: 2 })).toEqual({
      logs: [{ id: "log-1" }],
      pagination: { page: 2, pageSize: 25, totalCount: 51, totalPages: 3 },
      isLoading: false,
      isError: error,
      mutate,
    });
    expect(usePublicSiteSettings()).toEqual({
      settings: publicSiteSettings,
      isLoading: false,
      isError: undefined,
      mutate,
    });
    await useSWRMock.mock.calls[1][1]();
    expect(settingsApi.getAuditLogs).toHaveBeenCalledWith({ page: 2 });
    await expect(mutateUpdateFeatureFlag("flag-1", true)).resolves.toEqual({
      id: "flag-1",
      enabled: true,
    });
    await expect(mutateUpdatePublicSiteSettings(publicSiteSettings as any)).resolves.toBe(publicSiteSettings);
  });

  it("maps user hooks and delegates admin user mutations", async () => {
    const mutate = vi.fn();
    useSWRMock
      .mockReturnValueOnce({
        data: paginated([{ id: "customer-1" }]),
        error: undefined,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: { id: "customer-1" },
        error: undefined,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: paginated([{ id: "admin-1" }]),
        error: undefined,
        isLoading: false,
        mutate,
      });
    usersApi.createAdminUser.mockResolvedValue({ id: "admin-created" });
    usersApi.updateAdminUser.mockResolvedValue({ id: "admin-updated" });
    usersApi.updateAdminUserRole.mockResolvedValue({ id: "admin-role" });
    usersApi.updateAdminUserStatus.mockResolvedValue({ id: "admin-status" });
    usersApi.deleteAdminUser.mockResolvedValue(undefined);

    expect(useAdminCustomers({ q: "ada" }).customers).toEqual([{ id: "customer-1" }]);
    await useSWRMock.mock.calls[0][1]();
    expect(usersApi.getCustomers).toHaveBeenCalledWith({ q: "ada" });
    expect(useAdminCustomer("customer-1").customer).toEqual({ id: "customer-1" });
    await useSWRMock.mock.calls[1][1]();
    expect(usersApi.getCustomerById).toHaveBeenCalledWith("customer-1");
    expect(useAdminUsers().users).toEqual([{ id: "admin-1" }]);

    await expect(mutateCreateAdminUser({ email: "admin@example.com" } as any)).resolves.toEqual({
      id: "admin-created",
    });
    await expect(mutateUpdateAdminUser("admin-1", { email: "admin@example.com" } as any)).resolves.toEqual({
      id: "admin-updated",
    });
    await expect(mutateUpdateAdminUserRole("admin-1", "SuperAdmin" as any)).resolves.toEqual({
      id: "admin-role",
    });
    await expect(mutateUpdateAdminUserStatus("admin-1", false)).resolves.toEqual({
      id: "admin-status",
    });
    await mutateDeleteAdminUser("admin-1");
    expect(usersApi.deleteAdminUser).toHaveBeenCalledWith("admin-1");
  });

  it("maps report hooks and period fetchers", async () => {
    useSWRMock
      .mockReturnValueOnce({
        data: { totalRevenue: 12000 },
        error: undefined,
        isLoading: false,
      })
      .mockReturnValueOnce({
        data: { occupancyRate: 72 },
        error: undefined,
        isLoading: true,
      })
      .mockReturnValueOnce({
        data: [{ vehicleName: "Clio" }],
        error: new Error("popular error"),
        isLoading: false,
      });

    expect(useRevenueReport("monthly")).toEqual({
      report: { totalRevenue: 12000 },
      isLoading: false,
      isError: undefined,
    });
    await useSWRMock.mock.calls[0][1]();
    expect(reportsApi.getRevenueReport).toHaveBeenCalledWith("monthly");

    expect(useOccupancyReport("weekly")).toEqual({
      report: { occupancyRate: 72 },
      isLoading: true,
      isError: undefined,
    });
    await useSWRMock.mock.calls[1][1]();
    expect(reportsApi.getOccupancyReport).toHaveBeenCalledWith("weekly");

    expect(usePopularVehicles("yearly")).toMatchObject({
      vehicles: [{ vehicleName: "Clio" }],
      isLoading: false,
    });
    await useSWRMock.mock.calls[2][1]();
    expect(reportsApi.getPopularVehicles).toHaveBeenCalledWith("yearly");
  });

  it("maps reservation hooks and delegates reservation mutations", async () => {
    const mutate = vi.fn();
    const error = new Error("reservation error");
    useSWRMock
      .mockReturnValueOnce({
        data: paginated([{ id: "reservation-1" }]),
        error,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: { id: "reservation-1" },
        error: undefined,
        isLoading: false,
        mutate,
      })
      .mockReturnValueOnce({
        data: undefined,
        error: undefined,
        isLoading: false,
        mutate,
      });
    reservationsApi.cancelReservation.mockResolvedValue({ id: "cancelled" });
    reservationsApi.assignVehicle.mockResolvedValue({ id: "assigned" });
    reservationsApi.checkIn.mockResolvedValue({ id: "checked-in" });
    reservationsApi.checkOut.mockResolvedValue({ id: "checked-out" });
    reservationsApi.refundReservation.mockResolvedValue({ id: "refunded" });

    expect(useAdminReservations({ status: "PENDING" })).toEqual({
      reservations: [{ id: "reservation-1" }],
      pagination: { page: 2, pageSize: 25, totalCount: 51, totalPages: 3 },
      isLoading: false,
      isError: error,
      mutate,
    });
    await useSWRMock.mock.calls[0][1]();
    expect(reservationsApi.getReservations).toHaveBeenCalledWith({ status: "PENDING" });

    expect(useAdminReservation("reservation-1")).toEqual({
      reservation: { id: "reservation-1" },
      isLoading: false,
      isError: undefined,
      mutate,
    });
    await useSWRMock.mock.calls[1][1]();
    expect(reservationsApi.getReservationById).toHaveBeenCalledWith("reservation-1");

    expect(useAdminReservation(null).reservation).toBeUndefined();
    expect(useSWRMock).toHaveBeenLastCalledWith(null, expect.any(Function), {
      revalidateOnFocus: false,
    });

    await expect(mutateCancelReservation("reservation-1", "duplicate")).resolves.toEqual({
      id: "cancelled",
    });
    await expect(mutateAssignVehicle("reservation-1", "vehicle-1")).resolves.toEqual({
      id: "assigned",
    });
    await expect(mutateCheckIn("reservation-1", { checkedInBy: "Admin" } as any)).resolves.toEqual({
      id: "checked-in",
    });
    await expect(mutateCheckOut("reservation-1", { checkedOutBy: "Admin" } as any)).resolves.toEqual({
      id: "checked-out",
    });
    await expect(
      mutateRefundReservation("reservation-1", { amount: 100, idempotencyKey: "key" }),
    ).resolves.toEqual({
      id: "refunded",
    });
  });
});
