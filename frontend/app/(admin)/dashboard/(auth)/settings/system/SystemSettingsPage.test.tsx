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
  usePublicSiteSettings: vi.fn(),
}));

vi.mock("@/hooks/admin", () => ({
  mutateUpdatePublicSiteSettings: (...args: unknown[]) =>
    mocks.mutateUpdatePublicSiteSettings(...args),
  usePublicSiteSettings: (...args: unknown[]) => mocks.usePublicSiteSettings(...args),
}));

vi.mock("sonner", () => ({
  toast: {
    error: (...args: unknown[]) => mocks.toastError(...args),
    success: (...args: unknown[]) => mocks.toastSuccess(...args),
  },
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
      translations: {},
    },
  ],
  heroLinks: [],
  quickLinks: [],
  socialLinks: [],
  footerBottomLinks: [],
  contactPageChannels: [],
  contactPageOffices: [],
  contactPageWorkingHours: [],
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
          sortOrder: 0,
        },
      ],
    },
  ],
  paymentMethods: {
    creditCardEnabled: false,
    debitCardEnabled: false,
    unpaidRequestEnabled: true,
    paypalEnabled: false,
    anyEnabled: true,
  },
  onlinePaymentEnabled: false,
  updatedAt: "2026-06-27T00:00:00Z",
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
      mutate: mocks.mutate,
    });
    mocks.mutateUpdatePublicSiteSettings.mockResolvedValue(baseSettings);
  });

  it("groups managed pages by slug and creates a missing locale translation from the source page", async () => {
    const user = userEvent.setup();
    render(<SystemSettingsPage />);

    expect(await screen.findByText(/privacy/)).toBeInTheDocument();
    expect(screen.getByText(/1\/5 dil hazır/)).toBeInTheDocument();
    expect(screen.getByText(/1 dil yayında/)).toBeInTheDocument();

    await user.click(screen.getByRole("tab", { name: /ENEksik/i }));
    expect(screen.getByText("English çevirisi eksik")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /TR'den Kopyala/i }));

    await waitFor(() => expect(screen.getByDisplayValue("en")).toBeInTheDocument());
    expect(screen.getAllByDisplayValue("Gizlilik Politikası").length).toBeGreaterThan(1);

    await user.click(screen.getByRole("button", { name: "Kaydet" }));

    await waitFor(() => expect(mocks.mutateUpdatePublicSiteSettings).toHaveBeenCalled());
    const payload = mocks.mutateUpdatePublicSiteSettings.mock.calls[0][0] as typeof baseSettings;
    const englishPrivacy = payload.pages.find(
      (page) => page.slug === "privacy" && page.locale === "en"
    );

    expect(englishPrivacy).toMatchObject({
      title: "Gizlilik Politikası",
      isPublished: false,
      blocks: [
        expect.objectContaining({
          heading: "Veri Kullanımı",
          body: "Türkçe gövde",
        }),
      ],
    });
  });

  it("saves locale-specific labels for public navigation links", async () => {
    const user = userEvent.setup();
    render(<SystemSettingsPage />);

    await user.click(await screen.findByRole("tab", { name: "EN" }));
    await user.type(screen.getByPlaceholderText("Bu dilde bağlantı başlığı"), "Vehicles");
    await user.click(screen.getByRole("button", { name: "Kaydet" }));

    await waitFor(() => expect(mocks.mutateUpdatePublicSiteSettings).toHaveBeenCalled());
    const payload = mocks.mutateUpdatePublicSiteSettings.mock.calls[0][0] as typeof baseSettings;

    expect(payload.headerLinks[0]).toMatchObject({
      label: "Araçlar",
      translations: {
        en: {
          label: "Vehicles",
        },
      },
    });
  });
});
