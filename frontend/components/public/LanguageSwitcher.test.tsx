import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import LanguageSwitcher from "./LanguageSwitcher";

const useLocaleMock = vi.fn();
const usePathnameMock = vi.fn();
let searchParams = new URLSearchParams();
let routeParams: Record<string, string> = {};

vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParams,
  useParams: () => routeParams,
}));

vi.mock("next-intl", () => ({
  useLocale: () => useLocaleMock(),
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, locale, ...props }: any) => (
    <a href={typeof href === "string" ? href : `${href.pathname}${Object.keys(href.query).length ? `?${new URLSearchParams(href.query)}` : ""}`} data-params={JSON.stringify(href.params)} data-locale={locale} {...props}>
      {children}
    </a>
  ),
  usePathname: () => usePathnameMock(),
  localeLabels: {
    en: { label: "English", flag: "GB", dir: "ltr" },
    tr: { label: "Türkçe", flag: "TR", dir: "ltr" },
  },
  routing: {
    locales: ["en", "tr"],
  },
}));

vi.mock("react-world-flags", () => ({
  default: ({ code }: { code: string }) => <span data-testid={`flag-${code}`}>{code}</span>,
}));

describe("LanguageSwitcher", () => {
  beforeEach(() => {
    useLocaleMock.mockReturnValue("en");
    usePathnameMock.mockReturnValue("/vehicles");
    searchParams = new URLSearchParams();
    routeParams = {};
  });

  it("renders the current locale and opens the language menu", () => {
    render(<LanguageSwitcher />);

    expect(screen.getByRole("button", { name: "Select Language" })).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("English")).toBeInTheDocument();
    expect(screen.getByTestId("flag-GB")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Select Language" }));

    expect(screen.getByRole("menu")).toBeInTheDocument();
    expect(screen.getAllByRole("menuitem")).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Select Language" })).toHaveAttribute("aria-expanded", "true");
  });

  it("preserves itinerary, exact vehicle and confirmation parameters when changing language", () => {
    searchParams = new URLSearchParams({ pickup: "ala", preferredVehicleId: "car-1", code: "TEST-CODE", request: "unpaid" });
    routeParams = { id: "car-1", locale: "en" };
    render(<LanguageSwitcher />);
    fireEvent.click(screen.getByRole("button", { name: "Select Language" }));
    const link = screen.getByRole("menuitem", { name: /Türkçe/i });
    expect(link).toHaveAttribute("href", `/vehicles?${searchParams}`);
    expect(link).toHaveAttribute("data-params", JSON.stringify(routeParams));
  });

  it("marks the active locale and keeps route information on menu links", () => {
    render(<LanguageSwitcher />);

    fireEvent.click(screen.getByRole("button", { name: "Select Language" }));

    const englishItem = screen.getByRole("menuitem", { name: /English/i });
    const turkishItem = screen.getByRole("menuitem", { name: /Türkçe/i });

    expect(englishItem).toHaveAttribute("aria-current", "true");
    expect(englishItem).toHaveAttribute("data-locale", "en");
    expect(turkishItem).toHaveAttribute("data-locale", "tr");
    expect(englishItem).toHaveAttribute("href", "/vehicles");
    expect(turkishItem).toHaveAttribute("href", "/vehicles");
  });
});
