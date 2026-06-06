import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import { BookingStepper } from "./BookingStepper";

const usePathnameMock = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => usePathnameMock(),
}));



describe("BookingStepper", () => {
  it("marks the active step based on the current pathname", () => {
    usePathnameMock.mockReturnValue("/en/booking/step3");

    render(<BookingStepper locale="en" />);

    expect(screen.getByText("Details").className).toContain("text-sky-700");
    expect(screen.getByText("Dates").className).toContain("text-sky-700");
    expect(screen.getByText("Payment").className).toContain("text-slate-400");
  });

  it("defaults to the first step when the pathname does not include a step segment", () => {
    usePathnameMock.mockReturnValue("/en/booking");

    render(<BookingStepper locale="en" />);

    expect(screen.getByText("Dates").className).toContain("text-sky-700");
    expect(screen.getByText("Vehicle").className).toContain("text-slate-400");
  });
});
