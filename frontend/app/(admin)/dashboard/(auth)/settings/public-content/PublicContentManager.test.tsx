import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SWRConfig } from "swr";
import { getAdminPublicContent } from "@/lib/api/admin/publicContent";
import PublicContentPage from "./page";

const adminContentFixture = {
  version: "1",
  updatedAt: "2026-06-27T00:00:00Z",
  pages: [],
  contactPageChannels: [],
  contactPageOffices: [],
  contactPageWorkingHours: [],
  contactPageMapTitle: "",
  contactPageMapEmbedUrl: "",
  contactPageMapIsVisible: true,
};

vi.mock("@/lib/api/admin/publicContent", () => ({
  getAdminPublicContent: vi.fn(),
}));

const getAdminPublicContentMock = vi.mocked(getAdminPublicContent);

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
    getAdminPublicContentMock.mockResolvedValue(adminContentFixture);
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
});
