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
  adminPostFormData: vi.fn(),
  adminPut: vi.fn(),
}));

import { adminDel, adminGet, adminPatch, adminPost, adminPostFormData, adminPut } from "../client";
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
  confirmUnpaidRequest,
  createManualReservation,
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
import {
  getAuditLogs,
  getFeatureFlags,
  getPublicSiteSettings,
  updateFeatureFlag,
  updatePublicSiteSettings,
} from "./settings";
import {
  createAdminUser,
  deleteAdminUser,
  getAdminUsers,
  getCustomerById,
  getCustomers,
  updateAdminUser,
  updateAdminUserRole,
  updateAdminUserStatus,
} from "./users";
import {
  createOffice,
  createVehicle,
  createVehicleGroup,
  deleteOffice,
  deleteVehicle,
  deleteVehicleGroup,
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
  uploadVehiclePhoto,
} from "./vehicles";

const mockedDel = vi.mocked(adminDel);
const mockedGet = vi.mocked(adminGet);
const mockedPatch = vi.mocked(adminPatch);
const mockedPost = vi.mocked(adminPost);
const mockedPostFormData = vi.mocked(adminPostFormData);
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
    mockedPostFormData.mockResolvedValue({ data: mockVehicles[0] } as never);
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
    const formFile = new File(["image"], "vehicle.jpg", { type: "image/jpeg" });
    await uploadVehiclePhoto("vehicle-1", formFile);
    await deleteVehicle("vehicle-1");

    expect(mockedPost).toHaveBeenCalledWith("/v1/vehicles", { plate: "07ABC123" });
    expect(mockedPut).toHaveBeenCalledWith("/v1/vehicles/vehicle-1", { color: "Black" });
    expect(mockedPatch).toHaveBeenNthCalledWith(1, "/v1/vehicles/vehicle-1/status", {
      status: 3,
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(2, "/v1/vehicles/vehicle-1/transfer", {
      officeId: "office-2",
    });
    expect(mockedPatch).toHaveBeenNthCalledWith(3, "/v1/vehicles/vehicle-1/maintenance", {
      reason: "oil",
    });
    expect(mockedPostFormData).toHaveBeenCalledWith(
      "/v1/vehicles/vehicle-1/photo",
      expect.any(FormData)
    );
    expect(mockedDel).toHaveBeenCalledWith("/v1/vehicles/vehicle-1");
  });

  it("covers vehicle groups and offices clients", async () => {
    await getVehicleGroups();
    await createVehicleGroup({ name: "SUV" } as never);
    await updateVehicleGroup("group-1", { minAge: 25 } as never);
    await deleteVehicleGroup("group-1");
    await getOffices();
    await createOffice({ name: "Center" } as never);
    await updateOffice("office-1", { city: "Alanya" } as never);
    await deleteOffice("office-1");

    expect(mockedGet).toHaveBeenNthCalledWith(1, "/v1/vehicle-groups");
    expect(mockedPost).toHaveBeenNthCalledWith(1, "/v1/vehicle-groups", { name: "SUV" });
    expect(mockedPut).toHaveBeenNthCalledWith(1, "/v1/vehicle-groups/group-1", {
      minAge: 25,
    });
    expect(mockedDel).toHaveBeenNthCalledWith(1, "/v1/vehicle-groups/group-1");
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/offices");
    expect(mockedPost).toHaveBeenNthCalledWith(2, "/v1/offices", { name: "Center" });
    expect(mockedPut).toHaveBeenNthCalledWith(2, "/v1/offices/office-1", { city: "Alanya" });
    expect(mockedDel).toHaveBeenNthCalledWith(2, "/v1/offices/office-1");
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
    await createManualReservation({
      vehicleId: "vehicle-1",
      pickupOfficeId: "ala",
      returnOfficeId: "gzp",
      pickupDateTimeUtc: "2026-06-10T10:00:00Z",
      returnDateTimeUtc: "2026-06-12T10:00:00Z",
      customerFirstName: "Jane",
      customerLastName: "Doe",
      customerPhone: "+905551234567",
    });
    await confirmUnpaidRequest("reservation-3");
    await checkIn("reservation-1", { mileage: 1200 } as never);
    await checkOut("reservation-1", { mileage: 1400 } as never);
    await refundReservation("reservation-1", { amount: 100, reason: "Refund" } as never);

    expect(mockedGet).toHaveBeenNthCalledWith(
      1,
      "/v1/reservations?page=1&status=Confirmed"
    );
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/reservations/reservation-1");
    expect(mockedPost).toHaveBeenNthCalledWith(1, "/v1/reservations/reservation-1/cancel", "Customer request");
    expect(mockedPost).toHaveBeenNthCalledWith(2, "/v1/reservations/reservation-2/cancel", "Duplicate");
    expect(mockedPatch).toHaveBeenNthCalledWith(
      1,
      "/v1/reservations/reservation-1/assign-vehicle",
      { vehicleId: "vehicle-1" }
    );
    expect(mockedPost).toHaveBeenNthCalledWith(3, "/v1/reservations/manual", {
      vehicleId: "vehicle-1",
      pickupOfficeId: "ala",
      returnOfficeId: "gzp",
      pickupDateTimeUtc: "2026-06-10T10:00:00Z",
      returnDateTimeUtc: "2026-06-12T10:00:00Z",
      customerFirstName: "Jane",
      customerLastName: "Doe",
      customerPhone: "+905551234567",
    });
    expect(mockedPost).toHaveBeenNthCalledWith(4, "/v1/reservations/reservation-3/confirm-unpaid-request", {});
    expect(mockedPatch).toHaveBeenNthCalledWith(
      2,
      "/v1/reservations/reservation-1/check-in",
      { mileage: 1200 }
    );
    expect(mockedPatch).toHaveBeenNthCalledWith(
      3,
      "/v1/reservations/reservation-1/check-out",
      { mileage: 1400 }
    );
    expect(mockedPost).toHaveBeenNthCalledWith(5, "/v1/reservations/reservation-1/refund", {
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
    await createCampaign({
      code: "SUMMER",
      name: "Summer Campaign",
      description: "UI-only description",
      discountType: "percentage",
      discountValue: 10,
      minRentalDays: 3,
      validFrom: "2026-06-01",
      validUntil: "2026-12-31",
      isActive: true,
      allowedVehicleGroupIds: ["group-1"],
    });
    await updateCampaign("campaign-1", {
      code: "WINTER",
      discountType: "fixed",
      discountValue: 500,
      minRentalDays: 2,
      validFrom: "2026-12-01",
      validUntil: "2027-01-31",
      isActive: false,
    });
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
    expect(mockedPost).toHaveBeenNthCalledWith(2, "/v1/campaigns", {
      code: "SUMMER",
      discountType: "percentage",
      discountValue: 10,
      minDays: 3,
      validFrom: "2026-06-01",
      validUntil: "2026-12-31",
      isActive: true,
      allowedVehicleGroupIds: ["group-1"],
    });
    expect(mockedPut).toHaveBeenNthCalledWith(2, "/v1/campaigns/campaign-1", {
      code: "WINTER",
      discountType: "fixed",
      discountValue: 500,
      minDays: 2,
      validFrom: "2026-12-01",
      validUntil: "2027-01-31",
      isActive: false,
      allowedVehicleGroupIds: [],
    });
    expect(mockedDel).toHaveBeenNthCalledWith(2, "/v1/campaigns/campaign-1");
  });

  it("covers users endpoints and primitive/object payload branches", async () => {
    await getCustomers({ search: "leyla", empty: "" });
    await getCustomerById("customer-1");
    await getAdminUsers({ role: "Admin", page: 2 });
    await createAdminUser({
      email: "admin@example.test",
      password: "P@ssw0rd!",
      fullName: "Test Admin",
      role: "Admin",
    });
    await updateAdminUser("admin-1", {
      email: "admin-updated@example.test",
      fullName: "Updated Admin",
      role: "SuperAdmin",
    });
    await updateAdminUserRole("admin-1", "SuperAdmin");
    await updateAdminUserRole("admin-2", { role: "Admin" });
    await updateAdminUserStatus("admin-1", true);
    await updateAdminUserStatus("admin-2", { isActive: false });
    await deleteAdminUser("admin-2");

    expect(mockedGet).toHaveBeenNthCalledWith(1, "/v1/users/customers?search=leyla");
    expect(mockedGet).toHaveBeenNthCalledWith(2, "/v1/users/customers/customer-1");
    expect(mockedGet).toHaveBeenNthCalledWith(3, "/v1/users?role=Admin&page=2");
    expect(mockedPost).toHaveBeenNthCalledWith(1, "/v1/users", {
      email: "admin@example.test",
      password: "P@ssw0rd!",
      fullName: "Test Admin",
      role: "Admin",
    });
    expect(mockedPut).toHaveBeenNthCalledWith(1, "/v1/users/admin-1", {
      email: "admin-updated@example.test",
      fullName: "Updated Admin",
      role: "SuperAdmin",
    });
    expect(mockedPut).toHaveBeenNthCalledWith(2, "/v1/users/admin-1/role", {
      role: "SuperAdmin",
    });
    expect(mockedPut).toHaveBeenNthCalledWith(3, "/v1/users/admin-2/role", {
      role: "Admin",
    });
    expect(mockedPost).toHaveBeenNthCalledWith(2, "/v1/users/admin-1/activate");
    expect(mockedPost).toHaveBeenNthCalledWith(3, "/v1/users/admin-2/deactivate");
    expect(mockedDel).toHaveBeenCalledWith("/v1/users/admin-2");
  });

  it("covers settings and report endpoints", async () => {
    const publicSiteSettings = {
      companyName: "Alanya",
      companyAddress: "Alanya",
      companyPhone: "+90",
      companyEmail: "contact@example.test",
      workingHours: "09:00 - 18:00",
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
      updatedAt: "2026-06-06T00:00:00Z",
    };
    mockedGet
      .mockResolvedValueOnce({ data: mockFeatureFlags } as never)
      .mockResolvedValueOnce({ data: { items: mockAuditLogs, page: 1, pageSize: 5 } } as never)
      .mockResolvedValueOnce({ data: publicSiteSettings } as never)
      .mockResolvedValueOnce({ data: mockRevenueReports[0] } as never)
      .mockResolvedValueOnce({ data: mockOccupancyReports[0] } as never)
      .mockResolvedValueOnce({ data: mockPopularVehicles } as never);
    mockedPut.mockResolvedValueOnce({ data: publicSiteSettings } as never);

    await expect(getFeatureFlags()).resolves.toBe(mockFeatureFlags);
    await updateFeatureFlag("EnableCreditCardPayment", false);
    await getAuditLogs({ entityType: "Reservation", page: 1 });
    await expect(getPublicSiteSettings()).resolves.toBe(publicSiteSettings);
    await updatePublicSiteSettings(publicSiteSettings);
    await expect(getRevenueReport("month")).resolves.toBe(mockRevenueReports[0]);
    await expect(getOccupancyReport("week")).resolves.toBe(mockOccupancyReports[0]);
    await expect(getPopularVehicles("year")).resolves.toBe(mockPopularVehicles);

    expect(mockedGet).toHaveBeenNthCalledWith(1, "/v1/feature-flags");
    expect(mockedPatch).toHaveBeenCalledWith("/v1/feature-flags/EnableCreditCardPayment", { enabled: false });
    expect(mockedGet).toHaveBeenNthCalledWith(
      2,
      "/v1/audit-logs?entityType=Reservation&page=1"
    );
    expect(mockedGet).toHaveBeenNthCalledWith(3, "/v1/public-site-settings");
    expect(mockedPut).toHaveBeenCalledWith("/v1/public-site-settings", publicSiteSettings);
    expect(mockedGet).toHaveBeenNthCalledWith(4, "/v1/reports/revenue?period=month");
    expect(mockedGet).toHaveBeenNthCalledWith(5, "/v1/reports/occupancy?period=week");
    expect(mockedGet).toHaveBeenNthCalledWith(
      6,
      "/v1/reports/popular-vehicles?period=year"
    );
  });
});
