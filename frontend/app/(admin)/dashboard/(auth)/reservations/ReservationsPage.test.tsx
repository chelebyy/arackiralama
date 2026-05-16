import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";

import ReservationsPage from "./page";

const useAdminReservationsMock = vi.fn();
const mutateCancelReservationMock = vi.fn();
const toastSuccessMock = vi.fn();
const toastErrorMock = vi.fn();

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/hooks/admin", () => ({
  useAdminReservations: (...args: unknown[]) => useAdminReservationsMock(...args),
  mutateCancelReservation: (...args: unknown[]) => mutateCancelReservationMock(...args),
}));

vi.mock("sonner", () => ({
  toast: {
    success: (...args: unknown[]) => toastSuccessMock(...args),
    error: (...args: unknown[]) => toastErrorMock(...args),
  },
}));

vi.mock("@/components/ui/select", () => ({
  Select: ({ value, onValueChange, children }: any) => (
    <select
      aria-label="Durum"
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

const basePagination = {
  page: 1,
  pageSize: 10,
  totalCount: 2,
  totalPages: 2,
};

const reservations = [
  {
    id: "reservation-1",
    reservationCode: "RSV-1001",
    customerName: "Ada Lovelace",
    vehicleName: "Renault Clio",
    pickupDate: "2026-06-01",
    returnDate: "2026-06-05",
    status: "PENDING",
    totalPrice: 12500,
  },
  {
    id: "reservation-2",
    reservationCode: "RSV-1002",
    customer: { name: "Grace Hopper" },
    vehicle: { name: "Fiat Egea" },
    pickupDate: "2026-07-10",
    returnDate: "2026-07-12",
    status: "COMPLETED",
    totalPrice: 7400,
  },
];

describe("ReservationsPage", () => {
  beforeEach(() => {
    useAdminReservationsMock.mockReset();
    mutateCancelReservationMock.mockReset();
    toastSuccessMock.mockReset();
    toastErrorMock.mockReset();
  });

  it("renders loading placeholders while reservations are loading", () => {
    useAdminReservationsMock.mockReturnValue({
      reservations: [],
      pagination: null,
      isLoading: true,
      isError: false,
      mutate: vi.fn(),
    });

    const { container } = render(<ReservationsPage />);

    expect(screen.getByText("Rezervasyon Listesi")).toBeInTheDocument();
    expect(container.querySelectorAll(".animate-pulse")).toHaveLength(5);
  });

  it("renders an error state when the reservations hook fails", () => {
    useAdminReservationsMock.mockReturnValue({
      reservations: [],
      pagination: null,
      isLoading: false,
      isError: new Error("Network failure"),
      mutate: vi.fn(),
    });

    render(<ReservationsPage />);

    expect(screen.getByText("Veri yüklenirken hata oluştu")).toBeInTheDocument();
  });

  it("renders reservation rows with fallback customer and vehicle names", () => {
    useAdminReservationsMock.mockReturnValue({
      reservations,
      pagination: basePagination,
      isLoading: false,
      isError: false,
      mutate: vi.fn(),
    });

    render(<ReservationsPage />);

    expect(screen.getByText("RSV-1001")).toBeInTheDocument();
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(screen.getByText("Renault Clio")).toBeInTheDocument();
    expect(screen.getAllByText("Beklemede").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("₺12.500")).toBeInTheDocument();
    expect(screen.getByText("Grace Hopper")).toBeInTheDocument();
    expect(screen.getByText("Fiat Egea")).toBeInTheDocument();
    expect(screen.getAllByText("Tamamlandı").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByRole("link", { name: "" })[0]).toHaveAttribute(
      "href",
      "/dashboard/reservations/reservation-1",
    );
  });

  it("filters the rendered rows by search text without hiding matching vehicle fallback values", async () => {
    const user = userEvent.setup();
    useAdminReservationsMock.mockReturnValue({
      reservations,
      pagination: basePagination,
      isLoading: false,
      isError: false,
      mutate: vi.fn(),
    });

    render(<ReservationsPage />);

    await user.type(screen.getByPlaceholderText("Ara..."), "egea");

    expect(screen.queryByText("Ada Lovelace")).not.toBeInTheDocument();
    expect(screen.getByText("Grace Hopper")).toBeInTheDocument();
    expect(screen.getByText("Fiat Egea")).toBeInTheDocument();
  });

  it("sends status and page params to the reservations hook", async () => {
    const user = userEvent.setup();
    useAdminReservationsMock.mockReturnValue({
      reservations,
      pagination: basePagination,
      isLoading: false,
      isError: false,
      mutate: vi.fn(),
    });

    render(<ReservationsPage />);

    await user.selectOptions(screen.getByLabelText("Durum"), "CONFIRMED");
    await user.click(screen.getAllByRole("button", { name: "" }).at(-1)!);

    expect(useAdminReservationsMock).toHaveBeenCalledWith({
      page: 1,
      pageSize: 10,
      status: "CONFIRMED",
    });
    expect(useAdminReservationsMock).toHaveBeenLastCalledWith({
      page: 2,
      pageSize: 10,
      status: "CONFIRMED",
    });
    expect(screen.getByText("2 / 2")).toBeInTheDocument();
  });

  it("cancels pending reservations and refreshes the list on success", async () => {
    const user = userEvent.setup();
    const mutate = vi.fn();
    mutateCancelReservationMock.mockResolvedValue(undefined);
    useAdminReservationsMock.mockReturnValue({
      reservations,
      pagination: basePagination,
      isLoading: false,
      isError: false,
      mutate,
    });

    render(<ReservationsPage />);

    const row = screen.getByText("RSV-1001").closest("tr");
    expect(row).not.toBeNull();

    await user.click(within(row!).getByRole("button", { name: "" }));

    await waitFor(() => {
      expect(mutateCancelReservationMock).toHaveBeenCalledWith(
        "reservation-1",
        "Admin tarafından iptal",
      );
    });
    expect(toastSuccessMock).toHaveBeenCalledWith("Rezervasyon iptal edildi");
    expect(mutate).toHaveBeenCalled();
  });

  it("shows an error toast when cancellation fails", async () => {
    const user = userEvent.setup();
    mutateCancelReservationMock.mockRejectedValue(new Error("cancel failed"));
    useAdminReservationsMock.mockReturnValue({
      reservations: [reservations[0]],
      pagination: null,
      isLoading: false,
      isError: false,
      mutate: vi.fn(),
    });

    render(<ReservationsPage />);

    const row = screen.getByText("RSV-1001").closest("tr");
    expect(row).not.toBeNull();

    await user.click(within(row!).getByRole("button", { name: "" }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("İptal işlemi başarısız");
    });
  });

  it("shows the empty state when no reservations match", async () => {
    const user = userEvent.setup();
    useAdminReservationsMock.mockReturnValue({
      reservations,
      pagination: basePagination,
      isLoading: false,
      isError: false,
      mutate: vi.fn(),
    });

    render(<ReservationsPage />);

    await user.type(screen.getByPlaceholderText("Ara..."), "missing");

    expect(screen.getByText("Rezervasyon bulunamadı")).toBeInTheDocument();
  });
});
