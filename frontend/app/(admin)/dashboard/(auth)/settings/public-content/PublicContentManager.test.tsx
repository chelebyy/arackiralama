import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SWRConfig } from "swr";
import {
  getAdminPublicContent,
  publishAdminPublicPage,
  unpublishAdminPublicPage,
  updateAdminPublicPageDraft,
} from "@/lib/api/admin/publicContent";
import type { AdminPublicContent } from "@/lib/api/admin/types";
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
          bodyFormat: "html",
          isVisible: true,
          sortOrder: 0,
        },
      ],
      published: null,
      draftUpdatedAtUtc: null,
      publishedAtUtc: null,
    },
  ],
  contactPageChannels: [],
  contactPageOffices: [],
  contactPageWorkingHours: [],
  contactPageMapTitle: "",
  contactPageMapEmbedUrl: "",
  contactPageMapIsVisible: true,
};

vi.mock("@/lib/api/admin/publicContent", () => ({
  getAdminPublicContent: vi.fn(),
  publishAdminPublicPage: vi.fn(),
  unpublishAdminPublicPage: vi.fn(),
  updateAdminPublicPageDraft: vi.fn(),
}));

const getAdminPublicContentMock = vi.mocked(getAdminPublicContent);
const publishAdminPublicPageMock = vi.mocked(publishAdminPublicPage);
const unpublishAdminPublicPageMock = vi.mocked(unpublishAdminPublicPage);
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
    updateAdminPublicPageDraftMock.mockReset();
    getAdminPublicContentMock.mockResolvedValue(adminContentFixture);
    publishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
    unpublishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
    updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
  });

  it("renders the public content workspace", async () => {
    renderPublicContentPage();

    expect(await screen.findByRole("heading", { name: "İçerik Yönetimi" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
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

  it("publishes and unpublishes the selected page", async () => {
    const user = userEvent.setup();

    renderPublicContentPage();

    await user.click(await screen.findByRole("button", { name: "Yayınla" }));
    expect(publishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");

    await user.click(screen.getByRole("button", { name: "Yayından Kaldır" }));
    expect(unpublishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");
  });
});
