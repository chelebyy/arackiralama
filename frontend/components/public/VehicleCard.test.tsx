import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/en.json";
import { catalogueVehicle } from "@/lib/test-fixtures/catalogue";
import VehicleCard from "./VehicleCard";

describe("VehicleCard catalogue", () => {
  it("uses real specifications and forwards only search context to detail", () => {
    render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <VehicleCard
          vehicle={catalogueVehicle}
          locale="en"
          search={new URLSearchParams("pickup=ala&pickupDate=2030-01-01&dailyPrice=1&token=secret")}
        />
      </NextIntlClientProvider>
    );
    expect(screen.getByText("Manual")).toBeInTheDocument();
    expect(screen.getByText("Diesel")).toBeInTheDocument();
    expect(screen.getByText("4")).toBeInTheDocument();
    const href = screen.getByRole("link", { name: "View vehicle" }).getAttribute("href")!;
    expect(href).toContain("/en/vehicles/vehicle-1");
    expect(href).toContain("pickupDate=2030-01-01");
    expect(href).not.toMatch(/dailyPrice|token|booking/);
    expect(screen.queryByText(/₺|Free cancellation|200 km/)).not.toBeInTheDocument();
  });
  it("keeps unknown fields unknown and does not invent a price", () => {
    render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <VehicleCard
          vehicle={{
            ...catalogueVehicle,
            transmission: null,
            fuelType: null,
            seatCount: null,
            luggageCapacity: null
          }}
          locale="en"
        />
      </NextIntlClientProvider>
    );
    expect(screen.getAllByText("Not specified")).toHaveLength(4);
    expect(screen.getByRole("link").getAttribute("href")).toBe("/en/vehicles/vehicle-1");
  });
});
