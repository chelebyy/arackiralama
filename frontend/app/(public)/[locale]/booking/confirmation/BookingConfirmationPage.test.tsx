import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import BookingConfirmationPage from "./page";

let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParams,
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe("BookingConfirmationPage", () => {
  beforeEach(() => {
    searchParams = new URLSearchParams();
  });

  it("renders the reservation summary when confirmation details are present", () => {
    searchParams = new URLSearchParams({
      code: "ALN-2026-1001",
      vehicle: "Renault Clio",
      pickup: "2026-06-10 09:30",
      return: "2026-06-14 11:00",
      total: "₺3200",
    });

    render(<BookingConfirmationPage />);

    expect(screen.getByText("booking.confirmation.title")).toBeInTheDocument();
    expect(screen.getByText("booking.payment.summary.title")).toBeInTheDocument();
    expect(screen.getByText("ALN-2026-1001")).toBeInTheDocument();
    expect(screen.getByText("Renault Clio")).toBeInTheDocument();
    expect(screen.getByText("2026-06-10 09:30")).toBeInTheDocument();
    expect(screen.getByText("2026-06-14 11:00")).toBeInTheDocument();
    expect(screen.getByText("₺3200")).toBeInTheDocument();
    expect(screen.queryByText("booking.confirmation.instructions")).not.toBeInTheDocument();
  });

  it("shows fallback instructions when no confirmation details are available", () => {
    render(<BookingConfirmationPage />);

    expect(screen.getByText("booking.confirmation.instructions")).toBeInTheDocument();
    expect(screen.queryByText("booking.payment.summary.title")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "navigation.home" })).toHaveAttribute("href", "/");
  });
});
