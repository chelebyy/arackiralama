import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import LanguageSwitcher from "./LanguageSwitcher";

const useLocaleMock = vi.fn();
const usePathnameMock = vi.fn();

vi.mock("next-intl", () => ({
  useLocale: () => useLocaleMock(),
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, locale, ...props }: any) => (
    <a href={href} data-locale={locale} {...props}>
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
