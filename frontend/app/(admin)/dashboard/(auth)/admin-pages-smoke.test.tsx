import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

import VehiclesPage from "./fleet/vehicles/page";
import OfficesPage from "./fleet/offices/page";
import VehicleGroupsPage from "./fleet/groups/page";
import MaintenancePage from "./fleet/maintenance/page";
import PricingRulesPage from "./pricing/rules/page";
import CampaignsPage from "./pricing/campaigns/page";
import CustomersPage from "./users/customers/page";
import AdminUsersPage from "./users/admins/page";
import FeatureFlagsPage from "./settings/feature-flags/page";
import RevenueReportPage from "./reports/revenue/page";
import OccupancyReportPage from "./reports/occupancy/page";
import PopularVehiclesPage from "./reports/popular/page";

const mocks = vi.hoisted(() => ({
  useAdminVehicles: vi.fn(),
  useAdminOffices: vi.fn(),
  useAdminVehicleGroups: vi.fn(),
  usePricingRules: vi.fn(),
  useCampaigns: vi.fn(),
  useAdminCustomers: vi.fn(),
  useAdminUsers: vi.fn(),
  mutateDeleteAdminUser: vi.fn(),
  mutateUpdateAdminUserRole: vi.fn(),
  mutateUpdateAdminUserStatus: vi.fn(),
  useFeatureFlags: vi.fn(),
  mutateUpdateFeatureFlag: vi.fn(),
  useRevenueReport: vi.fn(),
  useOccupancyReport: vi.fn(),
  usePopularVehicles: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
}));

vi.mock("next/dynamic", () => ({
  default: () =>
    function MockDialog(props: any) {
      if (!props.open) return null;

      const label =
        props.vehicle?.name ||
        props.office?.name ||
        props.rule?.id ||
        props.campaign?.name ||
        props.adminUser?.email ||
        "new record";

      return (
        <div role="dialog" aria-label="admin dialog">
          <span>{label}</span>
          <button type="button" onClick={props.onSuccess}>
            dialog success
          </button>
        </div>
      );
    },
}));

vi.mock("recharts", () => ({
  Area: () => null,
  AreaChart: () => <div data-testid="area-chart" />,
  Bar: () => null,
  BarChart: () => <div data-testid="bar-chart" />,
  CartesianGrid: () => null,
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("@/components/ui/select", () => ({
  Select: ({ value, onValueChange, children }: any) => (
    <select
      aria-label="select"
      value={value}
      onChange={(event) => onValueChange(event.target.value)}
    >
      {children}
    </select>
  ),
  SelectContent: ({ children }: any) => <>{children}</>,
  SelectItem: ({ value, children }: any) => <option value={value}>{children}</option>,
  SelectTrigger: ({ children }: any) => <>{children}</>,
  SelectValue: () => null,
}));

vi.mock("@/hooks/admin", () => ({
  useAdminVehicles: (...args: unknown[]) => mocks.useAdminVehicles(...args),
  useAdminOffices: (...args: unknown[]) => mocks.useAdminOffices(...args),
  useAdminVehicleGroups: (...args: unknown[]) => mocks.useAdminVehicleGroups(...args),
  usePricingRules: (...args: unknown[]) => mocks.usePricingRules(...args),
  useCampaigns: (...args: unknown[]) => mocks.useCampaigns(...args),
  useAdminCustomers: (...args: unknown[]) => mocks.useAdminCustomers(...args),
  useAdminUsers: (...args: unknown[]) => mocks.useAdminUsers(...args),
  mutateDeleteAdminUser: (...args: unknown[]) => mocks.mutateDeleteAdminUser(...args),
  mutateUpdateAdminUserRole: (...args: unknown[]) =>
    mocks.mutateUpdateAdminUserRole(...args),
  mutateUpdateAdminUserStatus: (...args: unknown[]) =>
    mocks.mutateUpdateAdminUserStatus(...args),
  useFeatureFlags: (...args: unknown[]) => mocks.useFeatureFlags(...args),
  mutateUpdateFeatureFlag: (...args: unknown[]) => mocks.mutateUpdateFeatureFlag(...args),
  useRevenueReport: (...args: unknown[]) => mocks.useRevenueReport(...args),
  useOccupancyReport: (...args: unknown[]) => mocks.useOccupancyReport(...args),
  usePopularVehicles: (...args: unknown[]) => mocks.usePopularVehicles(...args),
}));

vi.mock("sonner", () => ({
  toast: {
    success: (...args: unknown[]) => mocks.toastSuccess(...args),
    error: (...args: unknown[]) => mocks.toastError(...args),
  },
}));

const mutate = vi.fn();

const offices = [
  {
    id: "office-1",
    code: "AYT",
    name: "Antalya Airport",
    city: "Antalya",
    phone: "+90 242 000 00 00",
    email: "airport@example.test",
    type: "airport",
    isActive: true,
  },
  {
    id: "office-2",
    code: "HTL",
    name: "Alanya Hotel Desk",
    city: "Alanya",
    phone: "+90 242 111 11 11",
    email: "hotel@example.test",
    type: "hotel",
    isActive: false,
  },
];

const groups = [
  {
    id: "group-1",
    name: "Ekonomi",
    description: "Kompakt ve ekonomik araçlar",
    depositAmount: 5000,
    minAge: 23,
    minLicenseYears: 2,
    features: ["Otomatik", "Klima"],
  },
  {
    id: "group-2",
    name: "SUV",
    description: "Geniş aile araçları",
    depositAmount: 9000,
    minAge: 27,
    minLicenseYears: 4,
    features: [],
  },
];

const vehicles = [
  {
    id: "vehicle-1",
    plate: "07ABC001",
    name: "Renault Clio",
    groupName: "Ekonomi",
    officeId: "office-1",
    officeName: "Antalya Airport",
    status: "Available",
    mileage: 15200,
    lastMaintenanceDate: "2026-01-01",
    nextMaintenanceDate: "2020-01-01",
    adminNotes: "Yağ değişimi",
  },
  {
    id: "vehicle-2",
    plate: "07ABC002",
    name: "Toyota Corolla",
    group: { name: "Konfor" },
    officeId: "office-2",
    office: { name: "Alanya Hotel Desk" },
    status: "Maintenance",
    mileage: 8300,
    nextMaintenanceDate: "2099-01-01",
  },
];

function setupAdminDefaults() {
  mutate.mockReset();
  mocks.useAdminVehicles.mockReturnValue({
    vehicles,
    isLoading: false,
    isError: false,
    mutate,
  });
  mocks.useAdminOffices.mockReturnValue({
    offices,
    isLoading: false,
    isError: false,
    mutate,
  });
  mocks.useAdminVehicleGroups.mockReturnValue({
    groups,
    isLoading: false,
    isError: false,
    mutate,
  });
  mocks.usePricingRules.mockReturnValue({
    rules: [
      {
        id: "rule-1",
        vehicleGroupId: "group-1",
        startDate: "2026-06-01",
        endDate: "2026-08-31",
        dailyPrice: 2400,
        multiplier: 1.25,
        priority: 10,
        calculationType: "multiplier",
      },
    ],
    isLoading: false,
    isError: false,
    mutate,
  });
  mocks.useCampaigns.mockReturnValue({
    campaigns: [
      {
        id: "campaign-1",
        code: "SUMMER10",
        name: "Summer Discount",
        discountType: "PERCENTAGE",
        discountValue: 10,
        minRentalDays: 3,
        validFrom: "2026-06-01",
        validUntil: "2026-08-31",
        isActive: true,
      },
    ],
    isLoading: false,
    isError: false,
    mutate,
  });
  mocks.useAdminCustomers.mockReturnValue({
    customers: [
      {
        id: "customer-1",
        name: "Ada Lovelace",
        email: "ada@example.test",
        phone: "+90 555 000 0000",
        nationality: "GB",
        reservationCount: 4,
        totalSpent: 32000,
      },
      {
        id: "customer-2",
        name: "Grace Hopper",
        email: "grace@example.test",
        phone: "+90 555 111 1111",
        reservationCount: 1,
        totalSpent: 7200,
      },
    ],
    isLoading: false,
    isError: false,
  });
  mocks.useAdminUsers.mockReturnValue({
    users: [
      {
        id: "admin-1",
        fullName: "Root Admin",
        email: "root@example.test",
        role: "SuperAdmin",
        lastLoginAt: "2026-05-16T10:00:00Z",
        isActive: true,
      },
      {
        id: "admin-2",
        fullName: "Desk Admin",
        email: "desk@example.test",
        role: "Admin",
        lastLoginAt: null,
        isActive: false,
      },
    ],
    isLoading: false,
    isError: false,
    mutate,
  });
  mocks.useFeatureFlags.mockReturnValue({
    flags: [
      {
        id: "flag-1",
        name: "Online Payment",
        description: "Enable payment capture",
        enabled: false,
      },
    ],
    isLoading: false,
    isError: false,
    mutate,
  });
  mocks.useRevenueReport.mockReturnValue({
    report: {
      totalRevenue: 120000,
      totalReservations: 18,
      averageOrderValue: 6666.4,
      dailyBreakdown: [{ date: "2026-05-01", revenue: 15000 }],
    },
    isLoading: false,
    isError: false,
  });
  mocks.useOccupancyReport.mockReturnValue({
    report: {
      totalVehicles: 42,
      occupiedVehicles: 31,
      occupancyRate: 73.8,
      dailyBreakdown: [{ date: "2026-05-01", occupancyRate: 73.8 }],
    },
    isLoading: false,
    isError: false,
  });
  mocks.usePopularVehicles.mockReturnValue({
    vehicles: [
      { vehicleName: "Renault Clio", rentalCount: 12, revenue: 45000 },
      { vehicleName: "Toyota Corolla", rentalCount: 9, revenue: 39000 },
    ],
    isLoading: false,
    isError: false,
  });
}

describe("admin dashboard page surfaces", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupAdminDefaults();
  });

  it("renders and filters fleet vehicles, then refreshes after dialog success", async () => {
    const user = userEvent.setup();

    render(<VehiclesPage />);

    expect(screen.getByText("Araç Listesi")).toBeInTheDocument();
    expect(screen.getByText("07ABC001")).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText("Plaka veya araç adı..."), "corolla");
    expect(screen.queryByText("07ABC001")).not.toBeInTheDocument();
    expect(screen.getByText("07ABC002")).toBeInTheDocument();

    await user.clear(screen.getByPlaceholderText("Plaka veya araç adı..."));
    await user.selectOptions(screen.getAllByRole("combobox")[0], "Maintenance");
    expect(screen.queryByText("07ABC001")).not.toBeInTheDocument();
    expect(screen.getAllByText("Bakımda").length).toBeGreaterThanOrEqual(1);

    await user.click(screen.getByRole("button", { name: /yeni araç/i }));
    expect(screen.getByRole("dialog", { name: "admin dialog" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "dialog success" }));
    expect(mutate).toHaveBeenCalled();
  });

  it("renders offices, vehicle groups, maintenance rows, and empty states", async () => {
    const user = userEvent.setup();

    const { rerender } = render(<OfficesPage />);
    expect(screen.getByText("Antalya Airport")).toBeInTheDocument();
    expect(screen.getByText("Havalimanı")).toBeInTheDocument();
    expect(screen.getByText("Otel")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /yeni ofis/i }));
    await user.click(screen.getByRole("button", { name: "dialog success" }));
    expect(mutate).toHaveBeenCalled();

    rerender(<VehicleGroupsPage />);
    expect(screen.getByText("Araç gruplarını ve özelliklerini yönetin.")).toBeInTheDocument();
    expect(screen.getByText("Ekonomi")).toBeInTheDocument();
    expect(screen.getByText("Otomatik")).toBeInTheDocument();

    rerender(<MaintenancePage />);
    expect(screen.getByText("Bakım Takvimi")).toBeInTheDocument();
    expect(screen.getByText("Gecikmiş")).toBeInTheDocument();
    expect(screen.getByText("Planlandı")).toBeInTheDocument();
    await user.click(screen.getAllByRole("button", { name: "" })[0]);
    expect(mocks.toastSuccess).toHaveBeenCalledWith("Bakım kaydı tamamlandı (mock)");

    mocks.useAdminVehicles.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: false,
      mutate,
    });
    rerender(<MaintenancePage />);
    expect(screen.getByText("Yaklaşan bakım bulunmamaktadır")).toBeInTheDocument();
  });

  it("renders pricing and campaign lists with edit dialogs", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<PricingRulesPage />);

    expect(screen.getByText("Fiyat Kuralları")).toBeInTheDocument();
    expect(screen.getByText("₺2.400")).toBeInTheDocument();
    await user.click(screen.getAllByRole("button", { name: "" })[0]);
    expect(screen.getByRole("dialog", { name: "admin dialog" })).toHaveTextContent("rule-1");
    await user.click(screen.getByRole("button", { name: "dialog success" }));
    expect(mutate).toHaveBeenCalled();

    rerender(<CampaignsPage />);
    expect(screen.getByText("Kampanyalar")).toBeInTheDocument();
    expect(screen.getByText("SUMMER10")).toBeInTheDocument();
    await user.click(screen.getAllByRole("button", { name: "" })[0]);
    expect(screen.getByRole("dialog", { name: "admin dialog" })).toHaveTextContent(
      "Summer Discount",
    );
  });

  it("filters customers and manages admin user edit, status, and delete actions", async () => {
    const user = userEvent.setup();
    mocks.mutateUpdateAdminUserStatus.mockResolvedValue(undefined);
    mocks.mutateDeleteAdminUser.mockResolvedValue(undefined);

    const { rerender } = render(<CustomersPage />);
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    await user.type(screen.getByPlaceholderText("Ara..."), "grace");
    expect(screen.queryByText("Ada Lovelace")).not.toBeInTheDocument();
    expect(screen.getByText("Grace Hopper")).toBeInTheDocument();

    rerender(<AdminUsersPage />);
    expect(screen.getByText("Root Admin")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /yeni admin/i }));
    expect(screen.getByRole("dialog", { name: "admin dialog" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "dialog success" }));
    expect(mutate).toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "root@example.test düzenle" }));
    expect(screen.getByRole("dialog", { name: "admin dialog" })).toHaveTextContent(
      "root@example.test",
    );

    await user.click(screen.getByRole("button", { name: "root@example.test pasif et" }));
    await waitFor(() =>
      expect(mocks.mutateUpdateAdminUserStatus).toHaveBeenCalledWith("admin-1", false),
    );
    expect(mocks.toastSuccess).toHaveBeenCalledWith("Durum güncellendi");

    await user.click(screen.getByRole("button", { name: "desk@example.test sil" }));
    expect(screen.getByText(/kalıcı olarak silinecek/i)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Sil" }));
    await waitFor(() =>
      expect(mocks.mutateDeleteAdminUser).toHaveBeenCalledWith("admin-2"),
    );
    expect(mocks.toastSuccess).toHaveBeenCalledWith("Admin kullanıcı silindi");
  });

  it("shows admin update errors through toast feedback", async () => {
    const user = userEvent.setup();
    mocks.mutateUpdateAdminUserStatus.mockRejectedValue(new Error("status failed"));
    mocks.mutateUpdateFeatureFlag.mockRejectedValue(new Error("flag failed"));

    const { rerender } = render(<AdminUsersPage />);
    await user.click(screen.getByRole("button", { name: "root@example.test pasif et" }));
    await waitFor(() => expect(mocks.toastError).toHaveBeenCalledWith("Durum güncellenemedi"));

    rerender(<FeatureFlagsPage />);
    await user.click(screen.getByRole("switch"));
    await waitFor(() => expect(mocks.toastError).toHaveBeenCalledWith("Güncelleme başarısız"));
  });

  it("updates feature flags successfully", async () => {
    const user = userEvent.setup();
    mocks.mutateUpdateFeatureFlag.mockResolvedValue(undefined);

    render(<FeatureFlagsPage />);

    expect(screen.getByText("Online Payment")).toBeInTheDocument();
    await user.click(screen.getByRole("switch"));
    await waitFor(() =>
      expect(mocks.mutateUpdateFeatureFlag).toHaveBeenCalledWith("flag-1", true),
    );
    expect(mocks.toastSuccess).toHaveBeenCalledWith("Özellik bayrağı güncellendi");
    expect(mutate).toHaveBeenCalled();
  });

  it("renders report cards, charts, period changes, and report error states", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<RevenueReportPage />);

    expect(screen.getByText("Gelir Raporu")).toBeInTheDocument();
    expect(screen.getByText("₺120.000")).toBeInTheDocument();
    expect(screen.getByTestId("bar-chart")).toBeInTheDocument();

    await user.selectOptions(screen.getByRole("combobox"), "weekly");
    expect(mocks.useRevenueReport).toHaveBeenLastCalledWith("weekly");

    rerender(<OccupancyReportPage />);
    expect(screen.getByText("Doluluk Raporu")).toBeInTheDocument();
    expect(screen.getByText("%73.8")).toBeInTheDocument();
    expect(screen.getByTestId("area-chart")).toBeInTheDocument();

    rerender(<PopularVehiclesPage />);
    expect(screen.getByText("Popüler Araçlar")).toBeInTheDocument();
    expect(screen.getAllByText("Renault Clio").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("₺45.000")).toBeInTheDocument();

    mocks.usePopularVehicles.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: false,
    });
    rerender(<PopularVehiclesPage />);
    expect(screen.getByText("Veri bulunamadı")).toBeInTheDocument();
  });

  it("renders loading and error branches for high-traffic admin pages", () => {
    mocks.useAdminVehicles.mockReturnValue({
      vehicles: [],
      isLoading: true,
      isError: false,
      mutate,
    });
    const { container, rerender } = render(<VehiclesPage />);
    expect(container.querySelectorAll(".animate-pulse")).toHaveLength(5);

    mocks.useAdminVehicles.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: new Error("failed"),
      mutate,
    });
    rerender(<VehiclesPage />);
    expect(screen.getByText("Veri yüklenirken hata oluştu")).toBeInTheDocument();

    mocks.usePricingRules.mockReturnValue({
      rules: [],
      isLoading: false,
      isError: false,
      mutate,
    });
    rerender(<PricingRulesPage />);
    expect(screen.getByText("Fiyat kuralı bulunamadı")).toBeInTheDocument();

    mocks.useCampaigns.mockReturnValue({
      campaigns: [],
      isLoading: false,
      isError: false,
      mutate,
    });
    rerender(<CampaignsPage />);
    expect(screen.getByText("Kampanya bulunamadı")).toBeInTheDocument();
  });
});
