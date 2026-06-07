import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep4Page from "./page";

const createReservationMock = vi.fn();
const createUnpaidReservationRequestMock = vi.fn();
const createPaymentIntentMock = vi.fn();
const getPublicSiteSettingsMock = vi.fn();
const validateCampaignMock = vi.fn();
const pushMock = vi.fn();
const toastErrorMock = vi.fn();
let searchParams = new URLSearchParams();

type BookingDates = {
  pickupOfficeId: string;
  pickupOfficeName: string;
  pickupDate: string;
  pickupTime: string;
  returnOfficeId: string;
  returnOfficeName: string;
  returnDate: string;
  returnTime: string;
};

type BookingVehicle = {
  vehicleGroupId: string;
  vehicleName: string;
  vehicleImage: string;
  dailyPrice: number;
  groupName: string;
};

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
  } as BookingDates | undefined,
  vehicle: {
    vehicleGroupId: "economy",
    vehicleName: "Fiat Egea or similar",
    vehicleImage: "/images/vehicles/economy.png",
    dailyPrice: 45,
    groupName: "Economy",
  } as BookingVehicle | undefined,
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

const baseVehicle: BookingVehicle = {
  vehicleGroupId: "economy",
  vehicleName: "Fiat Egea or similar",
  vehicleImage: "/images/vehicles/economy.png",
  dailyPrice: 45,
  groupName: "Economy",
};

const baseDates: BookingDates = {
  pickupOfficeId: "ala",
  pickupOfficeName: "Alanya City Center",
  pickupDate: "2026-05-10",
  pickupTime: "10:00",
  returnOfficeId: "gzp",
  returnOfficeName: "Gazipasa Airport",
  returnDate: "2026-05-13",
  returnTime: "09:00",
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
  createUnpaidReservationRequest: (...args: unknown[]) => createUnpaidReservationRequestMock(...args),
}));

vi.mock("@/lib/api/publicSiteSettings", () => ({
  getPublicSiteSettings: (...args: unknown[]) => getPublicSiteSettingsMock(...args),
}));

vi.mock("@/lib/api/payments", () => ({
  createPaymentIntent: (...args: unknown[]) => createPaymentIntentMock(...args),
}));

vi.mock("@/lib/api/pricing", () => ({
  validateCampaign: (...args: unknown[]) => validateCampaignMock(...args),
}));

const placeHoldMock = vi.fn();

vi.mock("@/hooks/useReservations", () => ({
  usePlaceHold: () => ({
    placeHold: (...args: unknown[]) => placeHoldMock(...args),
    isPlacingHold: false,
    error: null,
  }),
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
    createReservationMock.mockResolvedValue({ id: "res-123", publicCode: "ALN-REAL-123" });
    createUnpaidReservationRequestMock.mockReset();
    createUnpaidReservationRequestMock.mockResolvedValue({ id: "res-unpaid", publicCode: "ALN-REQ-123" });
    createPaymentIntentMock.mockReset();
    createPaymentIntentMock.mockResolvedValue({ paymentIntentId: "pi-123" });
    getPublicSiteSettingsMock.mockReset();
    getPublicSiteSettingsMock.mockResolvedValue({ onlinePaymentEnabled: true });
    placeHoldMock.mockReset();
    placeHoldMock.mockResolvedValue({ id: "res-123", publicCode: "ALN-REAL-123" });
    validateCampaignMock.mockReset();
    toastErrorMock.mockReset();
    pushMock.mockReset();
    bookingState.vehicle = { ...baseVehicle };
    bookingState.dates = { ...baseDates };
    searchParams = new URLSearchParams({
      vehicle: "economy",
      pickupDate: "2026-05-10",
      returnDate: "2026-05-13",
      extras: "gps,additional_driver",
    });
    Object.defineProperty(globalThis, "crypto", {
      value: { randomUUID: () => "uuid-123" },
      configurable: true,
    });
    sessionStorage.clear();
  });

  it("blocks card checkout until terms are accepted and valid card details are provided", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await user.click(await screen.findByRole("button", { name: /complete booking/i }));

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

    await screen.findByRole("button", { name: /complete booking/i });

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
    const appliedElements = await screen.findAllByText("Applied");
    expect(appliedElements.length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Applied" })).toBeDisabled();
  });

  it("shows an error toast when the campaign code is invalid", async () => {
    const user = userEvent.setup();
    validateCampaignMock.mockResolvedValue({ valid: false });

    render(<BookingStep4Page />);

    await screen.findByRole("button", { name: /complete booking/i });

    await user.type(screen.getByPlaceholderText(/enter code/i), "badcode");
    await user.click(screen.getByRole("button", { name: "Apply" }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Invalid campaign code.");
    });
    expect(screen.queryByText(/applied!/i)).not.toBeInTheDocument();
  });

  it("shows an error toast when campaign validation cannot run because booking details are missing", async () => {
    const user = userEvent.setup();

    bookingState.vehicle = undefined;
    bookingState.dates = undefined;
    searchParams = new URLSearchParams();

    render(<BookingStep4Page />);

    await screen.findByRole("button", { name: /send request/i });

    await user.type(screen.getByPlaceholderText(/enter code/i), "summer15");
    await user.click(screen.getByRole("button", { name: "Apply" }));

    expect(toastErrorMock).toHaveBeenCalledWith("Missing booking details for campaign validation.");
    expect(validateCampaignMock).not.toHaveBeenCalled();
  });

  it("shows an error toast when campaign validation returns no response", async () => {
    const user = userEvent.setup();
    validateCampaignMock.mockResolvedValue(null);

    render(<BookingStep4Page />);

    await screen.findByRole("button", { name: /complete booking/i });

    await user.type(screen.getByPlaceholderText(/enter code/i), "summer15");
    await user.click(screen.getByRole("button", { name: "Apply" }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Failed to validate campaign code.");
    });
  });

  it("allows PayPal checkout without card details and navigates to confirmation", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await user.click(await screen.findByRole("radio", { name: /paypal/i }));
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
        driver: bookingState.driver,
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

    await user.click(await screen.findByRole("radio", { name: /paypal/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Reservation service unavailable");
    });
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("shows an error toast and stops when the reservation hold cannot be created", async () => {
    const user = userEvent.setup();

    placeHoldMock.mockResolvedValueOnce(null);

    render(<BookingStep4Page />);

    await user.click(await screen.findByRole("radio", { name: /paypal/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Failed to hold reservation. Please try again.");
    });
    expect(createPaymentIntentMock).not.toHaveBeenCalled();
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("creates a payment intent for card payments and continues to confirmation when no redirect is required", async () => {
    const user = userEvent.setup();

    createPaymentIntentMock.mockResolvedValueOnce({
      paymentIntentId: "pi-redirect",
    });

    render(<BookingStep4Page />);

    await user.type(await screen.findByLabelText("Card Number"), "4111 1111 1111 1111");
    await user.type(screen.getByLabelText("Name on Card"), "Jane Doe");
    await user.type(screen.getByLabelText("Expiry Date"), "12/30");
    await user.type(screen.getByLabelText("CVV"), "123");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(createPaymentIntentMock).toHaveBeenCalledWith({
        reservationId: "res-123",
        idempotencyKey: "uuid-123",
        card: {
          holderName: "Jane Doe",
          number: "4111111111111111",
          expiryMonth: "12",
          expiryYear: "30",
          cvv: "123",
        },
      });
    });

    expect(sessionStorage.getItem("pendingPaymentIntentId")).toBe("pi-redirect");
    expect(sessionStorage.getItem("pendingReservationPublicCode")).toBe("ALN-REAL-123");
    expect(pushMock).toHaveBeenCalledWith(
      "/en/booking/confirmation?vehicle=economy&pickupDate=2026-05-10&returnDate=2026-05-13&extras=gps%2Cadditional_driver&code=ALN-REAL-123"
    );
  });

  it("creates an unpaid request without card fields, hold, or payment intent when online payment is disabled", async () => {
    const user = userEvent.setup();
    getPublicSiteSettingsMock.mockResolvedValueOnce({ onlinePaymentEnabled: false });

    render(<BookingStep4Page />);

    expect(await screen.findByText("Request without online payment")).toBeInTheDocument();
    expect(screen.queryByLabelText("Card Number")).not.toBeInTheDocument();

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /send request/i }));

    await waitFor(() => {
      expect(createUnpaidReservationRequestMock).toHaveBeenCalledWith({
        vehicleGroupId: "economy",
        pickupOfficeId: "ala",
        returnOfficeId: "gzp",
        pickupDateTimeUtc: "2026-05-10T10:00:00Z",
        returnDateTimeUtc: "2026-05-13T09:00:00Z",
        customer: bookingState.customer,
        driver: bookingState.driver,
        extraDriverCount: 1,
        childSeatCount: 0,
        campaignCode: undefined,
      });
    });
    expect(placeHoldMock).not.toHaveBeenCalled();
    expect(createPaymentIntentMock).not.toHaveBeenCalled();
    expect(pushMock).toHaveBeenCalledWith(
      "/en/booking/confirmation?vehicle=economy&pickupDate=2026-05-10&returnDate=2026-05-13&extras=gps%2Cadditional_driver&code=ALN-REQ-123&request=unpaid"
    );
  });

  it("offers an unpaid request secondary action when online payment is enabled", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await screen.findByRole("button", { name: /complete booking/i });
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /send request without payment/i }));

    await waitFor(() => {
      expect(createUnpaidReservationRequestMock).toHaveBeenCalled();
    });
    expect(createReservationMock).not.toHaveBeenCalled();
    expect(createPaymentIntentMock).not.toHaveBeenCalled();
  });
});
