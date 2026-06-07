import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import BookingConfirmationPage from "./page";

const getReservationByPublicCodeMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParams,
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/lib/api/reservations", () => ({
  getReservationByPublicCode: (code: string) => getReservationByPublicCodeMock(code),
}));

describe("BookingConfirmationPage", () => {
  beforeEach(() => {
    searchParams = new URLSearchParams();
    getReservationByPublicCodeMock.mockReset();
    getReservationByPublicCodeMock.mockResolvedValue(null);
  });

  it("renders the reservation summary when confirmation details are present", async () => {
    searchParams = new URLSearchParams({
      code: "ALN-2026-1001",
    });
    getReservationByPublicCodeMock.mockResolvedValue({
      id: "res-1",
      publicCode: "ALN-2026-1001",
      status: "CONFIRMED",
      vehicleName: "Renault Clio",
      pickupOfficeName: "Alanya Merkez",
      pickupDate: "2026-06-10",
      pickupTime: "09:30",
      returnOfficeName: "Gazipaşa Havalimanı",
      returnDate: "2026-06-14",
      returnTime: "11:00",
      priceBreakdown: { totalAmount: 3200, currency: "TRY" },
    });

    render(<BookingConfirmationPage />);

    expect(screen.getByText("booking.confirmation.title")).toBeInTheDocument();
    expect(screen.getByText("booking.payment.summary.title")).toBeInTheDocument();
    expect(screen.getByText("ALN-2026-1001")).toBeInTheDocument();
    expect(await screen.findByText("Renault Clio")).toBeInTheDocument();
    expect(screen.getByText("Alanya Merkez - 2026-06-10 - 09:30")).toBeInTheDocument();
    expect(screen.getByText("Gazipaşa Havalimanı - 2026-06-14 - 11:00")).toBeInTheDocument();
    expect(screen.getByText("₺3.200,00")).toBeInTheDocument();
    expect(screen.queryByText("booking.confirmation.instructions")).not.toBeInTheDocument();
  });

  it("shows fallback instructions when no confirmation details are available", () => {
    render(<BookingConfirmationPage />);

    expect(screen.getByText("booking.confirmation.instructions")).toBeInTheDocument();
    expect(screen.queryByText("booking.payment.summary.title")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "navigation.home" })).toHaveAttribute("href", "/");
  });

  it("uses the unpaid request confirmation copy when requested", () => {
    searchParams = new URLSearchParams({
      code: "ALN-REQ-123",
      request: "unpaid",
    });

    render(<BookingConfirmationPage />);

    expect(screen.getByText("booking.confirmation.unpaidRequestTitle")).toBeInTheDocument();
    expect(screen.getByText("booking.confirmation.unpaidRequestMessage")).toBeInTheDocument();
    expect(screen.getByText("ALN-REQ-123")).toBeInTheDocument();
  });
});
