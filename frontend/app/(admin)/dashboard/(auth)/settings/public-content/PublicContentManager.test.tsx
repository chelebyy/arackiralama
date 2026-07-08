import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SWRConfig } from "swr";
import {
  getAdminPublicContent,
  publishAdminPublicPage,
  unpublishAdminPublicPage,
  updateAdminPublicContact,
  updateAdminPublicPageDraft,
} from "@/lib/api/admin/publicContent";
import type { AdminPublicContent } from "@/lib/api/admin/types";
import ContactContentEditor from "@/components/admin/public-content/ContactContentEditor";
import PageContentEditor from "@/components/admin/public-content/PageContentEditor";
import PublicContentPage from "./page";

const adminContentFixture: AdminPublicContent = {
  version: "1",
  updatedAt: "2026-06-27T00:00:00Z",
  pages: [
    {
      id: "page-privacy-tr",
      slug: "privacy",
      locale: "tr",
      title: "Gizlilik",
      subtitle: "Gizlilik politikası",
      seoTitle: "Gizlilik",
      seoDescription: "Gizlilik politikası",
      isPublished: true,
      sortOrder: 0,
      blocks: [
        {
          id: "privacy-body",
          heading: "Veri Kullanımı",
          body: "<p>Rezervasyon verileri.</p>",
          isVisible: true,
          sortOrder: 99,
        },
      ],
      published: null,
      draftUpdatedAtUtc: null,
      publishedAtUtc: null,
    },
  ],
  contactPageChannels: [
    {
      id: "contact-whatsapp",
      type: "whatsapp",
      label: "WhatsApp",
      value: "+90 555 000 00 00",
      href: "https://wa.me/905550000000",
      description: "Hızlı destek",
      isVisible: true,
      sortOrder: 2,
      translations: {
        en: {
          label: "WhatsApp",
          description: "Fast support",
        },
      },
    },
  ],
  contactPageOffices: [
    {
      id: "office-alanya",
      name: "Alanya Ofis",
      address: "Saray Mahallesi",
      phone: "+90 242 000 00 00",
      hours: "09:00-18:00",
      type: "main",
      isVisible: true,
      sortOrder: 1,
      translations: {
        en: {
          name: "Alanya Office",
          address: "Saray District",
        },
      },
    },
  ],
  contactPageWorkingHours: [
    {
      id: "hours-weekday",
      day: "Hafta içi",
      hours: "09:00-18:00",
      isVisible: true,
      sortOrder: 1,
      translations: {
        en: {
          day: "Weekdays",
          hours: "09:00-18:00",
        },
      },
    },
  ],
  contactPageMapTitle: "Alanya Merkez",
  contactPageMapEmbedUrl: "https://maps.example/embed",
  contactPageMapIsVisible: true,
};

vi.mock("@/lib/api/admin/publicContent", () => ({
  getAdminPublicContent: vi.fn(),
  publishAdminPublicPage: vi.fn(),
  unpublishAdminPublicPage: vi.fn(),
  updateAdminPublicContact: vi.fn(),
  updateAdminPublicPageDraft: vi.fn(),
}));

const getAdminPublicContentMock = vi.mocked(getAdminPublicContent);
const publishAdminPublicPageMock = vi.mocked(publishAdminPublicPage);
const unpublishAdminPublicPageMock = vi.mocked(unpublishAdminPublicPage);
const updateAdminPublicContactMock = vi.mocked(updateAdminPublicContact);
const updateAdminPublicPageDraftMock = vi.mocked(updateAdminPublicPageDraft);

function renderPublicContentPage(swrValue = {}) {
  return render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0, ...swrValue }}>
      <PublicContentPage />
    </SWRConfig>,
  );
}

describe("PublicContentPage", () => {
  beforeEach(() => {
    getAdminPublicContentMock.mockReset();
    publishAdminPublicPageMock.mockReset();
    unpublishAdminPublicPageMock.mockReset();
    updateAdminPublicContactMock.mockReset();
    updateAdminPublicPageDraftMock.mockReset();
    getAdminPublicContentMock.mockResolvedValue(adminContentFixture);
    publishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
    unpublishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
    updateAdminPublicContactMock.mockResolvedValue(adminContentFixture);
    updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
  });

  it("renders the public content workspace", async () => {
    renderPublicContentPage();

    expect(await screen.findByRole("heading", { name: "İçerik Yönetimi" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
    expect(screen.getByText("Dil: TR")).toBeInTheDocument();
    expect(screen.getByText(/Son kaydedilen içerik gösteriliyor/)).toBeInTheDocument();
  });

  it("keeps loaded content visible when revalidation fails", async () => {
    getAdminPublicContentMock.mockRejectedValueOnce(new Error("refresh failed"));

    renderPublicContentPage({
      fallback: {
        "admin-public-content": adminContentFixture,
      },
    });

    expect(await screen.findByRole("status")).toHaveTextContent("Son kayıtlar gösteriliyor");
    expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
  });

  it("saves a selected page draft", async () => {
    const user = userEvent.setup();
    updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
    getAdminPublicContentMock.mockResolvedValue(adminContentFixture);

    renderPublicContentPage();

    await user.click(await screen.findByRole("button", { name: /privacy/i }));
    await user.clear(screen.getByLabelText("Sayfa Başlığı"));
    await user.type(screen.getByLabelText("Sayfa Başlığı"), "Yeni Gizlilik");
    await user.click(screen.getByRole("button", { name: "Taslağı Kaydet" }));

    expect(updateAdminPublicPageDraftMock).toHaveBeenCalledWith(
      "privacy",
      "tr",
      expect.objectContaining({
        title: "Yeni Gizlilik",
        version: "1",
        isPublished: true,
        blocks: [
          expect.objectContaining({
            id: "privacy-body",
            bodyFormat: "html",
            sortOrder: 0,
          }),
        ],
      }),
    );
  });

  it("saves contact map content", async () => {
    const user = userEvent.setup();
    updateAdminPublicContactMock.mockResolvedValue(adminContentFixture);
    getAdminPublicContentMock.mockResolvedValue(adminContentFixture);

    renderPublicContentPage();

    await user.click(await screen.findByRole("tab", { name: "İletişim" }));
    expect(screen.getByText("Global iletişim")).toBeInTheDocument();
    expect(screen.getByText(/Son kaydedilen global iletişim bilgileri gösteriliyor/)).toBeInTheDocument();
    await user.clear(screen.getByLabelText("Harita Başlığı"));
    await user.type(screen.getByLabelText("Harita Başlığı"), "Alanya Ofisleri");
    expect(screen.getByText(/İletişim alanlarında kaydedilmemiş değişiklik var/)).toBeInTheDocument();
    await user.clear(screen.getByLabelText("Google Maps Embed URL"));
    await user.type(screen.getByLabelText("Google Maps Embed URL"), "https://maps.example/new");
    await user.click(screen.getByLabelText("Harita görünür"));
    await user.clear(screen.getByLabelText("Kanal 1 Etiket"));
    await user.type(screen.getByLabelText("Kanal 1 Etiket"), "WhatsApp Destek");
    await user.clear(screen.getByLabelText("Kanal 1 EN Etiket"));
    await user.type(screen.getByLabelText("Kanal 1 EN Etiket"), "WhatsApp Support");
    await user.clear(screen.getByLabelText("Ofis 1 Ad"));
    await user.type(screen.getByLabelText("Ofis 1 Ad"), "Damlataş Ofis");
    await user.clear(screen.getByLabelText("Saat 1"));
    await user.type(screen.getByLabelText("Saat 1"), "10:00-19:00");
    await user.click(screen.getByRole("button", { name: "İletişimi Kaydet" }));

    expect(updateAdminPublicContactMock).toHaveBeenCalledWith(
      expect.objectContaining({
        version: "1",
        contactPageMapTitle: "Alanya Ofisleri",
        contactPageMapEmbedUrl: "https://maps.example/new",
        contactPageMapIsVisible: false,
        contactPageChannels: [
          expect.objectContaining({
            id: "contact-whatsapp",
            label: "WhatsApp Destek",
            sortOrder: 2,
            translations: {
              en: {
                label: "WhatsApp Support",
                description: "Fast support",
              },
            },
          }),
        ],
        contactPageOffices: [
          expect.objectContaining({
            id: "office-alanya",
            name: "Damlataş Ofis",
            sortOrder: 1,
          }),
        ],
        contactPageWorkingHours: [
          expect.objectContaining({
            id: "hours-weekday",
            hours: "10:00-19:00",
            sortOrder: 1,
          }),
        ],
      }),
    );
  });

  it("keeps unsaved contact edits and base version when refreshed content arrives", async () => {
    const user = userEvent.setup();
    const refreshedContent = {
      ...adminContentFixture,
      version: "2",
      contactPageMapTitle: "Sunucu Yenilemesi",
    };
    const onContentChange = vi.fn();
    updateAdminPublicContactMock.mockResolvedValue(refreshedContent);
    const { rerender } = render(
      <ContactContentEditor content={adminContentFixture} onContentChange={onContentChange} />,
    );

    await user.clear(screen.getByLabelText("Harita Başlığı"));
    await user.type(screen.getByLabelText("Harita Başlığı"), "Kaydedilmemiş Başlık");
    rerender(<ContactContentEditor content={refreshedContent} onContentChange={onContentChange} />);

    expect(screen.getByLabelText("Harita Başlığı")).toHaveValue("Kaydedilmemiş Başlık");

    await user.click(screen.getByRole("button", { name: "İletişimi Kaydet" }));
    expect(updateAdminPublicContactMock).toHaveBeenCalledWith(
      expect.objectContaining({
        version: "1",
        contactPageMapTitle: "Kaydedilmemiş Başlık",
      }),
    );

    await user.clear(screen.getByLabelText("Harita Başlığı"));
    await user.type(screen.getByLabelText("Harita Başlığı"), "İkinci Kayıt");
    await user.click(screen.getByRole("button", { name: "İletişimi Kaydet" }));

    expect(updateAdminPublicContactMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        version: "2",
        contactPageMapTitle: "İkinci Kayıt",
      }),
    );
  });

  it("keeps unsaved page edits and base version when refreshed content arrives", async () => {
    const user = userEvent.setup();
    const refreshedContent: AdminPublicContent = {
      ...adminContentFixture,
      version: "2",
      pages: [
        {
          ...adminContentFixture.pages[0],
          title: "Sunucu Yenilemesi",
        },
      ],
    };
    const onContentChange = vi.fn();
    updateAdminPublicPageDraftMock.mockResolvedValue(refreshedContent);
    const { rerender } = render(
      <PageContentEditor content={adminContentFixture} onContentChange={onContentChange} />,
    );

    await user.clear(screen.getByLabelText("Sayfa Başlığı"));
    await user.type(screen.getByLabelText("Sayfa Başlığı"), "Kaydedilmemiş Sayfa");
    rerender(<PageContentEditor content={refreshedContent} onContentChange={onContentChange} />);

    expect(screen.getByLabelText("Sayfa Başlığı")).toHaveValue("Kaydedilmemiş Sayfa");

    await user.click(screen.getByRole("button", { name: "Taslağı Kaydet" }));
    expect(updateAdminPublicPageDraftMock).toHaveBeenCalledWith(
      "privacy",
      "tr",
      expect.objectContaining({
        version: "1",
        title: "Kaydedilmemiş Sayfa",
      }),
    );
  });

  it("prevents publishing dirty page edits before saving the draft", async () => {
    const user = userEvent.setup();
    render(<PageContentEditor content={adminContentFixture} onContentChange={vi.fn()} />);

    await user.clear(screen.getByLabelText("Sayfa Başlığı"));
    await user.type(screen.getByLabelText("Sayfa Başlığı"), "Kirli Taslak");

    expect(screen.getByText(/Bu dilde kaydedilmemiş değişiklik var/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Yayınla" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Yayından Kaldır" })).toBeDisabled();
  });

  it("creates a missing locale page draft from the selected source page", async () => {
    const user = userEvent.setup();
    const onContentChange = vi.fn();
    updateAdminPublicPageDraftMock.mockResolvedValue({
      ...adminContentFixture,
      version: "2",
      pages: [
        ...adminContentFixture.pages,
        {
          ...adminContentFixture.pages[0],
          id: "page-privacy-en",
          locale: "en",
          title: "English Privacy",
          isPublished: false,
        },
      ],
    });
    render(<PageContentEditor content={adminContentFixture} onContentChange={onContentChange} />);

    await user.click(screen.getByRole("button", { name: "EN" }));

    expect(screen.getByText(/Bu dil için yeni bir taslak hazırlanıyor/)).toBeInTheDocument();
    expect(screen.getByLabelText("Sayfa Başlığı")).toHaveValue("Gizlilik");
    expect(screen.getByRole("button", { name: "Yayınla" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Yayından Kaldır" })).toBeDisabled();

    await user.clear(screen.getByLabelText("Sayfa Başlığı"));
    await user.type(screen.getByLabelText("Sayfa Başlığı"), "English Privacy");
    await user.click(screen.getByRole("button", { name: "Taslağı Kaydet" }));

    expect(updateAdminPublicPageDraftMock).toHaveBeenCalledWith(
      "privacy",
      "en",
      expect.objectContaining({
        title: "English Privacy",
        version: "1",
      }),
    );
  });

  it("publishes and unpublishes the selected page", async () => {
    const user = userEvent.setup();

    renderPublicContentPage();

    await user.click(await screen.findByRole("button", { name: "Yayınla" }));
    expect(publishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");

    await user.click(screen.getByRole("button", { name: "Yayından Kaldır" }));
    expect(unpublishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");
  });
});
