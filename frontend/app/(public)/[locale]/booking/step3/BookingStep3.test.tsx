import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep3Page from "./page";

const pushMock = vi.fn();
const updateCustomerDetailsMock = vi.fn();
const updateExtrasMock = vi.fn();
const setDatesMock = vi.fn();
const useOfficesMock = vi.fn();
const getPublicReservationExtraOptionsMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en" }),
  useRouter: () => ({ push: pushMock }),
  useSearchParams: () => searchParams,
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("@/hooks/useBooking", () => ({
  useBookingActions: () => ({
    updateCustomerDetails: updateCustomerDetailsMock,
    updateExtras: updateExtrasMock,
    setDates: setDatesMock,
  }),
  useBookingState: () => ({
    vehicle: { vehicleGroupId: "group-1" },
    selectedExtras: [],
  }),
}));

vi.mock("@/lib/api/reservationExtras", () => ({
  getPublicReservationExtraOptions: (...args: unknown[]) => getPublicReservationExtraOptionsMock(...args),
}));

vi.mock("@/hooks/useVehicles", () => ({
  useOffices: () => useOfficesMock(),
}));

describe("BookingStep3Page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getPublicReservationExtraOptionsMock.mockResolvedValue([
      { id: "child-seat", code: "child_seat", name: "Child Seat", description: "For children", unitPrice: 10, pricingMode: "PER_DAY", maxQuantity: 2, iconKey: "BABY", sortOrder: 1, version: 1 },
      { id: "gps", code: "gps", name: "GPS Navigation", description: "Navigation", unitPrice: 8, pricingMode: "PER_DAY", maxQuantity: 1, iconKey: "SHIELD", sortOrder: 2, version: 2 },
    ]);
    searchParams = new URLSearchParams({ vehicle: "compact" });
    useOfficesMock.mockReturnValue({
      offices: [
        { id: "11111111-1111-1111-1111-111111111111", name: "Alanya City Center" },
        { id: "22222222-2222-2222-2222-222222222222", name: "Gazipasa Airport" },
      ],
      isLoading: false,
      isError: false,
    });
  });

  it("validates required customer details before advancing", async () => {
    const user = userEvent.setup();

    render(<BookingStep3Page />);

    await user.click(screen.getByRole("button", { name: /continue to payment/i }));

    expect(await screen.findByText("First name is required")).toBeInTheDocument();
    expect(screen.getByText("Last name is required")).toBeInTheDocument();
    expect(screen.getByText("Invalid email address")).toBeInTheDocument();
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("stores server catalog selections with bounded quantities", async () => {
    const user = userEvent.setup();

    render(<BookingStep3Page />);

    await user.type(screen.getByLabelText("First Name"), "Jane");
    await user.type(screen.getByLabelText("Last Name"), "Doe");
    await user.type(screen.getByLabelText("Email"), "jane@example.com");
    await user.type(screen.getByLabelText("Phone"), "+905551234567");
    await user.type(screen.getByLabelText("Date of Birth"), "1990-05-10");
    await user.type(screen.getByLabelText("License Number"), "TR-12345");
    const increaseChildSeat = await screen.findByRole("button", { name: /increase quantity child seat/i });
    const increaseGps = screen.getByRole("button", { name: /increase quantity gps navigation/i });

    await user.click(increaseChildSeat);
    await user.click(increaseGps);

    expect(updateExtrasMock).toHaveBeenCalledWith([
      expect.objectContaining({ optionId: "child-seat", optionVersion: 1, quantity: 1 }),
    ]);
    expect(updateExtrasMock).toHaveBeenCalledWith([
      expect.objectContaining({ optionId: "gps", optionVersion: 2, quantity: 1 }),
    ]);
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("stores resolved pickup and return offices before continuing from a direct vehicle selection", async () => {
    const user = userEvent.setup();
    searchParams = new URLSearchParams({
      pickup: "ala",
      return: "gzp",
      pickupDate: "2026-06-10",
      pickupTime: "10:00",
      returnDate: "2026-06-14",
      returnTime: "09:00",
      vehicle: "group-1",
      dailyPrice: "2500",
      vehicleName: "Nissan Qashqai",
    });

    render(<BookingStep3Page />);

    await user.type(screen.getByLabelText("First Name"), "Jane");
    await user.type(screen.getByLabelText("Last Name"), "Doe");
    await user.type(screen.getByLabelText("Email"), "jane@example.com");
    await user.type(screen.getByLabelText("Phone"), "+905551234567");
    await user.type(screen.getByLabelText("Date of Birth"), "1990-05-10");
    await user.type(screen.getByLabelText("License Number"), "TR-12345");
    await user.type(screen.getByLabelText("License Country"), "TR");
    await user.click(screen.getByRole("button", { name: /continue to payment/i }));

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith(
        "/en/booking/step4?pickup=ala&return=gzp&pickupDate=2026-06-10&pickupTime=10%3A00&returnDate=2026-06-14&returnTime=09%3A00&vehicle=group-1&dailyPrice=2500&vehicleName=Nissan+Qashqai"
      );
    });
    expect(setDatesMock).toHaveBeenCalledWith({
      pickupOfficeId: "11111111-1111-1111-1111-111111111111",
      pickupOfficeName: "Alanya City Center",
      pickupDate: "2026-06-10",
      pickupTime: "10:00",
      returnOfficeId: "22222222-2222-2222-2222-222222222222",
      returnOfficeName: "Gazipasa Airport",
      returnDate: "2026-06-14",
      returnTime: "09:00",
    });
    expect(updateCustomerDetailsMock).toHaveBeenCalled();
  });

  it("does not continue while direct office slugs have not resolved to backend IDs", async () => {
    const user = userEvent.setup();
    useOfficesMock.mockReturnValue({
      offices: [],
      isLoading: true,
      isError: false,
    });
    searchParams = new URLSearchParams({
      pickup: "ala",
      return: "gzp",
      pickupDate: "2026-06-10",
      pickupTime: "10:00",
      returnDate: "2026-06-14",
      returnTime: "09:00",
      vehicle: "group-1",
    });

    render(<BookingStep3Page />);

    const continueButton = screen.getByRole("button", { name: /continue to payment/i });
    expect(continueButton).toBeDisabled();

    await user.click(continueButton);

    expect(setDatesMock).not.toHaveBeenCalled();
    expect(pushMock).not.toHaveBeenCalled();
  });
});
