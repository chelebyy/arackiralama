"use client";

import type { ReactNode } from "react";
import useSWR from "swr";
import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";
import type { PublicManagedPage } from "@/lib/api/admin/types";
import { sanitizeManagedHtml } from "@/lib/public-content/sanitize-managed-html";

type ManagedPageContentProps = {
  slug: string;
  children?: ReactNode;
};

function findPage(
  pages: PublicManagedPage[] | undefined,
  slug: string,
  locale: string,
  allowLocaleFallback = false,
) {
  const slugPages = pages?.filter((page) => page.slug === slug);
  const exactPage = slugPages?.find((page) => page.locale === locale);

  if (!allowLocaleFallback || exactPage?.isPublished) {
    return exactPage;
  }

  return (
    slugPages?.find((page) => page.locale === "tr" && page.isPublished) ??
    slugPages?.find((page) => page.isPublished) ??
    exactPage ??
    slugPages?.[0]
  );
}

function splitParagraphs(value: string) {
  return value
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean);
}

function getCurrentLocale() {
  if (typeof window === "undefined") {
    return "tr";
  }

  return window.location.pathname.split("/").filter(Boolean)[0] || "tr";
}

function NotPublishedPage() {
  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="mx-auto max-w-3xl px-4 py-24 text-center sm:px-6 lg:px-8">
        <p className="text-sm font-semibold text-[#0369A1]">404</p>
        <h1 className="mt-3 text-3xl font-bold text-[#0F172A]">Sayfa yayında değil</h1>
        <p className="mt-4 text-[#64748B]">
          Bu public sayfa admin panelinden yayından kaldırılmış veya henüz yayınlanmamış.
        </p>
      </div>
    </div>
  );
}

function ManagedBlockBody({ block }: { block: PublicManagedPage["blocks"][number] }) {
  if (block.bodyFormat === "html") {
    return (
      <div
        className="space-y-4 text-[#475569] [&_a]:font-semibold [&_a]:text-[#0369A1] [&_blockquote]:border-l-4 [&_blockquote]:border-[#CBD5E1] [&_blockquote]:pl-4"
        dangerouslySetInnerHTML={{ __html: sanitizeManagedHtml(block.body) }}
      />
    );
  }

  return (
    <div className="space-y-4 text-[#475569]">
      {splitParagraphs(block.body).map((paragraph) => (
        <p key={paragraph.slice(0, 48)} className="leading-relaxed">
          {paragraph}
        </p>
      ))}
    </div>
  );
}

function ManagedPage({ page }: { page: PublicManagedPage }) {
  const visibleBlocks = page.blocks
    .filter((block) => block.isVisible)
    .sort((a, b) => a.sortOrder - b.sortOrder);

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="mx-auto max-w-3xl text-center">
            <h1 className="mb-6 text-3xl font-bold text-white lg:text-5xl">
              {page.title}
            </h1>
            {page.subtitle && (
              <p className="text-lg text-white/70 lg:text-xl">
                {page.subtitle}
              </p>
            )}
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-4xl px-4 py-16 sm:px-6 lg:px-8 lg:py-24">
        <div className="space-y-8">
          {visibleBlocks.map((block) => (
            <section key={block.id} className="rounded-2xl border border-[#E2E8F0] bg-white p-6 lg:p-8">
              <h2 className="mb-4 text-2xl font-bold text-[#0F172A]">{block.heading}</h2>
              <ManagedBlockBody block={block} />
            </section>
          ))}
        </div>
      </div>
    </div>
  );
}

export default function ManagedPageContent({ slug, children }: ManagedPageContentProps) {
  const locale = getCurrentLocale();
  const { data: settings } = useSWR("public-site-settings", getPublicSiteSettings, {
    revalidateOnFocus: false,
    shouldRetryOnError: false,
  });

  const managedPage = findPage(settings?.pages, slug, locale, children === undefined);

  if (!settings) {
    return <>{children ?? <NotPublishedPage />}</>;
  }

  if (!managedPage) {
    return <>{children ?? <NotPublishedPage />}</>;
  }

  if (!managedPage.isPublished) {
    return <>{children ?? <NotPublishedPage />}</>;
  }

  return <ManagedPage page={managedPage} />;
}
