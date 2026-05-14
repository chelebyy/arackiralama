import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import Header from "./Header";

const usePathnameMock = vi.fn();
const useLocaleMock = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
  useLocale: () => useLocaleMock(),
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
  usePathname: () => usePathnameMock(),
  localeLabels: {
    en: { dir: "ltr" },
    ar: { dir: "rtl" },
  },
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("./LanguageSwitcher", () => ({
  default: () => <div data-testid="language-switcher">language-switcher</div>,
}));

describe("Header", () => {
  beforeEach(() => {
    usePathnameMock.mockReturnValue("/");
    useLocaleMock.mockReturnValue("en");
  });

  it("renders desktop navigation, login, tracking cta, and language switcher", () => {
    render(<Header />);

    expect(screen.getByTestId("language-switcher")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "home" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "vehicles" })).toHaveAttribute("href", "/vehicles");
    expect(screen.getByRole("link", { name: "about" })).toHaveAttribute("href", "/about");
    expect(screen.getByRole("link", { name: "contact" })).toHaveAttribute("href", "/contact");
    expect(screen.getByRole("link", { name: "login" })).toHaveAttribute("href", "/dashboard/login/v2");
    expect(screen.getByRole("link", { name: "trackReservation" })).toHaveAttribute("href", "/track-reservation");
  });

  it("toggles the mobile menu and reveals mobile navigation actions", () => {
    render(<Header />);

    fireEvent.click(screen.getByRole("button", { name: "Toggle menu" }));

    expect(screen.getAllByRole("link", { name: "trackReservation" }).length).toBeGreaterThan(1);
    expect(screen.getAllByRole("link", { name: "login" }).length).toBeGreaterThan(1);
    expect(screen.getByRole("button", { name: "Toggle menu" })).toBeInTheDocument();
  });
});
