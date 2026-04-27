import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/en.json";

import VehicleCard from "./VehicleCard";

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: { href: string | { pathname: string; params?: { id?: string } }; children: React.ReactNode }) => {
    const resolvedHref = typeof href === "string" ? href : `/vehicles/${href.params?.id ?? ""}`;
    return <a href={resolvedHref} {...props}>{children}</a>;
  },
}));

function renderVehicleCard(overrides: Partial<React.ComponentProps<typeof VehicleCard>> = {}) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <VehicleCard
        id="vehicle-1"
        name="Renault Clio"
        category="economy"
        image="/images/clio.jpg"
        seats={5}
        transmission="automatic"
        fuelType="gasoline"
        pricePerDay={55}
        {...overrides}
      />
    </NextIntlClientProvider>
  );
}

describe("VehicleCard", () => {
  it("renders translated vehicle details and booking CTA", () => {
    renderVehicleCard();

    expect(screen.getByRole("img", { name: "Renault Clio" })).toBeInTheDocument();
    expect(screen.getByText("Economy")).toBeInTheDocument();
    expect(screen.getByText("Free cancellation")).toBeInTheDocument();
    expect(screen.getByText("5 Seats")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Book Now" })).toHaveAttribute("href", "/vehicles/vehicle-1");
  });

  it("shows per-day and total pricing when the rental spans multiple days", () => {
    renderVehicleCard({ days: 4, totalPrice: 220 });

    expect(screen.getByText("₺ 55")).toBeInTheDocument();
    expect(screen.getByText("/per day")).toBeInTheDocument();
    expect(screen.getByText("Total: ₺ 220")).toBeInTheDocument();
  });

  it("renders the image fallback and unavailable state when no image is provided", () => {
    renderVehicleCard({ image: undefined, isAvailable: false });

    expect(screen.queryByRole("img", { name: "Renault Clio" })).not.toBeInTheDocument();
    expect(screen.getByText("Not available for these dates")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Book Now" })).not.toBeInTheDocument();
  });
});
