import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import Footer from "./Footer";

vi.mock("next-intl", () => ({
  useTranslations: () => {
    const t = (key: string, values?: Record<string, unknown>) => {
      if (key === "copyright") {
        return `copyright-${values?.year}-${values?.companyName}`;
      }
      return key;
    };
    t.has = (key: string) =>
      [
        "quickLinks.links.vehicles",
        "quickLinks.links.howItWorks",
        "quickLinks.links.contact",
        "quickLinks.links.track",
        "quickLinks.links.booking",
        "quickLinks.links.terms",
        "quickLinks.links.privacy",
      ].includes(key);
    return t;
  },
}));

const useSWRMock = vi.fn();

vi.mock("swr", () => ({
  default: (...args: unknown[]) => useSWRMock(...args),
}));

vi.mock("@/lib/api/publicSiteSettings", () => ({
  getPublicSiteSettings: vi.fn(),
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

describe("Footer", () => {
  beforeEach(() => {
    useSWRMock.mockReset();
    useSWRMock.mockReturnValue({ data: undefined });
  });

  it("renders quick links, contact details, and social links", () => {
    render(<Footer />);

    expect(screen.getByText("about.description")).toBeInTheDocument();
    expect(screen.getByText("Dvn rent a car")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "quickLinks.links.vehicles" })).toHaveAttribute("href", "/vehicles");
    expect(screen.getByRole("link", { name: "quickLinks.links.booking" })).toHaveAttribute("href", "/booking");
    expect(screen.getByRole("link", { name: "quickLinks.links.terms" })).toHaveAttribute("href", "/terms");
    expect(screen.getByRole("link", { name: "quickLinks.links.privacy" })).toHaveAttribute("href", "/privacy");
    expect(screen.getByRole("link", { name: "Instagram" })).toHaveAttribute("href", "https://instagram.com");
    expect(screen.getByRole("link", { name: "Facebook" })).toHaveAttribute("href", "https://facebook.com");
    expect(screen.getByRole("link", { name: "Twitter" })).toHaveAttribute("href", "https://twitter.com");
    expect(screen.getByRole("link", { name: "contact.email" })).toHaveAttribute("href", "mailto:contact.email");
    expect(screen.getByText(/copyright-/)).toBeInTheDocument();
  });

  it("uses managed public site settings when available", () => {
    useSWRMock.mockReturnValue({
      data: {
        companyName: "Managed Rent",
        companyAddress: "Managed address",
        companyPhone: "+90 555 000 00 00",
        companyEmail: "managed@example.test",
        workingHours: "09:00 - 18:00",
        headerLinks: [],
        heroLinks: [],
        quickLinks: [
          { id: "visible", label: "Managed Link", href: "/managed", isVisible: true, sortOrder: 0 },
          { id: "hidden", label: "Hidden Link", href: "/hidden", isVisible: false, sortOrder: 1 },
          { id: "terms", label: "Kullanım Koşulları", href: "/terms", isVisible: true, sortOrder: 2 },
        ],
        socialLinks: [
          { id: "instagram", platform: "Instagram", url: "https://instagram.com/managed", isVisible: true, sortOrder: 0 },
          { id: "facebook", platform: "Facebook", url: "https://facebook.com/hidden", isVisible: false, sortOrder: 1 },
        ],
        footerBottomLinks: [
          { id: "bottom", label: "Bottom Link", href: "/bottom", isVisible: true, sortOrder: 0 },
        ],
        contactPageChannels: [],
        contactPageOffices: [],
        contactPageWorkingHours: [],
        contactPageMapTitle: "Map",
        contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=managed",
        contactPageMapIsVisible: true,
        pages: [
          {
            id: "tr-managed",
            slug: "managed",
            locale: "tr",
            title: "Managed",
            subtitle: "",
            seoTitle: "",
            seoDescription: "",
            isPublished: true,
            sortOrder: 0,
            blocks: [],
          },
          {
            id: "tr-bottom",
            slug: "bottom",
            locale: "tr",
            title: "Bottom",
            subtitle: "",
            seoTitle: "",
            seoDescription: "",
            isPublished: true,
            sortOrder: 1,
            blocks: [],
          },
          {
            id: "tr-terms",
            slug: "terms",
            locale: "tr",
            title: "Terms",
            subtitle: "",
            seoTitle: "",
            seoDescription: "",
            isPublished: false,
            sortOrder: 2,
            blocks: [],
          },
        ],
        updatedAt: "2026-06-06T00:00:00Z",
      },
    });

    render(<Footer />);

    expect(screen.getByText("Managed Rent")).toBeInTheDocument();
    expect(screen.getByText(/copyright-.*-Managed Rent/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Managed Link" })).toHaveAttribute("href", "/managed");
    expect(screen.queryByText("Hidden Link")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Instagram" })).toHaveAttribute("href", "https://instagram.com/managed");
    expect(screen.queryByRole("link", { name: "Facebook" })).not.toBeInTheDocument();
    expect(screen.getByText("Managed address")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "managed@example.test" })).toHaveAttribute("href", "mailto:managed@example.test");
    expect(screen.getByRole("link", { name: "Bottom Link" })).toHaveAttribute("href", "/bottom");
    expect(screen.queryByRole("link", { name: "Kullanım Koşulları" })).not.toBeInTheDocument();
  });

  it("keeps newsletter form submit local and exposes the newsletter controls", () => {
    render(<Footer />);

    const emailInput = screen.getByPlaceholderText("newsletter.placeholder");
    const submitButton = screen.getByRole("button", { name: "newsletter.button" });

    expect(emailInput).toHaveAttribute("type", "email");
    fireEvent.click(submitButton);
    expect(screen.getByText("newsletter.description")).toBeInTheDocument();
  });
});
