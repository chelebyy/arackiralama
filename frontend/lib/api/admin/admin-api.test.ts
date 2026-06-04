import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../client", () => ({
  del: vi.fn(),
  get: vi.fn(),
  patch: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  adminDel: vi.fn(),
  adminGet: vi.fn(),
  adminPatch: vi.fn(),
  adminPost: vi.fn(),
  adminPut: vi.fn(),
}));

import { adminDel, adminGet, adminPatch, adminPost, adminPut } from "../client";
import {
  mockAdminUsers,
  mockAuditLogs,
  mockCampaigns,
  mockCustomers,
  mockFeatureFlags,
  mockOccupancyReports,
  mockOffices,
  mockPopularVehicles,
  mockPricingRules,
  mockReportStats,
  mockReservations,
  mockRevenueReports,
  mockVehicleGroups,
  mockVehicles,
} from "./mock";
import {
  assignVehicle,
  cancelReservation,
  checkIn,
  checkOut,
  getReservationById,
  getReservations,
  refundReservation,
} from "./reservations";
import {
  createCampaign,
  createPricingRule,
  deleteCampaign,
  deletePricingRule,
  getCampaigns,
  getPricingRules,
  updateCampaign,
  updatePricingRule,
} from "./pricing";
import { getOccupancyReport, getPopularVehicles, getRevenueReport } from "./reports";
import { getAuditLogs, getFeatureFlags, updateFeatureFlag } from "./settings";
import {
  createAdminUser,
  getAdminUsers,
  getCustomerById,
  getCustomers,
  updateAdminUserRole,
  updateAdminUserStatus,
} from "./users";
import {
  createOffice,
  createVehicle,
  createVehicleGroup,
  deleteVehicle,
  getOffices,
  getVehicleById,
  getVehicleGroups,
  getVehicles,
  scheduleMaintenance,
  transferVehicle,
  updateOffice,
  updateVehicle,
  updateVehicleGroup,
  updateVehicleStatus,
} from "./vehicles";

const mockedDel = vi.mocked(adminDel);
const mockedGet = vi.mocked(adminGet);
const mockedPatch = vi.mocked(adminPatch);
const mockedPost = vi.mocked(adminPost);
const mockedPut = vi.mocked(adminPut);

describe("admin API fixtures", () => {
  it("keeps mock fixtures coherent enough for admin screens", () => {
    expect(mockOffices).toHaveLength(5);
    expect(mockVehicleGroups).toHaveLength(5);
    expect(mockVehicles).toHaveLength(6);
    expect(mockCustomers).toHaveLength(5);
    expect(mockAdminUsers).toHaveLength(5);
    expect(mockReservations).toHaveLength(5);
    expect(mockPricingRules).toHaveLength(5);
    expect(mockCampaigns).toHaveLength(5);
    expect(mockFeatureFlags).toHaveLength(5);
    expect(mockAuditLogs).toHaveLength(5);
    expect(mockReportStats).toHaveLength(5);
    expect(mockRevenueReports).toHaveLength(2);
    expect(mockOccupancyReports).toHaveLength(2);
    expect(mockPopularVehicles).toHaveLength(5);

    expect(mockVehicles[0].groupName).toBe(mockVehicleGroups[0].name);
    expect(mockOffices.some((office) => office.name === mockVehicles[0].officeName)).toBe(true);
    expect(mockReservations[0].customer.email).toBe(mockCustomers[0].email);
  });
});

describe("admin vehicles API", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedGet.mockResolvedValue({ data: { items: [], page: 2, pageSize: 10, totalCount: 0 } } as never);
    mockedPost.mockResolvedValue({ data: mockVehicles[0] } as never);
    mockedPut.mockResolvedValue({ data: mockVehicles[1] } as never);
    mockedPatch.mockResolvedValue({ data: mockVehicles[2] } as never);
    mockedDel.mockResolvedValue(undefined as never);
  });

  it("builds list and detail endpoints for vehicles", async () => {
    await expect(
      getVehicles({ page: 2, search: "clio", status: "", officeId: null, active: true })
    ).resolves.toMatchObject({ page: 2 });
    await getVehicleById("vehicle-1");

    expect(mockedGet).toHaveBeenNthCalledWith(
      1,
      "/v1/vehicles?page=2&search=clio&active=true"
    );
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/vehicles/vehicle-1");
  });

  it("sends vehicle write operations to the expected endpoints", async () => {
    await createVehicle({ plate: "07ABC123" } as never);
    await updateVehicle("vehicle-1", { color: "Black" } as never);
    await updateVehicleStatus("vehicle-1", "Maintenance" as never);
    await transferVehicle("vehicle-1", "office-2");
    await scheduleMaintenance("vehicle-1", { reason: "oil" } as never);
    await deleteVehicle("vehicle-1");

    expect(mockedPost).toHaveBeenCalledWith("/v1/vehicles", { plate: "07ABC123" });
    expect(mockedPut).toHaveBeenCalledWith("/v1/vehicles/vehicle-1", { color: "Black" });
    expect(mockedPatch).toHaveBeenNthCalledWith(1, "/v1/vehicles/vehicle-1/status", {
      status: "Maintenance",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(2, "/v1/vehicles/vehicle-1/transfer", {
      officeId: "office-2",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(3, "/v1/vehicles/vehicle-1/maintenance", {
      reason: "oil",
    });
    expect(mockedDel).toHaveBeenCalledWith("/v1/vehicles/vehicle-1");
  });

  it("covers vehicle groups and offices clients", async () => {
    await getVehicleGroups();
    await createVehicleGroup({ name: "SUV" } as never);
    await updateVehicleGroup("group-1", { minAge: 25 } as never);
    await getOffices();
    await createOffice({ name: "Center" } as never);
    await updateOffice("office-1", { city: "Alanya" } as never);

    expect(mockedGet).toHaveBeenNthCalledWith(1, "/v1/vehicle-groups");
    expect(mockedPost).toHaveBeenNthCalledWith(1, "/v1/vehicle-groups", { name: "SUV" });
    expect(mockedPut).toHaveBeenNthCalledWith(1, "/v1/vehicle-groups/group-1", {
      minAge: 25,
    });
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/offices");
    expect(mockedPost).toHaveBeenNthCalledWith(2, "/v1/offices", { name: "Center" });
    expect(mockedPut).toHaveBeenNthCalledWith(2, "/v1/offices/office-1", { city: "Alanya" });
  });
});

describe("admin reservations API", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedGet.mockResolvedValue({ data: { items: mockReservations, page: 1, pageSize: 5 } } as never);
    mockedPatch.mockResolvedValue({ data: mockReservations[0] } as never);
    mockedPost.mockResolvedValue({ data: { id: "refund-1" } } as never);
  });

  it("builds reservation reads and action payloads", async () => {
    await getReservations({ page: 1, status: "Confirmed", empty: "", ignored: undefined });
    await getReservationById("reservation-1");
    await cancelReservation("reservation-1", "Customer request");
    await cancelReservation("reservation-2", { reason: "Duplicate" });
    await assignVehicle("reservation-1", "vehicle-1");
    await checkIn("reservation-1", { mileage: 1200 } as never);
    await checkOut("reservation-1", { mileage: 1400 } as never);
    await refundReservation("reservation-1", { amount: 100, reason: "Refund" } as never);

    expect(mockedGet).toHaveBeenNthCalledWith(
      1,
      "/v1/reservations?page=1&status=Confirmed"
    );
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/reservations/reservation-1");
    expect(mockedPatch).toHaveBeenNthCalledWith(1, "/v1/reservations/reservation-1/cancel", {
      reason: "Customer request",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(2, "/v1/reservations/reservation-2/cancel", {
      reason: "Duplicate",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(
      3,
      "/v1/reservations/reservation-1/assign-vehicle",
      { vehicleId: "vehicle-1" }
    );
    expect(mockedPatch).toHaveBeenNthCalledWith(
      4,
      "/v1/reservations/reservation-1/check-in",
      { mileage: 1200 }
    );
    expect(mockedPatch).toHaveBeenNthCalledWith(
      5,
      "/v1/reservations/reservation-1/check-out",
      { mileage: 1400 }
    );
    expect(mockedPost).toHaveBeenCalledWith("/v1/reservations/reservation-1/refund", {
      amount: 100,
      reason: "Refund",
    });
  });
});

describe("admin pricing, users, settings, and reports APIs", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedGet.mockResolvedValue({ data: { items: [], page: 1, pageSize: 10 } } as never);
    mockedPost.mockResolvedValue({ data: mockCampaigns[0] } as never);
    mockedPut.mockResolvedValue({ data: mockPricingRules[0] } as never);
    mockedPatch.mockResolvedValue({ data: mockFeatureFlags[0] } as never);
    mockedDel.mockResolvedValue(undefined as never);
  });

  it("covers pricing rules and campaigns endpoints", async () => {
    await getPricingRules({ vehicleGroupId: "group-1", active: true, skip: null });
    await createPricingRule({ vehicleGroupId: "group-1" } as never);
    await updatePricingRule("rule-1", { dailyPrice: 80 } as never);
    await deletePricingRule("rule-1");
    await getCampaigns();
    await createCampaign({ code: "SUMMER" } as never);
    await updateCampaign("campaign-1", { isActive: false } as never);
    await deleteCampaign("campaign-1");

    expect(mockedGet).toHaveBeenNthCalledWith(
      1,
      "/v1/pricing-rules?vehicleGroupId=group-1&active=true"
    );
    expect(mockedPost).toHaveBeenNthCalledWith(1, "/v1/pricing-rules", {
      vehicleGroupId: "group-1",
    });
    expect(mockedPut).toHaveBeenNthCalledWith(1, "/v1/pricing-rules/rule-1", {
      dailyPrice: 80,
    });
    expect(mockedDel).toHaveBeenNthCalledWith(1, "/v1/pricing-rules/rule-1");
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/campaigns");
    expect(mockedPost).toHaveBeenNthCalledWith(2, "/v1/campaigns", { code: "SUMMER" });
    expect(mockedPut).toHaveBeenNthCalledWith(2, "/v1/campaigns/campaign-1", {
      isActive: false,
    });
    expect(mockedDel).toHaveBeenNthCalledWith(2, "/v1/campaigns/campaign-1");
  });

  it("covers users endpoints and primitive/object payload branches", async () => {
    await getCustomers({ search: "leyla", empty: "" });
    await getCustomerById("customer-1");
    await getAdminUsers({ role: "Admin", page: 2 });
    await createAdminUser({ email: "admin@example.test" } as never);
    await updateAdminUserRole("admin-1", "SuperAdmin");
    await updateAdminUserRole("admin-2", { role: "Admin" });
    await updateAdminUserStatus("admin-1", true);
    await updateAdminUserStatus("admin-2", { isActive: false });

    expect(mockedGet).toHaveBeenNthCalledWith(1, "/v1/users/customers?search=leyla");
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/users/customers/customer-1");
    expect(mockedGet).toHaveBeenNthCalledWith(3, "/v1/users/admins?role=Admin&page=2");
    expect(mockedPost).toHaveBeenCalledWith("/v1/users/admins", {
      email: "admin@example.test",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(1, "/v1/users/admins/admin-1/role", {
      role: "SuperAdmin",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(2, "/v1/users/admins/admin-2/role", {
      role: "Admin",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(3, "/v1/users/admins/admin-1/status", {
      isActive: true,
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(4, "/v1/users/admins/admin-2/status", {
      isActive: false,
    });
  });

  it("covers settings and report endpoints", async () => {
    mockedGet
      .mockResolvedValueOnce({ data: mockFeatureFlags } as never)
      .mockResolvedValueOnce({ data: { items: mockAuditLogs, page: 1, pageSize: 5 } } as never)
      .mockResolvedValueOnce({ data: mockRevenueReports[0] } as never)
      .mockResolvedValueOnce({ data: mockOccupancyReports[0] } as never)
      .mockResolvedValueOnce({ data: mockPopularVehicles } as never);

    await expect(getFeatureFlags()).resolves.toBe(mockFeatureFlags);
    await updateFeatureFlag("flag-1", false);
    await getAuditLogs({ entityType: "Reservation", page: 1 });
    await expect(getRevenueReport("month")).resolves.toBe(mockRevenueReports[0]);
    await expect(getOccupancyReport("week")).resolves.toBe(mockOccupancyReports[0]);
    await expect(getPopularVehicles("year")).resolves.toBe(mockPopularVehicles);

    expect(mockedGet).toHaveBeenNthCalledWith(1, "/v1/feature-flags");
    expect(mockedPatch).toHaveBeenCalledWith("/v1/feature-flags/flag-1", { enabled: false });
    expect(mockedGet).toHaveBeenNthCalledWith(
      2,
      "/v1/audit-logs?entityType=Reservation&page=1"
    );
    expect(mockedGet).toHaveBeenNthCalledWith(3, "/v1/reports/revenue?period=month");
    expect(mockedGet).toHaveBeenNthCalledWith(4, "/v1/reports/occupancy?period=week");
    expect(mockedGet).toHaveBeenNthCalledWith(
      5,
      "/v1/reports/popular-vehicles?period=year"
    );
  });
});
