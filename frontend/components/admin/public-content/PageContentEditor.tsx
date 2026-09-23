"use client";

import { useEffect, useMemo, useState } from "react";
import { Eye, EyeOff, Loader2, Save } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import {
  publishAdminPublicPage,
  unpublishAdminPublicPage,
  updateAdminPublicPageDraft,
} from "@/lib/api/admin/publicContent";
import type { AdminPublicContent, AdminPublicManagedPage, PublicSettingsLocale } from "@/lib/api/admin/types";
import ManagedContentRichTextEditor from "./ManagedContentRichTextEditor";

const locales = ["tr", "en", "ru", "ar", "de"] satisfies PublicSettingsLocale[];

const localeLabels: Record<PublicSettingsLocale, string> = {
  tr: "TR",
  en: "EN",
  ru: "RU",
  ar: "AR",
  de: "DE",
};

type PageContentEditorProps = {
  content: AdminPublicContent;
  onContentChange: (content: AdminPublicContent) => void;
};

function clonePage(page: AdminPublicManagedPage): AdminPublicManagedPage {
  return {
    ...page,
    blocks: page.blocks.map((block) => ({ ...block })),
  };
}

function isPublicSettingsLocale(locale: string): locale is PublicSettingsLocale {
  return (locales as readonly string[]).includes(locale);
}

function getPageLocale(page: AdminPublicManagedPage | undefined): PublicSettingsLocale {
  if (page && isPublicSettingsLocale(page.locale)) {
    return page.locale;
  }

  return "tr";
}

function getPageKey(page: AdminPublicManagedPage | null) {
  return page ? `${page.slug}:${page.locale}` : null;
}

function createMissingLocaleDraft(
  pages: AdminPublicManagedPage[],
  slug: string,
  locale: PublicSettingsLocale,
): AdminPublicManagedPage | null {
  const sourcePage = pages.find((page) => page.slug === slug && page.locale === "tr") ??
    pages.find((page) => page.slug === slug);

  if (!sourcePage) {
    return null;
  }

  return {
    ...clonePage(sourcePage),
    id: `${locale}-${slug}`,
    locale,
    isPublished: false,
    published: null,
    publishedAtUtc: null,
  };
}

export default function PageContentEditor({ content, onContentChange }: PageContentEditorProps) {
  const sortedPages = useMemo(
    () =>
      [...content.pages].sort(
        (firstPage, secondPage) =>
          firstPage.sortOrder - secondPage.sortOrder ||
          firstPage.slug.localeCompare(secondPage.slug) ||
          firstPage.locale.localeCompare(secondPage.locale),
      ),
    [content.pages],
  );
  const slugs = useMemo(() => Array.from(new Set(sortedPages.map((page) => page.slug))), [sortedPages]);
  const firstPage = sortedPages[0];
  const [selectedSlug, setSelectedSlug] = useState(firstPage?.slug ?? "");
  const [selectedLocale, setSelectedLocale] = useState<PublicSettingsLocale>(getPageLocale(firstPage));
  const [isSaving, setIsSaving] = useState(false);
  const [isPublishing, setIsPublishing] = useState(false);
  const [isUnpublishing, setIsUnpublishing] = useState(false);

  useEffect(() => {
    if (!selectedSlug && firstPage) {
      setSelectedSlug(firstPage.slug);
      setSelectedLocale(getPageLocale(firstPage));
      return;
    }

    if (selectedSlug && slugs.length > 0 && !slugs.includes(selectedSlug)) {
      setSelectedSlug(firstPage.slug);
      setSelectedLocale(getPageLocale(firstPage));
    }
  }, [firstPage, selectedSlug, slugs]);

  const selectedPage = useMemo(
    () => content.pages.find((page) => page.slug === selectedSlug && page.locale === selectedLocale) ?? null,
    [content.pages, selectedLocale, selectedSlug],
  );
  const editablePage = useMemo(
    () => selectedPage ?? createMissingLocaleDraft(content.pages, selectedSlug, selectedLocale),
    [content.pages, selectedLocale, selectedPage, selectedSlug],
  );
  const [draft, setDraft] = useState<AdminPublicManagedPage | null>(() =>
    editablePage ? clonePage(editablePage) : null,
  );
  const [draftKey, setDraftKey] = useState(() => getPageKey(editablePage));
  const [draftVersion, setDraftVersion] = useState(content.version);
  const [isDirty, setIsDirty] = useState(false);

  useEffect(() => {
    const selectedPageKey = getPageKey(editablePage);

    if (selectedPageKey !== draftKey || !isDirty) {
      setDraft(editablePage ? clonePage(editablePage) : null);
      setDraftKey(selectedPageKey);
      setDraftVersion(content.version);
      setIsDirty(false);
    }
  }, [content.version, draftKey, editablePage, isDirty]);

  const isMutating = isSaving || isPublishing || isUnpublishing;
  const isUnsavedLocaleDraft = Boolean(draft && !selectedPage);
  const statusLabel = isDirty
    ? "Kaydedilmemiş değişiklik"
    : selectedPage
      ? selectedPage.isPublished
        ? "Yayında"
        : "Taslak"
      : "Yeni çeviri taslağı";

  const selectSlug = (slug: string) => {
    setSelectedSlug(slug);

    if (!content.pages.some((page) => page.slug === slug && page.locale === selectedLocale)) {
      const nextPage = content.pages.find((page) => page.slug === slug && page.locale === "tr") ??
        content.pages.find((page) => page.slug === slug);
      setSelectedLocale(getPageLocale(nextPage));
    }
  };

  const updateDraft = <Key extends keyof AdminPublicManagedPage>(key: Key, value: AdminPublicManagedPage[Key]) => {
    setIsDirty(true);
    setDraft((currentDraft) => (currentDraft ? { ...currentDraft, [key]: value } : currentDraft));
  };

  const updateBlock = (blockIndex: number, patch: Partial<AdminPublicManagedPage["blocks"][number]>) => {
    setIsDirty(true);
    setDraft((currentDraft) =>
      currentDraft
        ? {
            ...currentDraft,
            blocks: currentDraft.blocks.map((block, index) => (index === blockIndex ? { ...block, ...patch } : block)),
          }
        : currentDraft,
    );
  };

  const saveDraft = async () => {
    if (!draft) {
      return;
    }

    setIsSaving(true);

    try {
      const nextContent = await updateAdminPublicPageDraft(draft.slug, draft.locale as PublicSettingsLocale, {
        version: draftVersion,
        title: draft.title,
        subtitle: draft.subtitle,
        seoTitle: draft.seoTitle,
        seoDescription: draft.seoDescription,
        isPublished: draft.isPublished,
        sortOrder: draft.sortOrder,
        blocks: draft.blocks.map((block, index) => ({
          ...block,
          sortOrder: index,
          bodyFormat: block.bodyFormat ?? "html",
        })),
      });

      setIsDirty(false);
      setDraftVersion(nextContent.version);
      onContentChange(nextContent);
      toast.success("Sayfa taslağı kaydedildi.");
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : "Sayfa taslağı kaydedilemedi.");
    } finally {
      setIsSaving(false);
    }
  };

  const publishDraft = async () => {
    if (!draft) {
      return;
    }

    setIsPublishing(true);

    try {
      const nextContent = await publishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, draftVersion);
      setIsDirty(false);
      setDraftVersion(nextContent.version);
      onContentChange(nextContent);
      toast.success("Sayfa yayınlandı.");
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : "Sayfa yayınlanamadı.");
    } finally {
      setIsPublishing(false);
    }
  };

  const unpublishDraft = async () => {
    if (!draft) {
      return;
    }

    setIsUnpublishing(true);

    try {
      const nextContent = await unpublishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, draftVersion);
      setIsDirty(false);
      setDraftVersion(nextContent.version);
      onContentChange(nextContent);
      toast.success("Sayfa yayından kaldırıldı.");
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : "Sayfa yayından kaldırılamadı.");
    } finally {
      setIsUnpublishing(false);
    }
  };

  if (sortedPages.length === 0) {
    return (
      <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
        Düzenlenecek public sayfa bulunamadı.
      </div>
    );
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
      <div className="space-y-2 rounded-md border p-3">
        <div className="px-1 text-xs font-medium uppercase text-muted-foreground">Sayfalar</div>
        {slugs.map((slug) => (
          <Button
            key={slug}
            type="button"
            variant={slug === selectedSlug ? "secondary" : "ghost"}
            className="w-full justify-start"
            onClick={() => selectSlug(slug)}
          >
            {slug}
          </Button>
        ))}
      </div>

      <div className="space-y-4 rounded-md border p-4">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-4">
          <div className="flex flex-wrap gap-2" aria-label="Dil seçimi">
            {locales.map((locale) => (
              <Button
                key={locale}
                type="button"
                variant={locale === selectedLocale ? "default" : "outline"}
                size="sm"
                onClick={() => setSelectedLocale(locale)}
              >
                {localeLabels[locale]}
              </Button>
            ))}
          </div>
          <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <Badge variant="outline">Dil: {localeLabels[selectedLocale]}</Badge>
            <Badge variant={isDirty ? "warning" : selectedPage?.isPublished ? "success" : "secondary"}>
              {statusLabel}
            </Badge>
            <span>Sürüm {content.version}</span>
          </div>
        </div>

        {!draft ? (
          <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
            Bu sayfa için seçili dilde içerik bulunamadı.
          </div>
        ) : (
          <>
            <div
              aria-live="polite"
              className={cn(
                "rounded-md border px-3 py-2 text-sm",
                isDirty
                  ? "border-orange-300 bg-orange-50 text-orange-900"
                  : isUnsavedLocaleDraft
                    ? "border-blue-300 bg-blue-50 text-blue-900"
                    : "border-green-200 bg-green-50 text-green-900",
              )}
            >
              {isDirty
                ? "Bu dilde kaydedilmemiş değişiklik var. Yayınla veya yayından kaldırmadan önce taslağı kaydet."
                : isUnsavedLocaleDraft
                  ? "Bu dil için yeni bir taslak hazırlanıyor. Public sitede görünmesi için önce kaydet, sonra yayınla."
                  : "Son kaydedilen içerik gösteriliyor. Dil ve yayın durumu bu sayfa içeriği için geçerli."}
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="page-title">Sayfa Başlığı</Label>
                <Input
                  id="page-title"
                  value={draft.title}
                  onChange={(event) => updateDraft("title", event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="page-subtitle">Alt Başlık</Label>
                <Input
                  id="page-subtitle"
                  value={draft.subtitle}
                  onChange={(event) => updateDraft("subtitle", event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="page-seo-title">SEO Başlığı</Label>
                <Input
                  id="page-seo-title"
                  value={draft.seoTitle}
                  onChange={(event) => updateDraft("seoTitle", event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="page-seo-description">SEO Açıklaması</Label>
                <Input
                  id="page-seo-description"
                  value={draft.seoDescription}
                  onChange={(event) => updateDraft("seoDescription", event.target.value)}
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <div className="text-sm font-medium">Yayın Durumu</div>
                <div className="text-sm text-muted-foreground">
                  {draft.isPublished ? "Yayında" : "Taslak"} · Yayın durumunu alttaki yayınla/yayından kaldır butonları değiştirir.
                </div>
              </div>
            </div>

            <div className="space-y-3">
              {draft.blocks.length === 0 ? (
                <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                  Bu sayfada düzenlenecek blok yok.
                </div>
              ) : (
                draft.blocks.map((block, index) => (
                  <div key={block.id} className="space-y-3 rounded-md border p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="text-sm font-medium">Blok {index + 1}</div>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className={cn(!block.isVisible && "text-muted-foreground")}
                        onClick={() => updateBlock(index, { isVisible: !block.isVisible })}
                      >
                        {block.isVisible ? <Eye className="h-4 w-4" /> : <EyeOff className="h-4 w-4" />}
                        {block.isVisible ? "Görünür" : "Gizli"}
                      </Button>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor={`block-heading-${block.id}`}>Bölüm Başlığı</Label>
                      <Input
                        id={`block-heading-${block.id}`}
                        value={block.heading}
                        onChange={(event) => updateBlock(index, { heading: event.target.value })}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label>İçerik</Label>
                      <ManagedContentRichTextEditor
                        value={block.body}
                        onChange={(body) => updateBlock(index, { body, bodyFormat: "html" })}
                      />
                    </div>
                  </div>
                ))
              )}
            </div>

            <div className="flex flex-wrap gap-2 border-t pt-4">
              <Button type="button" onClick={saveDraft} disabled={isMutating}>
                {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                Taslağı Kaydet
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={publishDraft}
                disabled={isMutating || isDirty || isUnsavedLocaleDraft}
              >
                {isPublishing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Eye className="h-4 w-4" />}
                Yayınla
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={unpublishDraft}
                disabled={isMutating || isDirty || isUnsavedLocaleDraft}
              >
                {isUnpublishing ? <Loader2 className="h-4 w-4 animate-spin" /> : <EyeOff className="h-4 w-4" />}
                Yayından Kaldır
              </Button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
