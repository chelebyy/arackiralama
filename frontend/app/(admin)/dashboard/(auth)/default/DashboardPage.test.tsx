import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import DashboardPage from "./page";

const useAdminReservationsMock = vi.fn();
const useAdminVehiclesMock = vi.fn();

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/hooks/admin", () => ({
  useAdminReservations: (...args: unknown[]) => useAdminReservationsMock(...args),
  useAdminVehicles: (...args: unknown[]) => useAdminVehiclesMock(...args),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: any) => <div data-testid="chart-container">{children}</div>,
  BarChart: ({ children }: any) => <div data-testid="bar-chart">{children}</div>,
  Bar: () => <div data-testid="bar-series" />,
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  CartesianGrid: () => null,
}));

describe("DashboardPage", () => {
  beforeEach(() => {
    useAdminReservationsMock.mockReset();
    useAdminVehiclesMock.mockReset();
  });

  it("renders loading placeholders while reservation and vehicle stats are loading", () => {
    useAdminReservationsMock.mockReturnValue({
      reservations: [],
      pagination: null,
      isLoading: true,
      isError: false,
      mutate: vi.fn(),
    });

    useAdminVehiclesMock.mockReturnValue({
      vehicles: [],
      pagination: null,
      isLoading: true,
      isError: false,
      mutate: vi.fn(),
    });

    const { container } = render(<DashboardPage />);

    expect(screen.getByText("Dashboard")).toBeInTheDocument();
    expect(screen.getByText("Yükleniyor...")).toBeInTheDocument();
    expect(container.querySelectorAll(".animate-pulse").length).toBeGreaterThan(0);
  });

  it("renders dashboard stats, quick actions, and recent reservations from admin hooks", () => {
    useAdminReservationsMock.mockImplementation((params?: Record<string, unknown>) => {
      if (params?.pageSize === 5) {
        return {
          reservations: [
            {
              id: "rsv-1",
              reservationCode: "RSV-1001",
              customerName: "Ada Lovelace",
              vehicleName: "Renault Clio",
              status: "ACTIVE",
              totalPrice: 3200,
            },
          ],
          pagination: { page: 1, pageSize: 5, totalCount: 7, totalPages: 2 },
          isLoading: false,
          isError: false,
          mutate: vi.fn(),
        };
      }

      if (params?.status === "ACTIVE") {
        return {
          reservations: [],
          pagination: { page: 1, pageSize: 1, totalCount: 3, totalPages: 3 },
          isLoading: false,
          isError: false,
          mutate: vi.fn(),
        };
      }

      return {
        reservations: [],
        pagination: { page: 1, pageSize: 1, totalCount: 7, totalPages: 7 },
        isLoading: false,
        isError: false,
        mutate: vi.fn(),
      };
    });

    useAdminVehiclesMock.mockImplementation((params?: Record<string, unknown>) => {
      if (params?.status === "Available") {
        return {
          vehicles: [],
          pagination: { page: 1, pageSize: 1, totalCount: 11, totalPages: 11 },
          isLoading: false,
          isError: false,
          mutate: vi.fn(),
        };
      }

      if (params?.status === "Maintenance") {
        return {
          vehicles: [],
          pagination: { page: 1, pageSize: 1, totalCount: 2, totalPages: 2 },
          isLoading: false,
          isError: false,
          mutate: vi.fn(),
        };
      }

      if (params?.status === "Retired") {
        return {
          vehicles: [],
          pagination: { page: 1, pageSize: 1, totalCount: 1, totalPages: 1 },
          isLoading: false,
          isError: false,
          mutate: vi.fn(),
        };
      }

      return {
        vehicles: [],
        pagination: { page: 1, pageSize: 20, totalCount: 14, totalPages: 1 },
        isLoading: false,
        isError: false,
        mutate: vi.fn(),
      };
    });

    render(<DashboardPage />);

    expect(screen.getByText("Toplam Rezervasyon")).toBeInTheDocument();
    expect(screen.getByText("Aktif Rezervasyon")).toBeInTheDocument();
    expect(screen.getByText("Müsait Araç")).toBeInTheDocument();
    expect(screen.getByText("Toplam Araç")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("11")).toBeInTheDocument();
    expect(screen.getByText("14")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /yeni rezervasyon/i })).toHaveAttribute("href", "/dashboard/reservations");
    expect(screen.getByRole("link", { name: /araç ekle/i })).toHaveAttribute("href", "/dashboard/fleet/vehicles");
    expect(screen.getByRole("link", { name: /kampanya oluştur/i })).toHaveAttribute("href", "/dashboard/pricing/campaigns");
    expect(screen.getByText("RSV-1001")).toBeInTheDocument();
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(screen.getByText("Renault Clio")).toBeInTheDocument();
    expect(screen.getByText("Aktif")).toBeInTheDocument();
    expect(screen.getByText("₺3.200")).toBeInTheDocument();
    expect(screen.getByTestId("chart-container")).toBeInTheDocument();
    expect(screen.getByText("Müsait (11)")).toBeInTheDocument();
    expect(screen.getByText("Bakımda (2)")).toBeInTheDocument();
    expect(screen.getByText("Emekli (1)")).toBeInTheDocument();
  });

  it("shows the empty recent reservations state when no reservation exists", () => {
    useAdminReservationsMock.mockImplementation((params?: Record<string, unknown>) => ({
      reservations: [],
      pagination: { page: 1, pageSize: Number(params?.pageSize ?? 1), totalCount: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
      mutate: vi.fn(),
    }));

    useAdminVehiclesMock.mockReturnValue({
      vehicles: [],
      pagination: { page: 1, pageSize: 1, totalCount: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
      mutate: vi.fn(),
    });

    render(<DashboardPage />);

    expect(screen.getByText("Rezervasyon bulunamadı")).toBeInTheDocument();
    expect(screen.getByText("Müsait (0)")).toBeInTheDocument();
  });
});
