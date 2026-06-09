import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import { PriceBreakdown } from "./PriceBreakdown";

describe("PriceBreakdown", () => {
  it("displays subtotal, extras, discount, and total calculations", () => {
    render(
      <PriceBreakdown
        dailyRate={50}
        days={3}
        vehicleGroup="Economy - Renault Clio"
        extras={[
          { name: "GPS", price: 15 },
          { name: "Child Seat", price: 20 },
        ]}
        campaignDiscount={10}
        currency="TRY"
      />
    );

    expect(screen.getByText("Economy - Renault Clio")).toBeInTheDocument();
    expect(screen.getByText("Rental period")).toBeInTheDocument();
    expect(screen.getByText("3 days")).toBeInTheDocument();
    expect(screen.getByText("TRY 150.00")).toBeInTheDocument();
    expect(screen.getByText("TRY 35.00")).toBeInTheDocument();
    expect(screen.getByText("-TRY 18.50")).toBeInTheDocument();
    expect(screen.getByText("TRY 166.50")).toBeInTheDocument();
  });

  it("uses a backend base amount while keeping displayed extras in the total", () => {
    render(
      <PriceBreakdown
        dailyRate={45}
        days={3}
        baseAmount={150}
        vehicleGroup="Economy"
        extras={[{ name: "GPS", price: 24 }]}
        campaignDiscountAmount={20}
        campaignCode="SUMMER15"
        currency="TRY"
      />
    );

    expect(screen.getByText("TRY 150.00")).toBeInTheDocument();
    expect(screen.getAllByText("TRY 24.00")).toHaveLength(2);
    expect(screen.getByText("-TRY 20.00")).toBeInTheDocument();
    expect(screen.getByText("TRY 154.00")).toBeInTheDocument();
    expect(screen.queryByText("TRY 139.00")).not.toBeInTheDocument();
  });

  it("omits optional sections when extras and campaign discount are absent", () => {
    render(
      <PriceBreakdown
        dailyRate={80}
        days={2}
        vehicleGroup="Premium"
        currency="USD"
      />
    );

    expect(screen.queryByText("Additional Options")).not.toBeInTheDocument();
    expect(screen.queryByText(/Campaign Discount/i)).not.toBeInTheDocument();
    expect(screen.getAllByText("$160.00")).toHaveLength(2);
  });
});
