import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import Footer from "./Footer";

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    if (key === "copyright") {
      return `copyright-${values?.year}`;
    }
    return key;
  },
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

describe("Footer", () => {
  it("renders quick links, contact details, and social links", () => {
    render(<Footer />);

    expect(screen.getByText("about.description")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "quickLinks.links.vehicles" })).toHaveAttribute("href", "/vehicles");
    expect(screen.getByRole("link", { name: "quickLinks.links.booking" })).toHaveAttribute("href", "/booking");
    expect(screen.getByRole("link", { name: "quickLinks.links.terms" })).toHaveAttribute("href", "/terms");
    expect(screen.getByRole("link", { name: "quickLinks.links.privacy" })).toHaveAttribute("href", "/privacy");
    expect(screen.getByRole("link", { name: "Instagram" })).toHaveAttribute("href", "https://instagram.com");
    expect(screen.getByRole("link", { name: "Facebook" })).toHaveAttribute("href", "https://facebook.com");
    expect(screen.getByRole("link", { name: "Twitter" })).toHaveAttribute("href", "https://twitter.com");
    expect(screen.getByRole("link", { name: "contact.email" })).toHaveAttribute("href", "mailto:contact.email");
    expect(screen.getByText(/copyright-/)).toBeInTheDocument();
  });

  it("keeps newsletter form submit local and exposes the newsletter controls", () => {
    render(<Footer />);

    const emailInput = screen.getByPlaceholderText("newsletter.placeholder");
    const submitButton = screen.getByRole("button", { name: "newsletter.button" });

    expect(emailInput).toHaveAttribute("type", "email");
    fireEvent.click(submitButton);
    expect(screen.getByText("newsletter.description")).toBeInTheDocument();
  });
});
