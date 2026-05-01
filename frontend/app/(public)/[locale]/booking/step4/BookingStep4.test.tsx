import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep4Page from "./page";

const createReservationMock = vi.fn();
const validateCampaignMock = vi.fn();
const pushMock = vi.fn();
const toastErrorMock = vi.fn();
let searchParams = new URLSearchParams();

const bookingState = {
  dates: {
    pickupOfficeId: "ala",
    pickupOfficeName: "Alanya City Center",
    pickupDate: "2026-05-10",
    pickupTime: "10:00",
    returnOfficeId: "gzp",
    returnOfficeName: "Gazipasa Airport",
    returnDate: "2026-05-13",
    returnTime: "09:00",
  },
  vehicle: {
    vehicleGroupId: "economy",
    vehicleName: "Fiat Egea or similar",
    vehicleImage: "/images/vehicles/economy.png",
    dailyPrice: 45,
    groupName: "Economy",
  },
  extras: { items: [], total: 0 },
  customer: {
    firstName: "Jane",
    lastName: "Doe",
    email: "jane@example.com",
    phone: "+905551234567",
    dateOfBirth: "1990-05-10",
  },
  driver: {
    firstName: "Jane",
    lastName: "Doe",
    dateOfBirth: "1990-05-10",
    licenseNumber: "TR12345",
    licenseCountry: "TR",
    licenseIssueDate: "2015-05-10",
    licenseExpiryDate: "2030-05-10",
    isPrimaryDriver: true,
  },
  campaignCode: null,
  campaignDiscount: null,
  step: "payment" as const,
  isComplete: true,
};

vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en" }),
  useRouter: () => ({ push: pushMock }),
  useSearchParams: () => searchParams,
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("@/hooks/useBooking", () => ({
  useBookingState: () => bookingState,
}));

vi.mock("@/lib/api/reservations", () => ({
  createReservation: (...args: unknown[]) => createReservationMock(...args),
}));

vi.mock("@/lib/api/pricing", () => ({
  validateCampaign: (...args: unknown[]) => validateCampaignMock(...args),
}));

vi.mock("sonner", () => ({
  toast: {
    error: (...args: unknown[]) => toastErrorMock(...args),
  },
}));

describe("BookingStep4Page", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    createReservationMock.mockReset();
    createReservationMock.mockResolvedValue({ publicCode: "ALN-REAL-123" });
    validateCampaignMock.mockReset();
    toastErrorMock.mockReset();
    pushMock.mockReset();
    searchParams = new URLSearchParams({
      vehicle: "economy",
      pickupDate: "2026-05-10",
      returnDate: "2026-05-13",
      extras: "gps,additional_driver",
    });
  });

  it("blocks card checkout until terms are accepted and valid card details are provided", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    expect(await screen.findByText("You must accept the terms and conditions")).toBeInTheDocument();
    expect(pushMock).not.toHaveBeenCalled();

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    expect(pushMock).not.toHaveBeenCalled();
  });

  it("validates a campaign code through the API before applying it", async () => {
    const user = userEvent.setup();
    validateCampaignMock.mockResolvedValue({ valid: true });

    render(<BookingStep4Page />);

    await user.type(screen.getByPlaceholderText(/enter code/i), "summer15");
    await user.click(screen.getByRole("button", { name: "Apply" }));

    await waitFor(() => {
      expect(validateCampaignMock).toHaveBeenCalledWith({
        code: "SUMMER15",
        vehicleGroupId: "economy",
        rentalDays: 3,
        pickupDate: "2026-05-10",
      });
    });
    expect(await screen.findByText("Code SUMMER15 applied!")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Applied" })).toBeDisabled();
  });

  it("shows an error toast when the campaign code is invalid", async () => {
    const user = userEvent.setup();
    validateCampaignMock.mockResolvedValue({ valid: false });

    render(<BookingStep4Page />);

    await user.type(screen.getByPlaceholderText(/enter code/i), "badcode");
    await user.click(screen.getByRole("button", { name: "Apply" }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Invalid campaign code.");
    });
    expect(screen.queryByText(/applied!/i)).not.toBeInTheDocument();
  });

  it("allows PayPal checkout without card details and navigates to confirmation", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await user.click(screen.getByRole("radio", { name: /paypal/i }));
    expect(screen.queryByLabelText("Card Number")).not.toBeInTheDocument();

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(createReservationMock).toHaveBeenCalledWith({
        vehicleGroupId: "economy",
        pickupOfficeId: "ala",
        returnOfficeId: "gzp",
        pickupDateTimeUtc: "2026-05-10T10:00:00Z",
        returnDateTimeUtc: "2026-05-13T09:00:00Z",
        customer: bookingState.customer,
        extraDriverCount: 1,
        childSeatCount: 0,
        campaignCode: undefined,
      });
      expect(pushMock).toHaveBeenCalledWith(
        "/en/booking/confirmation?vehicle=economy&pickupDate=2026-05-10&returnDate=2026-05-13&extras=gps%2Cadditional_driver&code=ALN-REAL-123"
      );
    });
  });

  it("shows an error toast and does not redirect when reservation creation fails", async () => {
    const user = userEvent.setup();

    createReservationMock.mockRejectedValueOnce(new Error("Reservation service unavailable"));

    render(<BookingStep4Page />);

    await user.click(screen.getByRole("radio", { name: /paypal/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Reservation service unavailable");
    });
    expect(pushMock).not.toHaveBeenCalled();
  });
});
