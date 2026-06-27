import { describe, expect, it } from "vitest";

import type { PublicManagedPage, PublicSiteLink } from "@/lib/api/admin/types";
import { isPublicSiteLinkVisible } from "./public-page-visibility";

function link(overrides: Partial<PublicSiteLink> = {}): PublicSiteLink {
  return {
    id: "privacy",
    label: "Privacy",
    href: "/privacy",
    isVisible: true,
    sortOrder: 0,
    ...overrides,
  };
}

function page(locale: string, isPublished: boolean): PublicManagedPage {
  return {
    id: `${locale}-privacy`,
    slug: "privacy",
    locale,
    title: "Privacy",
    subtitle: "",
    seoTitle: "",
    seoDescription: "",
    isPublished,
    sortOrder: 0,
    blocks: [],
  };
}

describe("isPublicSiteLinkVisible", () => {
  it("keeps links visible when any managed locale for the slug is published", () => {
    expect(isPublicSiteLinkVisible(link(), [page("tr", false), page("en", true)])).toBe(true);
  });

  it("hides managed page links only when every locale for the slug is a draft", () => {
    expect(isPublicSiteLinkVisible(link(), [page("tr", false), page("en", false)])).toBe(false);
  });

  it("keeps unmanaged internal links visible and respects the link visibility flag", () => {
    expect(isPublicSiteLinkVisible(link({ href: "/vehicles" }), [page("tr", false)])).toBe(true);
    expect(isPublicSiteLinkVisible(link({ isVisible: false }), [page("tr", true)])).toBe(false);
  });
});
