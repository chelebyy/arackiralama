import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import BookingLayout from "./layout";

const bookingStepperMock = vi.fn(({ locale }: { locale: string }) => (
  <div data-testid="booking-stepper">stepper-{locale}</div>
));

vi.mock("@/components/public/BookingStepper", () => ({
  BookingStepper: (props: { locale: string }) => bookingStepperMock(props),
}));

describe("BookingLayout", () => {
  it("renders the booking stepper with the resolved locale and wraps children in the booking shell", async () => {
    const ui = await BookingLayout({
      children: <div>Booking child content</div>,
      params: Promise.resolve({ locale: "en" }),
    });

    render(ui);

    expect(bookingStepperMock).toHaveBeenCalledWith({ locale: "en" });
    expect(screen.getByTestId("booking-stepper")).toHaveTextContent("stepper-en");
    expect(screen.getByText("Booking child content")).toBeInTheDocument();
    expect(screen.getByRole("main").className).toContain("max-w-6xl");
  });
});
