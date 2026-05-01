import { describe, expect, it } from "vitest";
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
