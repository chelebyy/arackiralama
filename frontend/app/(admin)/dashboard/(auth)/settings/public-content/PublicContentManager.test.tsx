import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import PublicContentPage from "./page";

vi.mock("@/lib/api/admin/publicContent", () => ({
  getAdminPublicContent: vi.fn().mockResolvedValue({
    version: "1",
    updatedAt: "2026-06-27T00:00:00Z",
    pages: [],
    contactPageChannels: [],
    contactPageOffices: [],
    contactPageWorkingHours: [],
    contactPageMapTitle: "",
    contactPageMapEmbedUrl: "",
    contactPageMapIsVisible: true,
  }),
}));

describe("PublicContentPage", () => {
  it("renders the public content workspace", async () => {
    render(<PublicContentPage />);

    expect(await screen.findByRole("heading", { name: "İçerik Yönetimi" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
  });
});
