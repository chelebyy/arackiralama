import type { PublicManagedPage, PublicSiteLink } from "@/lib/api/admin/types";

function getPublicPageSlug(href: string) {
  if (!href.startsWith("/") || href.startsWith("//")) {
    return null;
  }

  const path = href.split(/[?#]/)[0]?.replace(/^\/+|\/+$/g, "") ?? "";
  if (!path || path.includes("/")) {
    return null;
  }

  return path;
}

export function isPublicSiteLinkVisible(link: PublicSiteLink, pages?: PublicManagedPage[]) {
  if (!link.isVisible) {
    return false;
  }

  const slug = getPublicPageSlug(link.href);
  if (!slug) {
    return true;
  }

  const managedPages = pages?.filter((page) => page.slug === slug);
  if (!managedPages?.length) {
    return true;
  }

  return managedPages.some((page) => page.isPublished);
}
