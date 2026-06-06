import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";

import messages from "@/i18n/messages/en.json";
import ContactPage from "./page";
import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";

vi.mock("@/lib/api/publicSiteSettings", () => ({
  getPublicSiteSettings: vi.fn(),
}));

vi.mock("swr", () => ({
  default: (_key: string, fetcher: () => unknown) => ({ data: fetcher() }),
}));

vi.mock("@/components/public/ContactForm", () => ({
  default: () => <div data-testid="contact-form-stub">Contact form stub</div>,
}));

const mockedGetPublicSiteSettings = vi.mocked(getPublicSiteSettings);

function renderContactPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ContactPage />
    </NextIntlClientProvider>
  );
}

describe("ContactPage", () => {
  beforeEach(() => {
    mockedGetPublicSiteSettings.mockReturnValue(undefined as never);
  });

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

  it("renders managed contact-page settings from the admin API", () => {
    mockedGetPublicSiteSettings.mockReturnValue({
      companyName: "Managed Rent",
      companyAddress: "Managed address",
      companyPhone: "+90 555",
      companyEmail: "managed@example.test",
      workingHours: "09:00 - 18:00",
      headerLinks: [],
      heroLinks: [],
      quickLinks: [],
      socialLinks: [],
      footerBottomLinks: [],
      contactPageChannels: [
        {
          id: "managed-phone",
          type: "phone",
          label: "Managed Reservations",
          value: "+90 555 000 00 00",
          href: "tel:+905550000000",
          description: "Managed phone description",
          isVisible: true,
          sortOrder: 0,
        },
        {
          id: "hidden-email",
          type: "email",
          label: "Hidden Email",
          value: "hidden@example.test",
          href: "mailto:hidden@example.test",
          description: "",
          isVisible: false,
          sortOrder: 1,
        },
      ],
      contactPageOffices: [
        {
          id: "managed-office",
          name: "Managed Office",
          address: "Managed office address",
          phone: "+90 555 111 11 11",
          hours: "10:00 - 17:00",
          type: "branch",
          isVisible: true,
          sortOrder: 0,
        },
      ],
      contactPageWorkingHours: [
        {
          id: "managed-hours",
          day: "Managed Day",
          hours: "11:00 - 16:00",
          isVisible: true,
          sortOrder: 0,
        },
      ],
      contactPageMapTitle: "Managed Map",
      contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=managed",
      contactPageMapIsVisible: true,
      pages: [],
      updatedAt: "2026-06-06T00:00:00Z",
    } as never);

    renderContactPage();

    expect(screen.getByText("Managed Reservations")).toBeInTheDocument();
    expect(screen.getByText("+90 555 000 00 00")).toBeInTheDocument();
    expect(screen.queryByText("Hidden Email")).not.toBeInTheDocument();
    expect(screen.getByText("Managed Office")).toBeInTheDocument();
    expect(screen.getByText("Managed Day")).toBeInTheDocument();
    expect(screen.getByText("11:00 - 16:00")).toBeInTheDocument();
    expect(screen.getByTitle("Managed Map")).toHaveAttribute("src", "https://www.google.com/maps/embed?pb=managed");
  });

  it("hides the map when managed settings disable it", () => {
    mockedGetPublicSiteSettings.mockReturnValue({
      companyName: "Managed Rent",
      companyAddress: "Managed address",
      companyPhone: "+90 555",
      companyEmail: "managed@example.test",
      workingHours: "09:00 - 18:00",
      headerLinks: [],
      heroLinks: [],
      quickLinks: [],
      socialLinks: [],
      footerBottomLinks: [],
      contactPageChannels: [],
      contactPageOffices: [],
      contactPageWorkingHours: [],
      contactPageMapTitle: "Hidden Map",
      contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=hidden",
      contactPageMapIsVisible: false,
      pages: [],
      updatedAt: "2026-06-06T00:00:00Z",
    } as never);

    renderContactPage();

    expect(screen.queryByTitle("Hidden Map")).not.toBeInTheDocument();
  });
});
