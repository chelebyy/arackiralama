import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import HomePage from "./page";

vi.mock("next-intl", () => ({
  useTranslations: (namespace?: string) => {
    if (namespace === "home") {
      const t = ((key: string) => key) as ((key: string) => string) & { raw: (key: string) => unknown };
      t.raw = (key: string) =>
        key === "faq.questions"
          ? [
              { id: "faq-1", question: "What is included?", answer: "Insurance and support." },
              { id: "faq-2", question: "Where can I pick up?", answer: "Airport or city center." },
            ]
          : [];
      return t;
    }

    const t = ((key: string) => key) as ((key: string) => string) & { raw?: (key: string) => unknown };
    return t;
  },
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("@/components/public/Hero", () => ({
  default: () => <div data-testid="hero-component">Hero section</div>,
}));

vi.mock("@/components/public/VehicleCard", () => ({
  default: ({ name }: { name: string }) => <div data-testid="vehicle-card">{name}</div>,
}));

describe("HomePage", () => {
  it("renders hero, featured vehicles, and action links", () => {
    render(<HomePage />);

    expect(screen.getByTestId("hero-component")).toBeInTheDocument();
    expect(screen.getAllByTestId("vehicle-card")).toHaveLength(4);
    expect(screen.getByText("Renault Clio")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "viewAllVehicles" })).toHaveAttribute("href", "/vehicles");
    expect(screen.getByRole("link", { name: "cta.contactUs" })).toHaveAttribute("href", "/contact");
  });

  it("renders why-choose-us content and faq entries from translations", () => {
    render(<HomePage />);

    expect(screen.getByText("whyChooseUs.title")).toBeInTheDocument();
    expect(screen.getByText("whyChooseUs.insurance.title")).toBeInTheDocument();
    expect(screen.getByText("faq.title")).toBeInTheDocument();
    expect(screen.getByText("What is included?")).toBeInTheDocument();
    expect(screen.getByText("Airport or city center.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "faq.contactSupport" })).toHaveAttribute("href", "/contact");
  });
});
