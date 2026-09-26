import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ApiError, NetworkError } from "@/lib/api/client";

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
  vehicleId?: string;
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

const baseQuote = {
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
    { optionId: "gps", optionVersion: 2, code: "gps", name: "GPS Navigation", description: "Navigation", unitPrice: 8, pricingMode: "PER_DAY" as const, quantity: 1, rentalDays: 3, total: 24 },
    { optionId: "additional-driver", optionVersion: 1, code: "additional_driver", name: "Additional Driver", description: "Second driver", unitPrice: 15, pricingMode: "PER_RENTAL" as const, quantity: 1, rentalDays: 3, total: 15 },
  ],
};

describe("BookingStep4Page", () => {
  it("waits for restored office IDs before automatically requesting a quote", async () => {
    bookingState.dates = undefined;
    const { rerender } = render(<BookingStep4Page />);
    await waitFor(() => expect(getPublicSiteSettingsMock).toHaveBeenCalled());
    expect(createReservationQuoteMock).not.toHaveBeenCalled();
    bookingState.dates = { ...baseDates, pickupOfficeId: "11111111-1111-1111-1111-111111111111", returnOfficeId: "22222222-2222-2222-2222-222222222222" };
    rerender(<BookingStep4Page />);
    await waitFor(() => expect(createReservationQuoteMock).toHaveBeenCalledTimes(1));
    expect(createReservationQuoteMock).toHaveBeenCalledWith(expect.objectContaining({ pickupOfficeId: bookingState.dates.pickupOfficeId, returnOfficeId: bookingState.dates.returnOfficeId }), expect.any(String));
  });

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
    createReservationQuoteMock.mockResolvedValue(baseQuote);
    getPublicReservationExtraOptionsMock.mockReset();
    getPublicReservationExtraOptionsMock.mockResolvedValue([]);
    updateExtrasMock.mockReset();
    toastErrorMock.mockReset();
    pushMock.mockReset();
    bookingState.vehicle = { ...baseVehicle };
    bookingState.dates = { ...baseDates };
    bookingState.selectedExtras = [
      { optionId: "gps", optionVersion: 2, code: "gps", name: "GPS Navigation", description: "Navigation", quantity: 1, unitPrice: 8, pricingMode: "PER_DAY" },
      { optionId: "additional-driver", optionVersion: 1, code: "additional_driver", name: "Additional Driver", description: "Second driver", quantity: 1, unitPrice: 15, pricingMode: "PER_RENTAL" },
    ];
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

  it("shows every non-zero quote fee in the checkout summary", async () => {
    createReservationQuoteMock.mockResolvedValueOnce({
      ...baseQuote,
      airportFee: 25,
      oneWayFee: 30,
      youngDriverFee: 20,
      fullCoverageWaiverFee: 15,
      finalTotal: 279,
    });

    render(<BookingStep4Page />);

    expect(await screen.findByText("Airport pickup fee")).toBeInTheDocument();
    expect(screen.getByText("One-way return fee")).toBeInTheDocument();
    expect(screen.getByText("Young driver fee")).toBeInTheDocument();
    expect(screen.getByText("Full coverage waiver fee")).toBeInTheDocument();
  });

  it("calculates driver age at the pickup date", async () => {
    bookingState.driver = { ...bookingState.driver, dateOfBirth: "2001-05-11" };

    render(<BookingStep4Page />);

    await waitFor(() => {
      expect(createReservationQuoteMock).toHaveBeenCalledWith(
        expect.objectContaining({ driverAge: 24 }),
        "uuid-123"
      );
    });
  });

  it("reconciles stale extras and prepares a refreshed quote after an initial 409", async () => {
    updateExtrasMock.mockImplementationOnce((extras) => {
      bookingState.selectedExtras = extras;
    });
    createReservationQuoteMock
      .mockRejectedValueOnce(new ApiError({
        statusCode: 409,
        message: "Selected extra version is stale",
        code: "CONFLICT",
        timestamp: "2026-07-11T19:30:00Z",
        path: "/api/pricing/quote",
      }))
      .mockResolvedValueOnce({
        ...baseQuote,
        quoteId: "quote-refreshed",
        extrasTotal: 24,
        finalTotal: 174,
        extraItems: [baseQuote.extraItems[0]],
      });
    getPublicReservationExtraOptionsMock.mockResolvedValueOnce([
      { id: "gps", code: "gps", name: "GPS Navigation", description: "Navigation", unitPrice: 8, pricingMode: "PER_DAY", maxQuantity: 1, iconKey: "SHIELD", sortOrder: 1, version: 3 },
    ]);

    render(<BookingStep4Page />);

    await waitFor(() => expect(createReservationQuoteMock).toHaveBeenCalledTimes(2));
    expect(createReservationQuoteMock).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({
        selectedExtras: [{ optionId: "gps", optionVersion: 3, quantity: 1 }],
      }),
      "uuid-123"
    );
    expect(updateExtrasMock).toHaveBeenCalledWith([
      expect.objectContaining({ optionId: "gps", optionVersion: 3, quantity: 1 }),
    ]);
    expect(await screen.findByText("The price or rental conditions changed. Review and accept the updated offer.")).toBeInTheDocument();
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

  it.each([undefined, "car-a"])("submits an unpaid request with its quoted vehicle identity %s", async (vehicleId) => {
    const user = userEvent.setup();
    bookingState.dates = { ...baseDates, pickupTime: "01:00" };
    bookingState.vehicle = { ...baseVehicle, vehicleId, vehicleName: vehicleId ? "Fiat Verified A" : baseVehicle.vehicleName };
    createReservationQuoteMock.mockResolvedValue({ ...baseQuote, vehicleId });

    render(<BookingStep4Page />);
    if (vehicleId) {
      expect(await screen.findByText("Fiat Verified A")).toBeInTheDocument();
      expect(screen.queryByText("Economy - Fiat Verified A")).not.toBeInTheDocument();
    }

    await user.click(await screen.findByRole("radio", { name: /request without online payment|pay at pickup/i }));
    expect(screen.queryByLabelText("Card Number")).not.toBeInTheDocument();

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /send request|complete booking/i }));

    await waitFor(() => {
      expect(createUnpaidReservationRequestMock).toHaveBeenCalledWith(
        expect.objectContaining({
          vehicleGroupId: "economy",
          vehicleId,
          pickupOfficeId: "ala",
          returnOfficeId: "gzp",
          customer: bookingState.customer,
          driver: bookingState.driver,
          quoteId: "quote-123",
          pickupDateTimeUtc: "2026-05-09T22:00:00.000Z",
          returnDateTimeUtc: "2026-05-13T06:00:00.000Z",
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
    expect(createReservationQuoteMock).toHaveBeenCalledWith(expect.objectContaining({ vehicleId, pickupDateTimeUtc: "2026-05-09T22:00:00.000Z", returnDateTimeUtc: "2026-05-13T06:00:00.000Z" }), "uuid-123");
  });

  it.each(["disabled", "unavailable"])("confirms exact pay-at-pickup when legacy settings are %s", async (settingsState) => {
    const user = userEvent.setup();
    bookingState.vehicle = { ...baseVehicle, vehicleId: "car-a", vehicleName: "Fiat Exact" };
    createReservationQuoteMock.mockResolvedValue({ ...baseQuote, vehicleId: "car-a" });
    if (settingsState === "unavailable") {
      getPublicSiteSettingsMock.mockRejectedValueOnce(new Error("settings unavailable"));
    } else {
      getPublicSiteSettingsMock.mockResolvedValueOnce({ paymentMethods: {
        creditCardEnabled: false, debitCardEnabled: false, unpaidRequestEnabled: false,
        paypalEnabled: false, anyEnabled: false
      } });
    }
    render(<BookingStep4Page />);
    await user.click(await screen.findByRole("radio", { name: /pay at pickup/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));
    await waitFor(() => expect(createUnpaidReservationRequestMock).toHaveBeenCalledWith(
      expect.objectContaining({ vehicleId: "car-a", quoteId: "quote-123" }), expect.any(Object)
    ));
    expect(createPaymentIntentMock).not.toHaveBeenCalled();
    expect(screen.queryByText(/no active payment method/i)).not.toBeInTheDocument();
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
      expect(toastErrorMock).toHaveBeenCalledWith("Your booking could not be completed. Please try again.");
    });
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("does not mint a new quote when the current quote is already in use", async () => {
    const user = userEvent.setup();
    createReservationMock.mockRejectedValueOnce(new ApiError({
      statusCode: 409,
      message: "Reservation quote is already being used or was consumed.",
      code: "CONFLICT",
      timestamp: "2026-07-12T14:00:00Z",
      path: "/api/reservations",
    }));

    render(<BookingStep4Page />);

    await user.type(await screen.findByLabelText("Card Number"), "4111 1111 1111 1111");
    await user.type(screen.getByLabelText("Name on Card"), "Jane Doe");
    await user.type(screen.getByLabelText("Expiry Date"), "12/30");
    await user.type(screen.getByLabelText("CVV"), "123");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("The booking result could not be verified yet. Retry the same request or contact us before creating another booking.");
    });
    expect(createReservationMock).toHaveBeenCalledTimes(1);
    expect(getPublicReservationExtraOptionsMock).not.toHaveBeenCalled();
    expect(createReservationQuoteMock).toHaveBeenCalledTimes(1);
    expect(updateExtrasMock).not.toHaveBeenCalled();
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("offers a dated search without silently substituting an unavailable vehicle", async () => {
    const user = userEvent.setup();
    bookingState.vehicle = { ...baseVehicle, vehicleId: "vehicle-1" };
    searchParams.set("preferredVehicleId", "vehicle-1");
    createReservationQuoteMock.mockResolvedValue({ ...baseQuote, vehicleId: "vehicle-1" });
    createUnpaidReservationRequestMock.mockRejectedValueOnce(new ApiError({ statusCode: 409, message: "Selected vehicle is unavailable for this itinerary.", code: "CONFLICT", timestamp: "2026-09-26", path: "/reservations" }));
    render(<BookingStep4Page />);
    await user.click(await screen.findByRole("radio", { name: /pay at pickup/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));
    const recovery = await screen.findByRole("link", { name: "Back to search" });
    expect(recovery).toHaveAttribute("href", "/en/vehicles?pickupDate=2026-05-10&returnDate=2026-05-13");
    expect(createUnpaidReservationRequestMock).toHaveBeenCalledTimes(1);
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("reuses the identical reservation attempt after a lost response", async () => {
    const user = userEvent.setup();
    let sequence = 0;
    Object.defineProperty(globalThis, "crypto", { value: { randomUUID: () => `attempt-${++sequence}` }, configurable: true });
    bookingState.vehicle = { ...baseVehicle, vehicleId: "vehicle-1" };
    createReservationQuoteMock.mockResolvedValue({ ...baseQuote, vehicleId: "vehicle-1" });
    createUnpaidReservationRequestMock.mockRejectedValueOnce(new NetworkError());
    render(<BookingStep4Page />);
    await user.click(await screen.findByRole("radio", { name: /pay at pickup/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));
    await waitFor(() => expect(toastErrorMock).toHaveBeenCalledWith("The response could not be received. Try again; the same booking attempt will be checked."));
    expect(pushMock).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: /complete booking/i }));
    await waitFor(() => expect(pushMock).toHaveBeenCalled());
    expect(createUnpaidReservationRequestMock).toHaveBeenCalledTimes(2);
    expect(createUnpaidReservationRequestMock.mock.calls[1]).toEqual(createUnpaidReservationRequestMock.mock.calls[0]);
    expect(createReservationQuoteMock).toHaveBeenCalledTimes(1);
  });

  it("requires acceptance of changed conditions even when the price is unchanged", async () => {
    const user = userEvent.setup();
    bookingState.vehicle = { ...baseVehicle, vehicleId: "vehicle-1" };
    bookingState.selectedExtras = [];
    createReservationQuoteMock
      .mockResolvedValueOnce({ ...baseQuote, vehicleId: "vehicle-1", conditions: { minAge: 21, minLicenseYears: 2 } })
      .mockResolvedValue({ ...baseQuote, quoteId: "updated-quote", vehicleId: "vehicle-1", conditions: { minAge: 25, minLicenseYears: 3 } });
    createUnpaidReservationRequestMock.mockRejectedValueOnce(new ApiError({ statusCode: 409, message: "Price or rental conditions changed. Request a new quote.", code: "CONFLICT", timestamp: "2026-09-26T10:00:00Z", path: "/api/reservations" }));
    render(<BookingStep4Page />);
    await user.click(await screen.findByRole("radio", { name: /pay at pickup/i }));
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));
    const accept = await screen.findByRole("button", { name: "Accept updated offer" });
    expect(createUnpaidReservationRequestMock).toHaveBeenCalledTimes(1);
    expect(screen.getByText("25")).toBeInTheDocument();
    expect(pushMock).not.toHaveBeenCalled();
    await user.click(accept);
    expect(createReservationQuoteMock).toHaveBeenCalledTimes(2);
    await user.click(screen.getByRole("button", { name: /complete booking/i }));
    await waitFor(() => expect(pushMock).toHaveBeenCalled());
    expect(createUnpaidReservationRequestMock).toHaveBeenLastCalledWith(expect.objectContaining({ quoteId: "updated-quote", vehicleId: "vehicle-1", customer: bookingState.customer }), expect.any(Object));
    expect(getPublicReservationExtraOptionsMock).toHaveBeenCalledWith("economy", "en", "vehicle-1");
  });

  it("requires explicit quote confirmation when a 409 refresh changes option terms", async () => {
    const user = userEvent.setup();
    createReservationMock.mockRejectedValueOnce(new ApiError({
      statusCode: 409,
      message: "A quoted extra option is no longer available.",
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
