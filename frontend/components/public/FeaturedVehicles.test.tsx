import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/en.json";
import { catalogueVehicle } from "@/lib/test-fixtures/catalogue";
import FeaturedVehicles from "./FeaturedVehicles";
const state = vi.hoisted(() => ({ vehicles: [] as unknown[], isLoading: false, isError: false }));
vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en" }),
  useSearchParams: () => new URLSearchParams("pickup=ala")
}));
vi.mock("@/hooks/useVehicles", () => ({ usePublicVehicles: () => state }));
beforeEach(() => {
  state.vehicles = [];
  state.isLoading = false;
  state.isError = false;
});
const draw = () =>
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <FeaturedVehicles />
    </NextIntlClientProvider>
  );
describe("Featured catalogue", () => {
  it("shows exactly four physical vehicles with detail links", () => {
    state.vehicles = Array.from({ length: 6 }, (_, i) => ({
      ...catalogueVehicle,
      id: `vehicle-${i}`,
      model: `Model ${i}`
    }));
    draw();
    expect(screen.getAllByRole("article")).toHaveLength(4);
    expect(screen.getAllByRole("link")[0].getAttribute("href")).toBe(
      "/en/vehicles/vehicle-0?pickup=ala"
    );
  });
  it("separates error and empty states", () => {
    state.isError = true;
    draw();
    expect(screen.getByRole("alert")).toHaveTextContent(messages.vehicles.failed);
  });
  it("shows the empty state", () => {
    draw();
    expect(screen.getByText(messages.vehicles.empty)).toBeInTheDocument();
  });
});
