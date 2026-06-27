import userEvent from "@testing-library/user-event";
import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import SystemSettingsPage from "./page";

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

const mocks = vi.hoisted(() => ({
  mutate: vi.fn(),
  mutateUpdatePublicSiteSettings: vi.fn(),
  toastError: vi.fn(),
  toastSuccess: vi.fn(),
  usePublicSiteSettings: vi.fn()
}));

vi.mock("@/hooks/admin", () => ({
  mutateUpdatePublicSiteSettings: (...args: unknown[]) =>
    mocks.mutateUpdatePublicSiteSettings(...args),
  usePublicSiteSettings: (...args: unknown[]) => mocks.usePublicSiteSettings(...args)
}));

vi.mock("sonner", () => ({
  toast: {
    error: (...args: unknown[]) => mocks.toastError(...args),
    success: (...args: unknown[]) => mocks.toastSuccess(...args)
  }
}));

const baseSettings = {
  companyName: "Dvn rent a car",
  companyAddress: "Alanya",
  companyPhone: "+90 242 000 00 00",
  companyEmail: "info@example.test",
  workingHours: "08:00 - 20:00",
  headerLinks: [
    {
      id: "vehicles",
      label: "Araçlar",
      href: "/vehicles",
      isVisible: true,
      sortOrder: 0,
      translations: {}
    }
  ],
  heroLinks: [],
  quickLinks: [],
  socialLinks: [],
  footerBottomLinks: [],
  contactPageChannels: [
    {
      id: "phone-main",
      type: "phone",
      label: "Telefon",
      value: "+90 242 000 00 00",
      href: "tel:+902420000000",
      description: "Merkez ofis",
      isVisible: true,
      sortOrder: 0,
      translations: {
        en: {
          label: "Phone",
          value: "+90 242 000 00 00",
          description: "Main office"
        }
      }
    }
  ],
  contactPageOffices: [
    {
      id: "office-main",
      name: "Alanya Merkez",
      address: "Ataturk Cd. No:1",
      phone: "+90 242 000 00 00",
      hours: "08:00 - 20:00",
      type: "main",
      isVisible: true,
      sortOrder: 0,
      translations: {
        en: {
          name: "Alanya Main Office",
          address: "Ataturk St. No:1",
          hours: "08:00 - 20:00"
        }
      }
    }
  ],
  contactPageWorkingHours: [
    {
      id: "weekday",
      day: "Hafta ici",
      hours: "08:00 - 20:00",
      isVisible: true,
      sortOrder: 0,
      translations: {
        en: {
          day: "Weekdays",
          hours: "08:00 - 20:00"
        }
      }
    }
  ],
  contactPageMapTitle: "Office Map",
  contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=test",
  contactPageMapIsVisible: true,
  pages: [
    {
      id: "tr-privacy",
      slug: "privacy",
      locale: "tr",
      title: "Gizlilik Politikası",
      subtitle: "Veri koruma notları",
      seoTitle: "Gizlilik Politikası",
      seoDescription: "Kişisel veri politikası",
      isPublished: true,
      sortOrder: 0,
      blocks: [
        {
          id: "privacy-data",
          heading: "Veri Kullanımı",
          body: "Türkçe gövde",
          isVisible: true,
          sortOrder: 0
        }
      ]
    }
  ],
  paymentMethods: {
    creditCardEnabled: false,
    debitCardEnabled: false,
    unpaidRequestEnabled: true,
    paypalEnabled: false,
    anyEnabled: true
  },
  onlinePaymentEnabled: false,
  updatedAt: "2026-06-27T00:00:00Z"
};

describe("SystemSettingsPage", () => {
  beforeEach(() => {
    vi.stubGlobal("ResizeObserver", ResizeObserverMock);
    mocks.mutate.mockReset();
    mocks.mutateUpdatePublicSiteSettings.mockReset();
    mocks.toastError.mockReset();
    mocks.toastSuccess.mockReset();
    mocks.usePublicSiteSettings.mockReset();
    mocks.usePublicSiteSettings.mockReturnValue({
      settings: baseSettings,
      isLoading: false,
      isError: false,
      mutate: mocks.mutate
    });
    mocks.mutateUpdatePublicSiteSettings.mockResolvedValue(baseSettings);
  });

  it("saves locale-specific labels for public navigation links", async () => {
    const user = userEvent.setup();
    render(<SystemSettingsPage />);

    expect(await screen.findByText("Public Site Ayarları")).toBeInTheDocument();
    expect(screen.queryByText("Sayfalar")).not.toBeInTheDocument();
    expect(screen.queryByText("İletişim Sayfası Kanalları")).not.toBeInTheDocument();
    expect(screen.queryByText("İletişim Sayfası Ofisleri")).not.toBeInTheDocument();
    expect(screen.queryByText("İletişim Sayfası Çalışma Saatleri")).not.toBeInTheDocument();
    expect(screen.queryByText("İletişim Sayfası Haritası")).not.toBeInTheDocument();

    await user.click(await screen.findByRole("tab", { name: "EN" }));
    await user.type(screen.getByPlaceholderText("Bu dilde bağlantı başlığı"), "Vehicles");
    await user.click(screen.getByRole("button", { name: "Kaydet" }));

    await waitFor(() => expect(mocks.mutateUpdatePublicSiteSettings).toHaveBeenCalled());
    const payload = mocks.mutateUpdatePublicSiteSettings.mock.calls[0][0] as typeof baseSettings;

    expect(payload.headerLinks[0]).toMatchObject({
      label: "Araçlar",
      translations: {
        en: {
          label: "Vehicles"
        }
      }
    });
    expect(payload.pages).toEqual(baseSettings.pages);
    expect(payload.contactPageChannels).toEqual(baseSettings.contactPageChannels);
    expect(payload.contactPageOffices).toEqual(baseSettings.contactPageOffices);
    expect(payload.contactPageWorkingHours).toEqual(baseSettings.contactPageWorkingHours);
    expect(payload.contactPageMapTitle).toBe(baseSettings.contactPageMapTitle);
    expect(payload.contactPageMapEmbedUrl).toBe(baseSettings.contactPageMapEmbedUrl);
    expect(payload.contactPageMapIsVisible).toBe(baseSettings.contactPageMapIsVisible);
  });
});
