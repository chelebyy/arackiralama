import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";

import messages from "@/i18n/messages/en.json";
import TermsPage from "./page";

function renderTermsPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <TermsPage />
    </NextIntlClientProvider>
  );
}

describe("TermsPage", () => {
  it("renders the terms hero and important notice", () => {
    renderTermsPage();

    expect(screen.getByRole("heading", { name: "Terms and Conditions" })).toBeInTheDocument();
    expect(screen.getByText("Please read these terms carefully before renting a vehicle")).toBeInTheDocument();
    expect(screen.getByText("Important Notice")).toBeInTheDocument();
    expect(screen.getByText(/By making a reservation or renting a vehicle from Alanya Car Rental/i)).toBeInTheDocument();
  });

  it("renders section cards and legal contact actions", () => {
    renderTermsPage();

    expect(screen.getAllByText(/Section 0[1-8]/).length).toBeGreaterThan(0);
    expect(screen.getByText("Questions About Our Terms?")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Contact Legal Team" })).toHaveAttribute("href", "mailto:legal@alanyacarrental.com");
    expect(screen.getByRole("link", { name: "Call Customer Service" })).toHaveAttribute("href", "tel:+905555550100");
    expect(screen.getByText("Last updated: March 2025")).toBeInTheDocument();
  });
});
