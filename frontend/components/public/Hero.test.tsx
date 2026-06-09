import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import Hero from "./Hero";

const useSWRMock = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => {
    const t = (key: string) => key;
    t.has = (key: string) =>
      ["trustBadge", "headline", "subtitle", "ctaPrimary", "features.insurance", "features.support", "features.price", "features.delivery"].includes(key);
    return t;
  },
}));

vi.mock("swr", () => ({
  default: (...args: unknown[]) => useSWRMock(...args),
}));

vi.mock("@/lib/api/publicSiteSettings", () => ({
  getPublicSiteSettings: vi.fn(),
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("./SearchForm", () => ({
  default: ({ variant }: { variant: string }) => <div data-testid="search-form">{variant}</div>,
}));

describe("Hero", () => {
  beforeEach(() => {
    useSWRMock.mockReset();
    useSWRMock.mockReturnValue({ data: undefined });
  });

  it("renders trust messaging, a browse CTA, and the hero search form", () => {
    render(<Hero />);

    expect(screen.getByText("trustBadge")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "headline" })).toBeInTheDocument();
    expect(screen.getByText("subtitle")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ctaPrimary" })).toHaveAttribute("href", "/vehicles");
    expect(screen.queryByRole("link", { name: "ctaSecondary" })).not.toBeInTheDocument();
    expect(screen.getByTestId("search-form")).toHaveTextContent("hero");
  });

  it("renders all trust feature badges from translation keys", () => {
    render(<Hero />);

    expect(screen.getByText("features.insurance")).toBeInTheDocument();
    expect(screen.getByText("features.support")).toBeInTheDocument();
    expect(screen.getByText("features.price")).toBeInTheDocument();
    expect(screen.getByText("features.delivery")).toBeInTheDocument();
  });

  it("uses managed primary CTA settings and ignores the duplicated booking CTA", () => {
    useSWRMock.mockReturnValue({
      data: {
        heroLinks: [
          { id: "ctaPrimary", label: "Managed Fleet", href: "/managed-fleet", isVisible: true, sortOrder: 0 },
          { id: "ctaSecondary", label: "Hidden Booking", href: "/booking", isVisible: true, sortOrder: 1 },
        ],
      },
    });

    render(<Hero />);

    expect(screen.getByRole("link", { name: "ctaPrimary" })).toHaveAttribute("href", "/managed-fleet");
    expect(screen.queryByText("Hidden Booking")).not.toBeInTheDocument();
  });
});
