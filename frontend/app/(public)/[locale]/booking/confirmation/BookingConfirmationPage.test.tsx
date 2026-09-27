import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingConfirmationPage from "./page";

const getReservationByPublicCodeMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParams,
}));

vi.mock("next-intl", () => ({
  useLocale: () => "tr",
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
      publicCode: "ALN-2026-1001",
      status: "CONFIRMED",
      vehicleGroupName: "Economy",
      customer: {
        name: "Ada Lovelace",
        email: "ada@example.com",
      },
      pickupOfficeName: "Alanya Merkez",
      pickupDateTime: "2026-06-09T22:00:00Z",
      returnOfficeName: "Gazipaşa Havalimanı",
      returnDateTime: "2026-06-14T11:00:00Z",
      totalAmount: 3200,
      depositAmount: 5000,
      currency: "TRY",
    });

    render(<BookingConfirmationPage />);

    expect(screen.getByText("booking.confirmation.title")).toBeInTheDocument();
    expect(screen.getByText("booking.payment.summary.title")).toBeInTheDocument();
    expect(screen.getByText("ALN-2026-1001")).toBeInTheDocument();
    expect(await screen.findByText("Economy")).toBeInTheDocument();
    expect(screen.getByText("Alanya Merkez - 2026-06-10 - 01:00")).toBeInTheDocument();
    expect(screen.getByText("Gazipaşa Havalimanı - 2026-06-14 - 14:00")).toBeInTheDocument();
    expect(screen.getByText("₺3.200,00")).toBeInTheDocument();
    expect(screen.queryByText("Ada Lovelace")).not.toBeInTheDocument();
    expect(screen.queryByText("ada@example.com")).not.toBeInTheDocument();
    expect(screen.queryByText("booking.confirmation.instructions")).not.toBeInTheDocument();
  });

  it("shows fallback instructions when no confirmation details are available", () => {
    render(<BookingConfirmationPage />);

    expect(screen.getByText("booking.confirmation.instructions")).toBeInTheDocument();
    expect(screen.queryByText("booking.payment.summary.title")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "navigation.home" })).toHaveAttribute("href", "/");
  });

  it("keeps the code and retries failed exact-booking verification without claiming a temporary request", async () => {
    const user = userEvent.setup();
    searchParams = new URLSearchParams({ code: "TEST-CODE", request: "unpaid", preferredVehicleId: "vehicle-1" });
    getReservationByPublicCodeMock.mockRejectedValueOnce(new Error("rate limited")).mockResolvedValueOnce({ publicCode: "TEST-CODE", status: "Confirmed", vehicleGroupName: "Exact vehicle", pickupDateTime: "2026-10-10T07:00:00Z", returnDateTime: "2026-10-13T07:00:00Z", totalAmount: 3600, currency: "TRY" });
    render(<BookingConfirmationPage />);
    expect(await screen.findByText("booking.confirmation.verificationFailed")).toBeInTheDocument();
    expect(screen.getByText("TEST-CODE")).toBeInTheDocument();
    expect(screen.queryByText("booking.confirmation.unpaidRequestMessage")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "booking.confirmation.retryDetails" }));
    expect(await screen.findByText("booking.payAtPickup.confirmedTitle")).toBeInTheDocument();
    expect(getReservationByPublicCodeMock).toHaveBeenCalledTimes(2);
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
