import { describe, expect, it, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import LocaleLayout, { generateMetadata, generateStaticParams } from "./layout";

const getMessagesMock = vi.fn();
const getTranslationsMock = vi.fn();
const notFoundMock = vi.fn(() => {
  throw new Error("not-found");
});

vi.mock("next-intl", () => ({
  NextIntlClientProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock("next-intl/server", () => ({
  getMessages: () => getMessagesMock(),
  getTranslations: (...args: unknown[]) => getTranslationsMock(...args),
}));

vi.mock("next/navigation", () => ({
  notFound: () => notFoundMock(),
}));

vi.mock("@/i18n/routing", () => ({
  routing: {
    locales: ["tr", "en", "ar"],
    defaultLocale: "tr",
  },
  localeLabels: {
    tr: { dir: "ltr" },
    en: { dir: "ltr" },
    ar: { dir: "rtl" },
  },
}));

vi.mock("next/font/google", () => ({
  Lexend: () => ({ variable: "--font-lexend" }),
  Source_Sans_3: () => ({ variable: "--font-source-sans" }),
}));

vi.mock("@/lib/utils", () => ({
  cn: (...classes: Array<string | false | null | undefined>) => classes.filter(Boolean).join(" "),
}));

vi.mock("@/components/public/Header", () => ({
  default: () => <div data-testid="public-header">Header</div>,
}));

vi.mock("@/components/public/Footer", () => ({
  default: () => <div data-testid="public-footer">Footer</div>,
}));

describe("LocaleLayout", () => {
  beforeEach(() => {
    getMessagesMock.mockReset();
    getTranslationsMock.mockReset();
    notFoundMock.mockClear();
    getMessagesMock.mockResolvedValue({ common: { title: "title" } });
    getTranslationsMock.mockResolvedValue((key: string) => `translated:${key}`);
  });

  it("renders header, footer, children, and rtl direction for rtl locales", async () => {
    const ui = await LocaleLayout({
      children: <div>Locale child content</div>,
      params: Promise.resolve({ locale: "ar" }),
    });

    render(ui);

    expect(screen.getByTestId("public-header")).toBeInTheDocument();
    expect(screen.getByTestId("public-footer")).toBeInTheDocument();
    expect(screen.getByText("Locale child content")).toBeInTheDocument();
    expect(screen.getByText("Locale child content").closest("main")).toHaveClass("flex-1");
    expect(screen.getByText("Header").parentElement?.parentElement).toHaveAttribute("dir", "rtl");
  });

  it("calls notFound for unsupported locales", async () => {
    await expect(
      LocaleLayout({
        children: <div>Invalid locale</div>,
        params: Promise.resolve({ locale: "de" }),
      })
    ).rejects.toThrow("not-found");

    expect(notFoundMock).toHaveBeenCalledTimes(1);
  });

  it("builds metadata using the requested locale when valid and the default locale otherwise", async () => {
    const validMetadata = await generateMetadata({ params: Promise.resolve({ locale: "en" }) });
    const fallbackMetadata = await generateMetadata({ params: Promise.resolve({ locale: "de" }) });

    expect(getTranslationsMock).toHaveBeenNthCalledWith(1, { locale: "en", namespace: "metadata" });
    expect(getTranslationsMock).toHaveBeenNthCalledWith(2, { locale: "tr", namespace: "metadata" });
    expect(validMetadata).toEqual({
      title: "translated:title",
      description: "translated:description",
    });
    expect(fallbackMetadata).toEqual({
      title: "translated:title",
      description: "translated:description",
    });
  });

  it("returns every configured locale as a static param", () => {
    expect(generateStaticParams()).toEqual([
      { locale: "tr" },
      { locale: "en" },
      { locale: "ar" },
    ]);
  });
});
