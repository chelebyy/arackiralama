import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import Hero from "./Hero";

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("./SearchForm", () => ({
  default: ({ variant }: { variant: string }) => <div data-testid="search-form">{variant}</div>,
}));

describe("Hero", () => {
  it("renders trust messaging, ctas, and the hero search form", () => {
    render(<Hero />);

    expect(screen.getByText("trustBadge")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "headline" })).toBeInTheDocument();
    expect(screen.getByText("subtitle")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ctaPrimary" })).toHaveAttribute("href", "/vehicles");
    expect(screen.getByRole("link", { name: "ctaSecondary" })).toHaveAttribute("href", "/booking");
    expect(screen.getByTestId("search-form")).toHaveTextContent("hero");
  });

  it("renders all trust feature badges from translation keys", () => {
    render(<Hero />);

    expect(screen.getByText("features.insurance")).toBeInTheDocument();
    expect(screen.getByText("features.support")).toBeInTheDocument();
    expect(screen.getByText("features.price")).toBeInTheDocument();
    expect(screen.getByText("features.delivery")).toBeInTheDocument();
  });
});
