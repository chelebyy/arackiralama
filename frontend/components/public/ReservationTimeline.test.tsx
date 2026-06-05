import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";

import messages from "../../i18n/messages/en.json";
import ReservationTimeline from "./ReservationTimeline";

function renderTimeline(status: Parameters<typeof ReservationTimeline>[0]["status"]) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ReservationTimeline status={status} />
    </NextIntlClientProvider>
  );
}

describe("ReservationTimeline", () => {
  it("renders active progress with the current status message", () => {
    renderTimeline("confirmed");

    expect(screen.getByRole("heading", { name: "Reservation Progress" })).toBeInTheDocument();
    expect(screen.getByText("Reservation Confirmed")).toBeInTheDocument();
    expect(screen.getByText("Your booking is confirmed and vehicle is reserved")).toBeInTheDocument();
    expect(screen.getByText("Your reservation is confirmed. We will contact you before pickup.")).toBeInTheDocument();
  });

  it("renders the cancelled branch with cancellation messaging", () => {
    renderTimeline("cancelled");

    expect(screen.getByText("Reservation Cancelled")).toBeInTheDocument();
    expect(screen.getByText("This reservation has been cancelled")).toBeInTheDocument();
    expect(screen.getByText("This reservation has been cancelled. Contact support for more information.")).toBeInTheDocument();
    expect(screen.queryByText("Vehicle Picked Up")).not.toBeInTheDocument();
  });
});
