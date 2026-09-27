import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import BookingStep2Page from "./page";
import { useBookingStore } from "@/hooks/useBooking";
import { catalogueVehicle } from "@/lib/test-fixtures/catalogue";
import type { ExactVehicleOffer } from "@/lib/api/vehicles";

const state = vi.hoisted(() => ({
  search: "", offers: [] as ExactVehicleOffer[], loading: false, error: false,
  push: vi.fn(), request: vi.fn(), mutate: vi.fn()
}));
vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en" }), useRouter: () => ({ push: state.push }),
  useSearchParams: () => new URLSearchParams(state.search)
}));
vi.mock("@/hooks/useVehicles", () => ({
  useExactAvailableVehicles: (params: unknown) => {
    state.request(params);
    return { vehicles: state.offers, isLoading: state.loading, isError: state.error, mutate: state.mutate };
  },
  useOffices: () => ({ offices: [{ id: "office-1", name: "Alanya", code: "ala" }], isLoading: false })
}));
beforeEach(() => {
  vi.clearAllMocks();
  useBookingStore.getState().clearBooking();
  state.search = "pickup=ala&return=ala&pickupDate=2099-10-10&pickupTime=01:00&returnDate=2099-10-13&returnTime=09:00";
  state.loading = false;
  state.error = false;
  state.offers = [
    { vehicle: { ...catalogueVehicle, id: "car-a", officeId: "office-1", dailyPrice: 1200 }, rentalDays: 3, finalTotal: 3600, currency: "TRY" },
    { vehicle: { ...catalogueVehicle, id: "car-b", model: "Panda", groupId: null, dailyPrice: 1000 }, rentalDays: 3, finalTotal: 3000, currency: "TRY" }
  ];
});
describe("Exact vehicle booking selection", () => {
  it("uses Turkey instants and both resolved office IDs", () => {
    render(<BookingStep2Page />);
    expect(state.request).toHaveBeenLastCalledWith({
      pickupOfficeId: "office-1", returnOfficeId: "office-1",
      pickupDateTimeUtc: "2099-10-09T22:00:00.000Z", returnDateTimeUtc: "2099-10-13T06:00:00.000Z"
    });
  });
  it("requires a concrete vehicle before continuing", () => {
    render(<BookingStep2Page />);
    expect(screen.getByRole("button", { name: /continue to payment/i })).toBeDisabled();
    expect(state.push).not.toHaveBeenCalled();
  });
  it("stores the selected actual vehicle and supports no legacy group", async () => {
    const user = userEvent.setup();
    render(<BookingStep2Page />);
    await user.click(screen.getByRole("button", { name: /Fiat Panda/i }));
    await user.click(screen.getByRole("button", { name: /continue to payment/i }));
    expect(useBookingStore.getState().vehicle).toMatchObject({
      vehicleId: "car-b", vehicleGroupId: "00000000-0000-0000-0000-000000000000", vehicleName: "Fiat Panda", dailyPrice: 1000
    });
    expect(state.push).toHaveBeenCalledWith(expect.stringContaining("preferredVehicleId=car-b"));
  });
  it("automatically advances only the requested exact available vehicle", async () => {
    state.search += "&preferredVehicleId=car-a";
    render(<BookingStep2Page />);
    await waitFor(() => expect(state.push).toHaveBeenCalledTimes(1));
    expect(useBookingStore.getState().vehicle?.vehicleId).toBe("car-a");
  });
  it("does not substitute a sibling when the requested vehicle is unavailable", () => {
    state.search += "&preferredVehicleId=missing";
    render(<BookingStep2Page />);
    expect(state.push).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: /continue to payment/i })).toBeDisabled();
    expect(screen.getByRole("alert")).toBeInTheDocument();
  });
  it("waits for availability before auto advance", () => {
    state.search += "&preferredVehicleId=car-a";
    state.loading = true;
    const { rerender } = render(<BookingStep2Page />);
    expect(state.push).not.toHaveBeenCalled();
    state.loading = false;
    rerender(<BookingStep2Page />);
    expect(state.push).toHaveBeenCalledTimes(1);
  });
  it("blocks stale selection when availability fails and supports retry", async () => {
    state.search += "&preferredVehicleId=car-a";
    state.error = true;
    const user = userEvent.setup();
    render(<BookingStep2Page />);
    expect(state.push).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: /try again/i }));
    expect(state.mutate).toHaveBeenCalled();
  });
  it("uses the server total and actual catalogue facts without group ratings", () => {
    render(<BookingStep2Page />);
    expect(screen.getByText(/3,600/)).toBeInTheDocument();
    expect(screen.queryByText(/4\.5|or similar/i)).not.toBeInTheDocument();
  });
  it("clears extras when another exact vehicle is chosen", async () => {
    useBookingStore.setState({ vehicle: { vehicleId: "car-a", vehicleGroupId: "g", vehicleName: "A", vehicleImage: "", dailyPrice: 1000, groupName: "" }, selectedExtras: [{ optionId: "old" } as never] });
    const user = userEvent.setup();
    render(<BookingStep2Page />);
    await user.click(screen.getByRole("button", { name: /Fiat Panda/i }));
    await user.click(screen.getByRole("button", { name: /continue to payment/i }));
    expect(useBookingStore.getState().selectedExtras).toEqual([]);
  });
});
