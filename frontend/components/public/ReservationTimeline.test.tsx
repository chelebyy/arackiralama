import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import ReservationTimeline from "./ReservationTimeline";

describe("ReservationTimeline", () => {
  it("renders active progress with the current status message", () => {
    render(<ReservationTimeline status="confirmed" />);

    expect(screen.getByRole("heading", { name: "Reservation Progress" })).toBeInTheDocument();
    expect(screen.getByText("Reservation Confirmed")).toBeInTheDocument();
    expect(screen.getByText("Your booking is confirmed and vehicle is reserved")).toBeInTheDocument();
    expect(screen.getByText("Your reservation is confirmed. We will contact you before pickup.")).toBeInTheDocument();
  });

  it("renders the cancelled branch with cancellation messaging", () => {
    render(<ReservationTimeline status="cancelled" />);

    expect(screen.getByText("Reservation Cancelled")).toBeInTheDocument();
    expect(screen.getByText("This reservation has been cancelled")).toBeInTheDocument();
    expect(screen.getByText("This reservation has been cancelled. Contact support for more information.")).toBeInTheDocument();
    expect(screen.queryByText("Vehicle Picked Up")).not.toBeInTheDocument();
  });
});
