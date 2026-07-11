import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ApiError } from "@/lib/api/client";

import BookingStep4Page from "./page";

const createReservationMock = vi.fn();
const createUnpaidReservationRequestMock = vi.fn();
const createPaymentIntentMock = vi.fn();
const getPublicSiteSettingsMock = vi.fn();
const validateCampaignMock = vi.fn();
const createReservationQuoteMock = vi.fn();
const getPublicReservationExtraOptionsMock = vi.fn();
const updateExtrasMock = vi.fn();
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
  selectedExtras: [
    { optionId: "gps", optionVersion: 2, code: "gps", name: "GPS Navigation", description: "Navigation", quantity: 1, unitPrice: 8, pricingMode: "PER_DAY" },
    { optionId: "additional-driver", optionVersion: 1, code: "additional_driver", name: "Additional Driver", description: "Second driver", quantity: 1, unitPrice: 15, pricingMode: "PER_RENTAL" },
  ],
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
  useBookingActions: () => ({ updateExtras: updateExtrasMock }),
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

vi.mock("@/lib/api/reservationExtras", () => ({
  createReservationQuote: (...args: unknown[]) => createReservationQuoteMock(...args),
  getPublicReservationExtraOptions: (...args: unknown[]) => getPublicReservationExtraOptionsMock(...args),
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
    getPublicSiteSettingsMock.mockResolvedValue({
      onlinePaymentEnabled: true,
      paymentMethods: {
        creditCardEnabled: true,
        debitCardEnabled: true,
        unpaidRequestEnabled: true,
        paypalEnabled: false,
        anyEnabled: true,
      },
    });
    placeHoldMock.mockReset();
    placeHoldMock.mockResolvedValue({ id: "res-123", publicCode: "ALN-REAL-123" });
    validateCampaignMock.mockReset();
    createReservationQuoteMock.mockReset();
    createReservationQuoteMock.mockResolvedValue({
      quoteId: "quote-123",
      expiresAtUtc: "2030-05-10T10:30:00Z",
      dailyRate: 45,
      rentalDays: 3,
      baseTotal: 150,
      extrasTotal: 39,
      campaignDiscount: 0,
      airportFee: 0,
      oneWayFee: 0,
      extraDriverFee: 0,
      childSeatFee: 0,
      youngDriverFee: 0,
      fullCoverageWaiverFee: 0,
      finalTotal: 189,
      currency: "TRY",
      depositAmount: 0,
      preAuthorizationAmount: 0,
      appliedCampaignCode: null,
      extraItems: [
        { optionId: "gps", optionVersion: 2, code: "gps", name: "GPS Navigation", description: "Navigation", unitPrice: 8, pricingMode: "PER_DAY", quantity: 1, rentalDays: 3, total: 24 },
        { optionId: "additional-driver", optionVersion: 1, code: "additional_driver", name: "Additional Driver", description: "Second driver", unitPrice: 15, pricingMode: "PER_RENTAL", quantity: 1, rentalDays: 3, total: 15 },
      ],
    });
    getPublicReservationExtraOptionsMock.mockReset();
    getPublicReservationExtraOptionsMock.mockResolvedValue([]);
    updateExtrasMock.mockReset();
    toastErrorMock.mockReset();
    pushMock.mockReset();
    bookingState.vehicle = { ...baseVehicle };
    bookingState.dates = { ...baseDates };
    searchParams = new URLSearchParams({
      vehicle: "economy",
      pickupDate: "2026-05-10",
      returnDate: "2026-05-13",
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
    await waitFor(() => {
      expect(createReservationQuoteMock).toHaveBeenLastCalledWith(
        expect.objectContaining({
          vehicleGroupId: "economy",
          pickupOfficeId: "ala",
          returnOfficeId: "gzp",
          campaignCode: "SUMMER15",
          locale: "en",
          selectedExtras: [
            { optionId: "gps", optionVersion: 2, quantity: 1 },
            { optionId: "additional-driver", optionVersion: 1, quantity: 1 },
          ],
        }),
        "uuid-123"
      );
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

    await screen.findByRole("button", { name: /complete booking/i });

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

  it("shows unpaid reservation as a payment method card and submits unpaid request", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await user.click(await screen.findByRole("radio", { name: /request without online payment/i }));
    expect(screen.queryByLabelText("Card Number")).not.toBeInTheDocument();

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /send request/i }));

    await waitFor(() => {
      expect(createUnpaidReservationRequestMock).toHaveBeenCalledWith(
        expect.objectContaining({
          vehicleGroupId: "economy",
          pickupOfficeId: "ala",
          returnOfficeId: "gzp",
          customer: bookingState.customer,
          driver: bookingState.driver,
          quoteId: "quote-123",
          locale: "en",
        }),
        { sessionId: "uuid-123", idempotencyKey: "uuid-123" }
      );
      expect(pushMock).toHaveBeenCalledWith(
        "/en/booking/confirmation?vehicle=economy&pickupDate=2026-05-10&returnDate=2026-05-13&code=ALN-REQ-123&request=unpaid"
      );
    });
    expect(createReservationMock).not.toHaveBeenCalled();
    expect(createPaymentIntentMock).not.toHaveBeenCalled();
  });

  it("shows an error toast and does not redirect when reservation creation fails", async () => {
    const user = userEvent.setup();

    createReservationMock.mockRejectedValueOnce(new Error("Reservation service unavailable"));

    render(<BookingStep4Page />);

    await user.type(await screen.findByLabelText("Card Number"), "4111 1111 1111 1111");
    await user.type(screen.getByLabelText("Name on Card"), "Jane Doe");
    await user.type(screen.getByLabelText("Expiry Date"), "12/30");
    await user.type(screen.getByLabelText("CVV"), "123");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Reservation service unavailable");
    });
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("requires explicit quote confirmation when a 409 refresh changes option terms", async () => {
    const user = userEvent.setup();
    createReservationMock.mockRejectedValueOnce(new ApiError({
      statusCode: 409,
      message: "Quote is stale",
      code: "CONFLICT",
      timestamp: "2026-07-11T18:00:00Z",
      path: "/api/reservations",
    }));
    getPublicReservationExtraOptionsMock.mockResolvedValueOnce([
      { id: "gps", code: "gps", name: "GPS Navigation", description: "Navigation", unitPrice: 8, pricingMode: "PER_RENTAL", maxQuantity: 1, iconKey: "SHIELD", sortOrder: 1, version: 3 },
      { id: "additional-driver", code: "additional_driver", name: "Additional Driver", description: "Second driver", unitPrice: 15, pricingMode: "PER_RENTAL", maxQuantity: 1, iconKey: "USERS", sortOrder: 2, version: 1 },
    ]);

    render(<BookingStep4Page />);

    await user.type(await screen.findByLabelText("Card Number"), "4111 1111 1111 1111");
    await user.type(screen.getByLabelText("Name on Card"), "Jane Doe");
    await user.type(screen.getByLabelText("Expiry Date"), "12/30");
    await user.type(screen.getByLabelText("CVV"), "123");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => expect(updateExtrasMock).toHaveBeenCalled());
    expect(createReservationMock).toHaveBeenCalledTimes(1);
    expect(updateExtrasMock).toHaveBeenCalledWith([
      expect.objectContaining({ optionId: "gps", optionVersion: 3, pricingMode: "PER_RENTAL" }),
      expect.objectContaining({ optionId: "additional-driver", optionVersion: 1 }),
    ]);
    expect(createPaymentIntentMock).not.toHaveBeenCalled();
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("shows an error toast and stops when the reservation hold cannot be created", async () => {
    const user = userEvent.setup();

    placeHoldMock.mockResolvedValueOnce(null);

    render(<BookingStep4Page />);

    await user.type(await screen.findByLabelText("Card Number"), "4111 1111 1111 1111");
    await user.type(screen.getByLabelText("Name on Card"), "Jane Doe");
    await user.type(screen.getByLabelText("Expiry Date"), "12/30");
    await user.type(screen.getByLabelText("CVV"), "123");
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
        paymentMethod: "credit_card",
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
      "/en/booking/confirmation?vehicle=economy&pickupDate=2026-05-10&returnDate=2026-05-13&code=ALN-REAL-123"
    );
  });

  it("falls back to no payment methods when public settings omit payment method details", async () => {
    getPublicSiteSettingsMock.mockResolvedValueOnce({ onlinePaymentEnabled: false });

    render(<BookingStep4Page />);

    expect(await screen.findByText(/no active payment method/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /complete booking/i })).toBeDisabled();
    expect(screen.queryByLabelText("Card Number")).not.toBeInTheDocument();
  });

  it("keeps checkout disabled when public settings cannot be loaded", async () => {
    getPublicSiteSettingsMock.mockRejectedValueOnce(new Error("settings unavailable"));

    render(<BookingStep4Page />);

    expect(await screen.findByText(/no active payment method/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /complete booking/i })).toBeDisabled();
    expect(createReservationMock).not.toHaveBeenCalled();
    expect(createUnpaidReservationRequestMock).not.toHaveBeenCalled();
  });

  it("uses the rendered payment method when the form default is stale", async () => {
    const user = userEvent.setup();
    getPublicSiteSettingsMock.mockResolvedValueOnce({
      onlinePaymentEnabled: true,
      paymentMethods: {
        creditCardEnabled: false,
        debitCardEnabled: true,
        unpaidRequestEnabled: false,
        paypalEnabled: false,
        anyEnabled: true,
      },
    });

    render(<BookingStep4Page />);

    await screen.findByRole("radio", { name: /debit card/i });
    await user.type(screen.getByLabelText("Card Number"), "4111 1111 1111 1111");
    await user.type(screen.getByLabelText("Name on Card"), "Jane Doe");
    await user.type(screen.getByLabelText("Expiry Date"), "12/30");
    await user.type(screen.getByLabelText("CVV"), "123");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(createPaymentIntentMock).toHaveBeenCalledWith(expect.objectContaining({
        paymentMethod: "debit_card",
      }));
    });
  });

  it("blocks submission when no payment methods are enabled", async () => {
    getPublicSiteSettingsMock.mockResolvedValueOnce({
      onlinePaymentEnabled: false,
      paymentMethods: {
        creditCardEnabled: false,
        debitCardEnabled: false,
        unpaidRequestEnabled: false,
        paypalEnabled: false,
        anyEnabled: false,
      },
    });

    render(<BookingStep4Page />);

    expect(await screen.findByText(/no active payment method/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /complete booking/i })).toBeDisabled();
    expect(screen.queryByLabelText("Card Number")).not.toBeInTheDocument();
  });

  it("does not render PayPal as an actionable public payment method", async () => {
    render(<BookingStep4Page />);

    await screen.findByRole("button", { name: /complete booking/i });

    expect(screen.queryByRole("radio", { name: /paypal/i })).not.toBeInTheDocument();
  });

  it("submits unpaid request through the payment method card when online payment is enabled", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await screen.findByRole("button", { name: /complete booking/i });
    await user.click(screen.getByRole("radio", { name: /request without online payment/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /send request/i }));

    await waitFor(() => {
      expect(createUnpaidReservationRequestMock).toHaveBeenCalled();
    });
    expect(createReservationMock).not.toHaveBeenCalled();
    expect(createPaymentIntentMock).not.toHaveBeenCalled();
  });
});
