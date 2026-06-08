import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";

import messages from "@/i18n/messages/en.json";
import PrivacyPage from "./page";

function renderPrivacyPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <PrivacyPage />
    </NextIntlClientProvider>
  );
}

describe("PrivacyPage", () => {
  it("renders the privacy-policy hero and compliance notice", () => {
    renderPrivacyPage();

    expect(screen.getByRole("heading", { name: "Privacy Policy" })).toBeInTheDocument();
    expect(screen.getByText("How we protect and handle your personal data")).toBeInTheDocument();
    expect(screen.getByText("KVKK Compliance")).toBeInTheDocument();
    expect(screen.getByText(/Dvn rent a car is fully compliant with the Turkish Personal Data Protection Law/i)).toBeInTheDocument();
    expect(screen.getByText(/This Privacy Policy explains how Dvn rent a car collects, uses, stores/i)).toBeInTheDocument();
  });

  it("renders privacy sections and data-protection contact details", () => {
    renderPrivacyPage();

    expect(screen.getAllByText(/Section 0[1-7]/).length).toBeGreaterThan(0);
    expect(screen.getByText("Contact Our Data Protection Officer")).toBeInTheDocument();
    expect(screen.getByText("dpo@alanyacarrental.com")).toBeInTheDocument();
    expect(screen.getByText("Data Breach Notification")).toBeInTheDocument();
    expect(screen.getByText(/we will notify you and the relevant authorities within 72 hours/i)).toBeInTheDocument();
    expect(screen.getByText("Last updated: March 2025")).toBeInTheDocument();
  });
});
