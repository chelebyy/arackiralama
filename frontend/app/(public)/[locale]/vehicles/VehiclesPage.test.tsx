import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/en.json";
import { catalogueVehicle } from "@/lib/test-fixtures/catalogue";
import VehiclesPage from "./page";
const state = vi.hoisted(() => ({ vehicles: [] as unknown[], isLoading: false, isError: false }));
vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en" }),
  useSearchParams: () => new URLSearchParams()
}));
vi.mock("@/hooks/useVehicles", () => ({ usePublicVehicles: () => state }));
beforeEach(() => {
  state.vehicles = [catalogueVehicle];
  state.isLoading = false;
  state.isError = false;
});
const draw = () =>
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <VehiclesPage />
    </NextIntlClientProvider>
  );
describe("Vehicle catalogue page", () => {
  it("does not invent dates, prices, plate or availability", () => {
    draw();
    expect(screen.getByRole("link", { name: "View vehicle" }).getAttribute("href")).toBe(
      "/en/vehicles/vehicle-1"
    );
    expect(screen.queryByText(/2025|₺|07 ABC/)).not.toBeInTheDocument();
    expect(screen.getByText("Manual")).toBeInTheDocument();
  });
  it("paginates and resets the page when filtering", () => {
    state.vehicles = Array.from({ length: 8 }, (_, i) => ({
      ...catalogueVehicle,
      id: `id-${i}`,
      model: `Car ${i}`,
      groupId: i === 7 ? "second" : "group-1",
      groupNameEn: i === 7 ? "SUV" : "Economy"
    }));
    draw();
    expect(screen.getAllByRole("article")).toHaveLength(6);
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getAllByRole("article")).toHaveLength(2);
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "group-1" } });
    expect(screen.getAllByRole("article")).toHaveLength(6);
  });
  it("shows API failure separately", () => {
    state.isError = true;
    draw();
    expect(screen.getByRole("alert")).toBeInTheDocument();
  });
});
