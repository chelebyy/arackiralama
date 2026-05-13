import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";

import messages from "@/i18n/messages/en.json";
import ContactPage from "./page";

vi.mock("@/components/public/ContactForm", () => ({
  default: () => <div data-testid="contact-form-stub">Contact form stub</div>,
}));

function renderContactPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ContactPage />
    </NextIntlClientProvider>
  );
}

describe("ContactPage", () => {
  it("renders translated contact headings, key channels, and the contact form area", () => {
    renderContactPage();

    expect(screen.getByRole("heading", { name: "Contact Us" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Send Us a Message" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Contact Information" })).toBeInTheDocument();
    expect(screen.getAllByText("+90 242 555 10 00").length).toBeGreaterThan(0);
    expect(screen.getByText("+90 555 123 45 67")).toBeInTheDocument();
    expect(screen.getByText("info@alanyacarrental.com")).toBeInTheDocument();
    expect(screen.getByTestId("contact-form-stub")).toBeInTheDocument();
  });

  it("renders office cards and working hours from the locale content", () => {
    renderContactPage();

    expect(screen.getByRole("heading", { name: "Working Hours" })).toBeInTheDocument();
    expect(screen.getByText("Monday - Friday")).toBeInTheDocument();
    expect(screen.getAllByText("08:00 - 20:00").length).toBeGreaterThan(0);
    expect(screen.getByRole("heading", { name: "Our Locations" })).toBeInTheDocument();
    expect(screen.getByText("Main Office - Alanya City Center")).toBeInTheDocument();
    expect(screen.getByText("Gazipasa Airport Desk")).toBeInTheDocument();
    expect(screen.getByText("Antalya Airport Desk")).toBeInTheDocument();
    expect(screen.getByText("Mahmutlar Office")).toBeInTheDocument();
    expect(screen.getAllByText("Main Office").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Airport").length).toBeGreaterThan(0);
  });
});
