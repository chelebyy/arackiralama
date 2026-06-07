import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";

import ReservationsPage from "./page";

const useAdminReservationsMock = vi.fn();
const useAdminVehiclesMock = vi.fn();
const useAdminOfficesMock = vi.fn();
const mutateCancelReservationMock = vi.fn();
const mutateCreateManualReservationMock = vi.fn();
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
  useAdminVehicles: (...args: unknown[]) => useAdminVehiclesMock(...args),
  useAdminOffices: (...args: unknown[]) => useAdminOfficesMock(...args),
  mutateCancelReservation: (...args: unknown[]) => mutateCancelReservationMock(...args),
  mutateCreateManualReservation: (...args: unknown[]) =>
    mutateCreateManualReservationMock(...args),
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

vi.mock("@/components/ui/dialog", () => ({
  Dialog: ({ open, children }: any) => (open ? <div role="dialog">{children}</div> : null),
  DialogContent: ({ children }: any) => <div>{children}</div>,
  DialogHeader: ({ children }: any) => <div>{children}</div>,
  DialogTitle: ({ children }: any) => <h2>{children}</h2>,
  DialogFooter: ({ children }: any) => <div>{children}</div>,
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
    useAdminVehiclesMock.mockReset();
    useAdminOfficesMock.mockReset();
    mutateCancelReservationMock.mockReset();
    mutateCreateManualReservationMock.mockReset();
    toastSuccessMock.mockReset();
    toastErrorMock.mockReset();

    useAdminVehiclesMock.mockReturnValue({
      vehicles: [
        {
          id: "vehicle-1",
          plate: "07 ABC 123",
          name: "Renault Clio",
        },
      ],
    });
    useAdminOfficesMock.mockReturnValue({
      offices: [
        { id: "office-1", name: "Alanya Merkez" },
        { id: "office-2", name: "Gazipaşa Havalimanı" },
      ],
    });
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

  it("creates a manual reservation and refreshes the list", async () => {
    const user = userEvent.setup();
    const mutate = vi.fn();
    mutateCreateManualReservationMock.mockResolvedValue(undefined);
    useAdminReservationsMock.mockReturnValue({
      reservations,
      pagination: basePagination,
      isLoading: false,
      isError: false,
      mutate,
    });

    render(<ReservationsPage />);

    await user.click(screen.getByRole("button", { name: "Manuel Rezervasyon" }));
    fireEvent.change(screen.getByLabelText("Fiziksel Araç"), { target: { value: "vehicle-1" } });
    fireEvent.change(screen.getByLabelText("Alış Ofisi"), { target: { value: "office-1" } });
    fireEvent.change(screen.getByLabelText("İade Ofisi"), { target: { value: "office-2" } });
    fireEvent.change(screen.getByLabelText("Alış Tarihi UTC"), { target: { value: "2026-06-10T10:00" } });
    fireEvent.change(screen.getByLabelText("İade Tarihi UTC"), { target: { value: "2026-06-12T10:00" } });
    fireEvent.change(screen.getByLabelText("Müşteri Adı"), { target: { value: "Jane" } });
    fireEvent.change(screen.getByLabelText("Müşteri Soyadı"), { target: { value: "Doe" } });
    fireEvent.change(screen.getByLabelText("Telefon"), { target: { value: "+905551234567" } });
    fireEvent.change(screen.getByLabelText("E-posta (opsiyonel)"), { target: { value: "jane@example.test" } });
    fireEvent.change(screen.getByLabelText("Toplam Tutar (opsiyonel)"), { target: { value: "4500" } });
    fireEvent.change(screen.getByLabelText("Notlar (opsiyonel)"), { target: { value: "Telefonla alındı" } });
    await user.click(screen.getByRole("button", { name: "Oluştur" }));

    await waitFor(() => {
      expect(mutateCreateManualReservationMock).toHaveBeenCalledWith({
        vehicleId: "vehicle-1",
        pickupOfficeId: "office-1",
        returnOfficeId: "office-2",
        pickupDateTimeUtc: "2026-06-10T10:00",
        returnDateTimeUtc: "2026-06-12T10:00",
        customerFirstName: "Jane",
        customerLastName: "Doe",
        customerPhone: "+905551234567",
        customerEmail: "jane@example.test",
        notes: "Telefonla alındı",
        totalAmount: 4500,
      });
    });
    expect(toastSuccessMock).toHaveBeenCalledWith("Manuel rezervasyon oluşturuldu");
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
