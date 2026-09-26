import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import VehicleImage from "./VehicleImage";

describe("VehicleImage", () => {
  it("replaces a broken URL with an accessible fallback and loads a different gallery image", () => {
    const { rerender } = render(<VehicleImage src="/missing.jpg" alt="Fiat" />);
    fireEvent.error(screen.getByRole("img", { name: "Fiat" }));
    expect(screen.getByRole("img", { name: "Fiat" }).tagName).toBe("DIV");
    rerender(<VehicleImage src="/working.jpg" alt="Fiat" />);
    expect(screen.getByRole("img", { name: "Fiat" })).toHaveAttribute("src", "/working.jpg");
  });
  it("shows the same fallback when no photo was uploaded", () => {
    render(<VehicleImage alt="Fiat" />);
    expect(screen.getByRole("img", { name: "Fiat" }).tagName).toBe("DIV");
  });
});
