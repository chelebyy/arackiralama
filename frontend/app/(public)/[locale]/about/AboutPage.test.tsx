import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";

import messages from "@/i18n/messages/en.json";
import AboutPage from "./page";

function renderAboutPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <AboutPage />
    </NextIntlClientProvider>
  );
}

describe("AboutPage", () => {
  it("renders the translated hero and story content", () => {
    renderAboutPage();

    expect(screen.getByRole("heading", { name: "About Dvn rent a car" })).toBeInTheDocument();
    expect(screen.getByText("Your trusted partner for car rentals in Alanya since 2008")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Our Story" })).toBeInTheDocument();
    expect(screen.getByText(/Founded in 2008, Dvn rent a car began with a simple mission/i)).toBeInTheDocument();
    expect(screen.getByText(/Over the past 15 years, we have served over 50,000 customers/i)).toBeInTheDocument();
  });

  it("renders key stats, value cards, and coverage areas", () => {
    renderAboutPage();

    expect(screen.getByText("15+")).toBeInTheDocument();
    expect(screen.getByText("500+")).toBeInTheDocument();
    expect(screen.getByText("50K+")).toBeInTheDocument();
    expect(screen.getAllByText("24/7").length).toBeGreaterThan(0);
    expect(screen.getByRole("heading", { name: "Why Choose Us" })).toBeInTheDocument();
    expect(screen.getByText("Comprehensive Insurance")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Our Values" })).toBeInTheDocument();
    expect(screen.getByText("Trust & Transparency")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Our Fleet" })).toBeInTheDocument();
    expect(screen.getByText("Economy")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Coverage Areas" })).toBeInTheDocument();
    expect(screen.getByText("Gazipasa Airport (GZP)")).toBeInTheDocument();
    expect(screen.getAllByText("Free Delivery").length).toBeGreaterThan(0);
  });
});
