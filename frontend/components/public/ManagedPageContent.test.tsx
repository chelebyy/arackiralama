import { render, screen, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";
import ManagedPageContent from "./ManagedPageContent";

vi.mock("@/lib/api/publicSiteSettings", () => ({
  getPublicSiteSettings: vi.fn(),
}));

const mockedGetPublicSiteSettings = vi.mocked(getPublicSiteSettings);

function renderManagedPage(slug = "privacy", withChildren = true) {
  return render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <ManagedPageContent slug={slug}>
        {withChildren ? <div>Static English Privacy</div> : undefined}
      </ManagedPageContent>
    </SWRConfig>
  );
}

function managedPage(locale: string, title: string, slug = "privacy", isPublished = true) {
  return {
    id: `${locale}-${slug}`,
    slug,
    locale,
    title,
    subtitle: "Managed subtitle",
    seoTitle: title,
    seoDescription: "Managed description",
    isPublished,
    sortOrder: 0,
    blocks: [
      {
        id: `${locale}-block`,
        heading: "Managed section",
        body: "Managed body",
        isVisible: true,
        sortOrder: 0,
      },
    ],
  };
}

describe("ManagedPageContent", () => {
  beforeEach(() => {
    mockedGetPublicSiteSettings.mockReset();
    window.history.pushState({}, "", "/en/privacy");
  });

  it("uses static page content when the current locale has no managed page", async () => {
    mockedGetPublicSiteSettings.mockResolvedValue({
      pages: [managedPage("tr", "Turkish Managed Privacy")],
    } as any);

    renderManagedPage();

    await waitFor(() => expect(mockedGetPublicSiteSettings).toHaveBeenCalled());
    expect(screen.getByText("Static English Privacy")).toBeInTheDocument();
    expect(screen.queryByText("Turkish Managed Privacy")).not.toBeInTheDocument();
  });

  it("renders the managed page for the exact current locale", async () => {
    mockedGetPublicSiteSettings.mockResolvedValue({
      pages: [
        managedPage("tr", "Turkish Managed Privacy"),
        managedPage("en", "English Managed Privacy"),
      ],
    } as any);

    renderManagedPage();

    expect(await screen.findByRole("heading", { name: "English Managed Privacy" })).toBeInTheDocument();
    expect(screen.queryByText("Static English Privacy")).not.toBeInTheDocument();
  });

  it("keeps static page content when the exact locale managed page is a draft", async () => {
    mockedGetPublicSiteSettings.mockResolvedValue({
      pages: [managedPage("en", "Draft English Privacy", "privacy", false)],
    } as any);

    renderManagedPage();

    await waitFor(() => expect(mockedGetPublicSiteSettings).toHaveBeenCalled());
    expect(screen.getByText("Static English Privacy")).toBeInTheDocument();
    expect(screen.queryByText("Draft English Privacy")).not.toBeInTheDocument();
  });

  it("falls back to the published source page for custom routes without locale content", async () => {
    window.history.pushState({}, "", "/en/useful-guide");
    mockedGetPublicSiteSettings.mockResolvedValue({
      pages: [managedPage("tr", "Turkish Managed Guide", "useful-guide")],
    } as any);

    renderManagedPage("useful-guide", false);

    expect(await screen.findByRole("heading", { name: "Turkish Managed Guide" })).toBeInTheDocument();
    expect(screen.queryByText("Static English Privacy")).not.toBeInTheDocument();
  });

  it("falls back to the published source page when the custom locale copy is still a draft", async () => {
    window.history.pushState({}, "", "/en/useful-guide");
    mockedGetPublicSiteSettings.mockResolvedValue({
      pages: [
        managedPage("tr", "Turkish Managed Guide", "useful-guide"),
        managedPage("en", "Draft English Guide", "useful-guide", false),
      ],
    } as any);

    renderManagedPage("useful-guide", false);

    expect(await screen.findByRole("heading", { name: "Turkish Managed Guide" })).toBeInTheDocument();
    expect(screen.queryByText("Draft English Guide")).not.toBeInTheDocument();
  });
});
