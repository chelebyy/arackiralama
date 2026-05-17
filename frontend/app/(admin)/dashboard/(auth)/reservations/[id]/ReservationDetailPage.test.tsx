import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

import type { AdminReservation } from "@/lib/api/admin/types";
import { PaymentStatus, ReservationStatus } from "@/lib/api/types";
import ReservationDetailPage from "./page";

const useParamsMock = vi.fn();
const useAdminReservationMock = vi.fn();
const mutateCancelReservationMock = vi.fn();
const mutateCheckInMock = vi.fn();
const mutateCheckOutMock = vi.fn();
const mutateRefundReservationMock = vi.fn();
const toastSuccessMock = vi.fn();
const toastErrorMock = vi.fn();
const randomUUIDMock = vi.fn();

vi.mock("next/navigation", () => ({
  useParams: () => useParamsMock(),
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/hooks/admin", () => ({
  useAdminReservation: (...args: unknown[]) => useAdminReservationMock(...args),
  mutateCancelReservation: (...args: unknown[]) => mutateCancelReservationMock(...args),
  mutateCheckIn: (...args: unknown[]) => mutateCheckInMock(...args),
  mutateCheckOut: (...args: unknown[]) => mutateCheckOutMock(...args),
  mutateRefundReservation: (...args: unknown[]) => mutateRefundReservationMock(...args),
}));

vi.mock("sonner", () => ({
  toast: {
    success: (...args: unknown[]) => toastSuccessMock(...args),
    error: (...args: unknown[]) => toastErrorMock(...args),
  },
}));

vi.mock("@/components/ui/dialog", () => ({
  Dialog: ({ open, children }: any) => (open ? <div role="dialog">{children}</div> : null),
  DialogContent: ({ children }: any) => <div>{children}</div>,
  DialogHeader: ({ children }: any) => <div>{children}</div>,
  DialogTitle: ({ children }: any) => <h2>{children}</h2>,
  DialogFooter: ({ children }: any) => <div>{children}</div>,
}));

const baseReservation: AdminReservation = {
  id: "reservation-1",
  publicCode: "PUB-1001",
  reservationCode: "RSV-1001",
  status: ReservationStatus.CONFIRMED,
  vehicleId: "vehicle-1",
  vehicleName: "Renault Clio",
  vehicleImage: "/clio.jpg",
  vehiclePlate: "07 ABC 123",
  assignedVehicleId: "vehicle-assigned-1",
  pickupOfficeId: "office-1",
  pickupOfficeName: "Alanya Merkez",
  pickupDate: "2026-06-01",
  pickupTime: "10:00",
  returnOfficeId: "office-2",
  returnOfficeName: "Gazipaşa Havalimanı",
  returnDate: "2026-06-05",
  returnTime: "18:00",
  customerName: "Ada Lovelace",
  customer: {
    id: "customer-1",
    firstName: "Ada",
    lastName: "Lovelace",
    email: "ada@example.com",
    phone: "+90 555 000 0000",
    nationality: "TR",
    passportNumber: "P123456",
    reservationCount: 3,
    totalSpent: 42000,
    createdAt: "2026-01-01T00:00:00.000Z",
  },
  driver: {
    firstName: "Grace",
    lastName: "Hopper",
    dateOfBirth: "1985-01-01",
    licenseNumber: "D-123",
    licenseCountry: "TR",
    licenseIssueDate: "2020-01-01",
    licenseExpiryDate: "2030-01-01",
    isPrimaryDriver: true,
  },
  extras: [],
  priceBreakdown: {
    basePrice: 10000,
    rentalDays: 4,
    extraFees: [],
    extrasTotal: 500,
    insuranceTotal: 800,
    subtotal: 11300,
    taxRate: 20,
    taxAmount: 2260,
    discountAmount: 1000,
    totalAmount: 12560,
    currency: "TRY",
    depositAmount: 5000,
  },
  campaignCode: "SUMMER10",
  campaignDiscount: 1000,
  createdAt: "2026-05-17T08:00:00.000Z",
  updatedAt: "2026-05-17T09:00:00.000Z",
  paymentStatus: PaymentStatus.AUTHORIZED,
  paymentIntentId: "payment-1",
  totalPrice: 12560,
  notes: "Customer note",
  adminNotes: "VIP müşteri",
  cancellationReason: "Plan değişikliği",
  refundAmount: 3000,
};

function mockReservation(overrides: Partial<AdminReservation> = {}) {
  const mutate = vi.fn();
  useAdminReservationMock.mockReturnValue({
    reservation: { ...baseReservation, ...overrides },
    isLoading: false,
    isError: false,
    mutate,
  });
  return mutate;
}

describe("ReservationDetailPage", () => {
  beforeEach(() => {
    useParamsMock.mockReset();
    useAdminReservationMock.mockReset();
    mutateCancelReservationMock.mockReset();
    mutateCheckInMock.mockReset();
    mutateCheckOutMock.mockReset();
    mutateRefundReservationMock.mockReset();
    toastSuccessMock.mockReset();
    toastErrorMock.mockReset();
    randomUUIDMock.mockReset();
    randomUUIDMock.mockReturnValue("refund-key-1");

    vi.stubGlobal("crypto", {
      ...globalThis.crypto,
      randomUUID: randomUUIDMock,
    });

    useParamsMock.mockReturnValue({ id: "reservation-1" });
  });

  it("renders loading placeholders while the reservation is loading", () => {
    useAdminReservationMock.mockReturnValue({
      reservation: undefined,
      isLoading: true,
      isError: false,
      mutate: vi.fn(),
    });

    const { container } = render(<ReservationDetailPage />);

    expect(useAdminReservationMock).toHaveBeenCalledWith("reservation-1");
    expect(container.querySelectorAll(".animate-pulse")).toHaveLength(5);
  });

  it("renders an error state when the hook fails or no reservation is returned", () => {
    useAdminReservationMock.mockReturnValue({
      reservation: undefined,
      isLoading: false,
      isError: true,
      mutate: vi.fn(),
    });

    render(<ReservationDetailPage />);

    expect(screen.getByRole("link", { name: /geri/i })).toHaveAttribute(
      "href",
      "/dashboard/reservations",
    );
    expect(
      screen.getByText("Rezervasyon bilgileri yüklenirken hata oluştu veya rezervasyon bulunamadı."),
    ).toBeInTheDocument();
  });

  it("renders reservation, customer, driver, vehicle, pricing, notes, and timeline details", () => {
    mockReservation({
      checkedInAt: "2026-06-01T10:05:00.000Z",
      checkedInBy: "Front Desk",
      checkedOutAt: "2026-06-05T18:10:00.000Z",
      checkedOutBy: "Ops",
    });

    render(<ReservationDetailPage />);

    expect(screen.getByRole("heading", { name: "Rezervasyon Detayı" })).toBeInTheDocument();
    expect(screen.getByText("RSV-1001")).toBeInTheDocument();
    expect(screen.getByText("Onaylı")).toBeInTheDocument();
    expect(screen.getByText("Yetkilendirildi")).toBeInTheDocument();
    expect(screen.getByText("SUMMER10")).toBeInTheDocument();
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(screen.getByText("ada@example.com")).toBeInTheDocument();
    expect(screen.getByText("Grace Hopper")).toBeInTheDocument();
    expect(screen.getByText("D-123")).toBeInTheDocument();
    expect(screen.getByText("Renault Clio")).toBeInTheDocument();
    expect(screen.getByText("07 ABC 123")).toBeInTheDocument();
    expect(screen.getByText("vehicle-assigned-1")).toBeInTheDocument();
    expect(screen.getByText("VIP müşteri")).toBeInTheDocument();
    expect(screen.getByText(/Plan değişikliği/)).toBeInTheDocument();
    expect(screen.getByText(/İade Tutarı:/)).toBeInTheDocument();
    expect(screen.getByText("₺12.560,00")).toBeInTheDocument();
    expect(screen.getByText("Check-In Yapıldı")).toBeInTheDocument();
    expect(screen.getByText("Check-Out Yapıldı")).toBeInTheDocument();
  });

  it("cancels confirmed reservations and refreshes detail data", async () => {
    const user = userEvent.setup();
    const mutate = mockReservation();
    mutateCancelReservationMock.mockResolvedValue(undefined);

    render(<ReservationDetailPage />);

    await user.click(screen.getByRole("button", { name: "İptal Et" }));

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
    mockReservation();
    mutateCancelReservationMock.mockRejectedValue(new Error("cancel failed"));

    render(<ReservationDetailPage />);

    await user.click(screen.getByRole("button", { name: "İptal Et" }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("İptal işlemi başarısız");
    });
  });

  it("checks in confirmed reservations", async () => {
    const user = userEvent.setup();
    const mutate = mockReservation();
    mutateCheckInMock.mockResolvedValue(undefined);

    render(<ReservationDetailPage />);

    await user.click(screen.getByRole("button", { name: /check-in/i }));

    await waitFor(() => {
      expect(mutateCheckInMock).toHaveBeenCalledWith("reservation-1", {
        checkedInBy: "Admin",
      });
    });
    expect(toastSuccessMock).toHaveBeenCalledWith("Check-in yapıldı");
    expect(mutate).toHaveBeenCalled();
  });

  it("checks out active reservations", async () => {
    const user = userEvent.setup();
    const mutate = mockReservation({ status: ReservationStatus.ACTIVE });
    mutateCheckOutMock.mockResolvedValue(undefined);

    render(<ReservationDetailPage />);

    await user.click(screen.getByRole("button", { name: /check-out/i }));

    await waitFor(() => {
      expect(mutateCheckOutMock).toHaveBeenCalledWith("reservation-1", {
        checkedOutBy: "Admin",
      });
    });
    expect(toastSuccessMock).toHaveBeenCalledWith("Check-out yapıldı");
    expect(mutate).toHaveBeenCalled();
  });

  it("submits a partial refund with amount, reason, and idempotency key", async () => {
    const user = userEvent.setup();
    const mutate = mockReservation();
    mutateRefundReservationMock.mockResolvedValue(undefined);

    render(<ReservationDetailPage />);

    await user.click(screen.getByRole("button", { name: "İade Et" }));
    await user.type(screen.getByLabelText("İade Tutarı (opsiyonel)"), "1250.5");
    await user.type(screen.getByLabelText("İade Nedeni (opsiyonel)"), "Müşteri talebi");
    await user.click(screen.getAllByRole("button", { name: "İade Et" }).at(-1)!);

    await waitFor(() => {
      expect(mutateRefundReservationMock).toHaveBeenCalledWith("reservation-1", {
        amount: 1250.5,
        reason: "Müşteri talebi",
        idempotencyKey: "refund-key-1",
      });
    });
    expect(toastSuccessMock).toHaveBeenCalledWith("İade işlemi tamamlandı");
    expect(mutate).toHaveBeenCalled();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("renders fallback sections for completed reservations with sparse optional data", () => {
    mockReservation({
      status: ReservationStatus.COMPLETED,
      paymentStatus: PaymentStatus.REFUNDED,
      reservationCode: "",
      driver: undefined as unknown as AdminReservation["driver"],
      priceBreakdown: undefined as unknown as AdminReservation["priceBreakdown"],
      vehicleName: "",
      vehicle: undefined,
      vehiclePlate: undefined,
      assignedVehicleId: undefined,
      campaignCode: undefined,
      adminNotes: undefined,
      cancellationReason: undefined,
      refundAmount: undefined,
      checkedInAt: undefined,
      checkedOutAt: undefined,
    });

    render(<ReservationDetailPage />);

    expect(screen.getByText("PUB-1001")).toBeInTheDocument();
    expect(screen.getByText("Tamamlandı")).toBeInTheDocument();
    expect(screen.getByText("İade Edildi")).toBeInTheDocument();
    expect(screen.getByText("Sürücü bilgisi bulunmuyor")).toBeInTheDocument();
    expect(screen.getByText("Fiyat bilgisi bulunmuyor")).toBeInTheDocument();
    expect(screen.getByText("Admin notu bulunmuyor")).toBeInTheDocument();
    expect(screen.getByText("Check-In bekleniyor")).toBeInTheDocument();
    expect(screen.getByText("Check-Out bekleniyor")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /iptal et/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /check-in/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /check-out/i })).not.toBeInTheDocument();
  });
});
