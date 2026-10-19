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
let storedDetails: Record<string, unknown> = {};
let selectedExtras: Array<{
  optionId: string;
  optionVersion: number;
  code: string;
  name: string;
  description: string;
  quantity: number;
  unitPrice: number;
  pricingMode: string;
}> = [];

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
    selectedExtras,
    ...storedDetails,
  }),
}));

vi.mock("@/lib/api/reservationExtras", () => ({
  getPublicReservationExtraOptions: (...args: unknown[]) => getPublicReservationExtraOptionsMock(...args),
}));

vi.mock("@/hooks/useVehicles", () => ({
  useOffices: () => useOfficesMock(),
}));

describe("BookingStep3Page", () => {
  it("returns to vehicle selection without the automatic detail handoff", async () => {
    searchParams = new URLSearchParams({ preferredVehicleId: "car-a", pickup: "ala", return: "gzp", pickupDate: "2026-10-01", returnDate: "2026-10-03" });
    render(<BookingStep3Page />);
    const back = screen.getByRole("link", { name: /back/i });
    const url = new URL(back.getAttribute("href")!, "https://example.test");
    expect(url.pathname).toBe("/en/booking/step2");
    expect(url.searchParams.has("preferredVehicleId")).toBe(false);
    expect(url.searchParams.get("pickup")).toBe("ala");
    expect(url.searchParams.get("returnDate")).toBe("2026-10-03");
    await waitFor(() => expect(getPublicReservationExtraOptionsMock).toHaveBeenCalled());
  });

  beforeEach(() => {
    vi.clearAllMocks();
    selectedExtras = [];
    storedDetails = {};
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

  it.each(["group-url", "00000000-0000-0000-0000-000000000000"])("loads exact vehicle extras from a restored URL with group %s", async (groupId) => {
    storedDetails = { vehicle: undefined };
    searchParams = new URLSearchParams({ vehicle: groupId, preferredVehicleId: "car-url" });

    const { rerender } = render(<BookingStep3Page />);
    await waitFor(() => expect(getPublicReservationExtraOptionsMock).toHaveBeenCalledWith(groupId, "en", "car-url"));

    searchParams = new URLSearchParams({ vehicle: groupId, preferredVehicleId: "car-url-next" });
    rerender(<BookingStep3Page />);
    await waitFor(() => expect(getPublicReservationExtraOptionsMock).toHaveBeenCalledWith(groupId, "en", "car-url-next"));
  });

  it.each(["car-store", undefined])("prefers the stored vehicle identity %s over a stale exact-vehicle URL", async (vehicleId) => {
    storedDetails = { vehicle: { vehicleGroupId: "group-store", vehicleId } };
    searchParams = new URLSearchParams({ vehicle: "group-url", preferredVehicleId: "car-url" });

    render(<BookingStep3Page />);
    await waitFor(() => expect(getPublicReservationExtraOptionsMock).toHaveBeenCalledWith("group-store", "en", vehicleId));
    expect(getPublicReservationExtraOptionsMock).not.toHaveBeenCalledWith(expect.anything(), "en", "car-url");
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

  it("restores submitted driver details when returning from checkout", async () => {
    storedDetails = {
      customer: { firstName: "Jane", lastName: "Doe", email: "jane@example.test", phone: "+905550000099" },
      driver: { declaration: { ageAtPickup: 30, licenseYearsAtPickup: 8, licenseValidThroughReturn: true, documentsAvailableAtPickup: true } },
    };
    render(<BookingStep3Page />);
    expect(screen.getByLabelText("First Name")).toHaveValue("Jane");
    expect(screen.getByLabelText("Age on pickup date")).toHaveValue(30);
    expect(screen.getByLabelText("Completed licence years at pickup")).toHaveValue(8);
    expect(screen.queryByLabelText("Date of Birth")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("License Number")).not.toBeInTheDocument();
  });

  it("stores server catalog selections with bounded quantities", async () => {
    const user = userEvent.setup();

    render(<BookingStep3Page />);

    await user.type(screen.getByLabelText("First Name"), "Jane");
    await user.type(screen.getByLabelText("Last Name"), "Doe");
    await user.type(screen.getByLabelText("Email"), "jane@example.com");
    await user.type(screen.getByLabelText("Phone"), "+905551234567");
    await user.type(screen.getByLabelText("Age on pickup date"), "30");
    await user.type(screen.getByLabelText("Completed licence years at pickup"), "8");
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

  it("aggregates repeated supported legacy extra codes into bounded quantities", async () => {
    searchParams = new URLSearchParams({ vehicle: "compact", extras: "child_seat,child_seat,gps" });

    render(<BookingStep3Page />);

    await waitFor(() => {
      expect(updateExtrasMock).toHaveBeenCalledWith([
        expect.objectContaining({ optionId: "child-seat", optionVersion: 1, quantity: 2 }),
        expect.objectContaining({ optionId: "gps", optionVersion: 2, quantity: 1 }),
      ]);
    });
  });

  it("reconciles stored extras with the fresh catalog", async () => {
    selectedExtras = [
      { optionId: "child-seat", optionVersion: 0, code: "child_seat", name: "Old seat", description: "Old", quantity: 5, unitPrice: 5, pricingMode: "PER_RENTAL" },
      { optionId: "gps", optionVersion: 1, code: "gps", name: "Old GPS", description: "Old", quantity: 0, unitPrice: 4, pricingMode: "PER_RENTAL" },
      { optionId: "removed", optionVersion: 1, code: "removed", name: "Removed", description: "Removed", quantity: 1, unitPrice: 1, pricingMode: "PER_RENTAL" },
    ];

    render(<BookingStep3Page />);

    await waitFor(() => {
      expect(updateExtrasMock).toHaveBeenCalledWith([
        expect.objectContaining({
          optionId: "child-seat",
          optionVersion: 1,
          quantity: 2,
          unitPrice: 10,
          pricingMode: "PER_DAY",
        }),
        expect.objectContaining({
          optionId: "gps",
          optionVersion: 2,
          quantity: 1,
          unitPrice: 8,
          pricingMode: "PER_DAY",
        }),
      ]);
    });
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
    await user.type(screen.getByLabelText("Age on pickup date"), "30");
    await user.type(screen.getByLabelText("Completed licence years at pickup"), "8");
    await user.click(screen.getByLabelText(/licence will remain valid/i));
    await user.click(screen.getByLabelText(/documents at pickup/i));
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
    expect(updateCustomerDetailsMock).toHaveBeenCalledWith(expect.any(Object), expect.objectContaining({ declaration: { ageAtPickup: 30, licenseYearsAtPickup: 8, licenseValidThroughReturn: true, documentsAvailableAtPickup: true } }));
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
