import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/en.json";
import { catalogueVehicle } from "@/lib/test-fixtures/catalogue";
import VehicleDetailPage from "./page";
const state = vi.hoisted(() => ({
  search: "",
  vehicle: undefined as unknown,
  quotes: [] as unknown[],
  offices: [] as { id: string; name: string; code?: string }[],
  request: vi.fn(),
  isError: false
}));
vi.unmock("next-intl");
vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en", id: "vehicle-1" }),
  useSearchParams: () => new URLSearchParams(state.search)
}));
vi.mock("@/hooks/useVehicles", () => ({
  useVehicle: () => ({ vehicle: state.vehicle, isLoading: false, isError: state.isError }),
  useOffices: () => ({ offices: state.offices }),
  useExactAvailableVehicles: (request: unknown) => {
    state.request(request);
    return { vehicles: state.quotes, isLoading: false, isError: false };
  }
}));
beforeEach(() => {
  state.search = "";
  state.vehicle = catalogueVehicle;
  state.quotes = [];
  state.offices = [{ id: "office-1", name: "Alanya" }];
  state.isError = false;
  state.request.mockClear();
});
const draw = () =>
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <VehicleDetailPage />
    </NextIntlClientProvider>
  );
describe("Vehicle detail catalogue", () => {
  it("shows the selected vehicle's actual age and licence requirements without dates", () => {
    state.vehicle = { ...catalogueVehicle, minAge: 25, minLicenseYears: 4 };
    draw();
    expect(within(screen.getByText("Min. Age").parentElement!).getByText("25")).toBeInTheDocument();
    expect(within(screen.getByText("Minimum licence held (years)").parentElement!).getByText("4")).toBeInTheDocument();
  });
  it("selects URL offices after a cold load and preserves later user selections", () => {
    state.search = "pickup=gzp&return=ala";
    state.offices = [];
    const { rerender } = draw();
    expect(screen.getAllByRole("combobox")[0]).toHaveValue("");
    state.offices = [{ id: "office-1", name: "Alanya", code: "ala" }, { id: "office-2", name: "Gazipasa Airport", code: "gzp" }];
    const page = <NextIntlClientProvider locale="en" messages={messages}><VehicleDetailPage /></NextIntlClientProvider>;
    rerender(page);
    expect(screen.getAllByRole("combobox")[0]).toHaveValue("office-2");
    expect(screen.getAllByRole("combobox")[1]).toHaveValue("office-1");
    fireEvent.change(screen.getAllByRole("combobox")[0], { target: { value: "office-1" } });
    state.offices = [...state.offices];
    rerender(<NextIntlClientProvider locale="en" messages={messages}><VehicleDetailPage /></NextIntlClientProvider>);
    expect(screen.getAllByRole("combobox")[0]).toHaveValue("office-1");
  });
  it("does not offer a zero-price booking when the group has no configured rate", () => {
    state.search =
      "pickup=office-1&return=office-1&pickupDate=2099-01-01&pickupTime=10:00&returnDate=2099-01-02&returnTime=10:00";
    state.quotes = [{ vehicle: catalogueVehicle, finalTotal: 0, rentalDays: 1, currency: "TRY" }];
    draw();
    expect(screen.queryByRole("link", { name: "Continue booking" })).not.toBeInTheDocument();
    expect(screen.queryByText(/₺/)).not.toBeInTheDocument();
  });
  it("allows undated browsing without asking availability or inventing ratings", () => {
    draw();
    expect(screen.getByRole("heading", { name: "Fiat Egea" })).toBeInTheDocument();
    expect(state.request).toHaveBeenLastCalledWith(null);
    expect(screen.queryByText(/4.5|2025|₺/)).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Continue booking" })).not.toBeInTheDocument();
  });
  it("shows actual ordered photos and switches to the next image", () => {
    state.vehicle = {
      ...catalogueVehicle,
      photoUrls: ["/uploads/vehicles/one.png", "/uploads/vehicles/two.png"]
    };
    draw();
    expect(screen.getByRole("img")).toHaveAttribute("src", expect.stringContaining("/one.png"));
    fireEvent.click(screen.getByRole("button", { name: "Next photo" }));
    expect(screen.getByRole("img")).toHaveAttribute("src", expect.stringContaining("/two.png"));
    expect(screen.getAllByRole("button", { name: /^Photo \d/ })).toHaveLength(2);
  });
  it("rejects stale or reversed date context", () => {
    state.search =
      "pickup=office-1&return=office-1&pickupDate=2025-01-01&pickupTime=10:00&returnDate=2025-01-02&returnTime=10:00";
    draw();
    expect(state.request).toHaveBeenLastCalledWith(null);
    expect(screen.getByRole("alert")).toHaveTextContent(messages.catalogue.invalidDates);
  });
});
