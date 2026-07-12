# Task 7 Review Package R2

Base: 0e41610
Head: 98c4074a36c082bf800c151bfe9e38c869669a92

## Commit Log
```
98c4074 fix(admin): clarify page publication controls
44ef18b feat(admin): add page content editor
```

## Diff Stat
```
 .../public-content/PublicContentManager.test.tsx   |  92 +++++-
 .../ManagedContentRichTextEditor.tsx               | 182 +++++++++++
 .../admin/public-content/PageContentEditor.tsx     | 345 +++++++++++++++++++++
 .../admin/public-content/PublicContentManager.tsx  |  31 +-
 4 files changed, 631 insertions(+), 19 deletions(-)
```

## Diff
```diff
diff --git a/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
index a9ea625..4755f29 100644
--- a/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
+++ b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
@@ -1,46 +1,90 @@
 import { render, screen } from "@testing-library/react";
+import userEvent from "@testing-library/user-event";
 import { beforeEach, describe, expect, it, vi } from "vitest";
 import { SWRConfig } from "swr";
-import { getAdminPublicContent } from "@/lib/api/admin/publicContent";
+import {
+  getAdminPublicContent,
+  publishAdminPublicPage,
+  unpublishAdminPublicPage,
+  updateAdminPublicPageDraft,
+} from "@/lib/api/admin/publicContent";
+import type { AdminPublicContent } from "@/lib/api/admin/types";
 import PublicContentPage from "./page";
 
-const adminContentFixture = {
+const adminContentFixture: AdminPublicContent = {
   version: "1",
   updatedAt: "2026-06-27T00:00:00Z",
-  pages: [],
+  pages: [
+    {
+      id: "page-privacy-tr",
+      slug: "privacy",
+      locale: "tr",
+      title: "Gizlilik",
+      subtitle: "Gizlilik politikası",
+      seoTitle: "Gizlilik",
+      seoDescription: "Gizlilik politikası",
+      isPublished: true,
+      sortOrder: 0,
+      blocks: [
+        {
+          id: "privacy-body",
+          heading: "Veri Kullanımı",
+          body: "<p>Rezervasyon verileri.</p>",
+          bodyFormat: "html",
+          isVisible: true,
+          sortOrder: 0,
+        },
+      ],
+      published: null,
+      draftUpdatedAtUtc: null,
+      publishedAtUtc: null,
+    },
+  ],
   contactPageChannels: [],
   contactPageOffices: [],
   contactPageWorkingHours: [],
   contactPageMapTitle: "",
   contactPageMapEmbedUrl: "",
   contactPageMapIsVisible: true,
 };
 
 vi.mock("@/lib/api/admin/publicContent", () => ({
   getAdminPublicContent: vi.fn(),
+  publishAdminPublicPage: vi.fn(),
+  unpublishAdminPublicPage: vi.fn(),
+  updateAdminPublicPageDraft: vi.fn(),
 }));
 
 const getAdminPublicContentMock = vi.mocked(getAdminPublicContent);
+const publishAdminPublicPageMock = vi.mocked(publishAdminPublicPage);
+const unpublishAdminPublicPageMock = vi.mocked(unpublishAdminPublicPage);
+const updateAdminPublicPageDraftMock = vi.mocked(updateAdminPublicPageDraft);
 
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
+    publishAdminPublicPageMock.mockReset();
+    unpublishAdminPublicPageMock.mockReset();
+    updateAdminPublicPageDraftMock.mockReset();
     getAdminPublicContentMock.mockResolvedValue(adminContentFixture);
+    publishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
+    unpublishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
+    updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
   });
 
   it("renders the public content workspace", async () => {
     renderPublicContentPage();
 
     expect(await screen.findByRole("heading", { name: "İçerik Yönetimi" })).toBeInTheDocument();
     expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
     expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
   });
 
@@ -50,11 +94,53 @@ describe("PublicContentPage", () => {
     renderPublicContentPage({
       fallback: {
         "admin-public-content": adminContentFixture,
       },
     });
 
     expect(await screen.findByRole("status")).toHaveTextContent("Son kayıtlar gösteriliyor");
     expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
     expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
   });
+
+  it("saves a selected page draft", async () => {
+    const user = userEvent.setup();
+    updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
+    getAdminPublicContentMock.mockResolvedValue(adminContentFixture);
+
+    renderPublicContentPage();
+
+    await user.click(await screen.findByRole("button", { name: /privacy/i }));
+    await user.clear(screen.getByLabelText("Sayfa Başlığı"));
+    await user.type(screen.getByLabelText("Sayfa Başlığı"), "Yeni Gizlilik");
+    await user.click(screen.getByRole("button", { name: "Taslağı Kaydet" }));
+
+    expect(updateAdminPublicPageDraftMock).toHaveBeenCalledWith(
+      "privacy",
+      "tr",
+      expect.objectContaining({
+        title: "Yeni Gizlilik",
+        version: "1",
+        isPublished: true,
+        blocks: [
+          expect.objectContaining({
+            id: "privacy-body",
+            bodyFormat: "html",
+            sortOrder: 0,
+          }),
+        ],
+      }),
+    );
+  });
+
+  it("publishes and unpublishes the selected page", async () => {
+    const user = userEvent.setup();
+
+    renderPublicContentPage();
+
+    await user.click(await screen.findByRole("button", { name: "Yayınla" }));
+    expect(publishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");
+
+    await user.click(screen.getByRole("button", { name: "Yayından Kaldır" }));
+    expect(unpublishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");
+  });
 });
diff --git a/frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx b/frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx
new file mode 100644
index 0000000..663dc48
--- /dev/null
+++ b/frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx
@@ -0,0 +1,182 @@
+"use client";
+
+import { useEffect } from "react";
+import { EditorContent, useEditor } from "@tiptap/react";
+import Link from "@tiptap/extension-link";
+import Placeholder from "@tiptap/extension-placeholder";
+import Underline from "@tiptap/extension-underline";
+import StarterKit from "@tiptap/starter-kit";
+import { Bold, Italic, LinkIcon, List, ListOrdered, Redo, UnderlineIcon, Undo } from "lucide-react";
+import { toast } from "sonner";
+import { Button } from "@/components/ui/button";
+import { cn } from "@/lib/utils";
+
+type ManagedContentRichTextEditorProps = {
+  value: string;
+  onChange: (value: string) => void;
+  placeholder?: string;
+};
+
+const allowedLinkProtocolPattern = /^(https?:|mailto:|tel:)/i;
+
+function isAllowedManagedContentLink(url: string) {
+  const normalizedUrl = url.trim();
+  return normalizedUrl.length > 0 && !normalizedUrl.startsWith("//") && allowedLinkProtocolPattern.test(normalizedUrl);
+}
+
+export default function ManagedContentRichTextEditor({
+  value,
+  onChange,
+  placeholder = "İçeriği yazın",
+}: ManagedContentRichTextEditorProps) {
+  const editor = useEditor({
+    immediatelyRender: false,
+    extensions: [
+      StarterKit.configure({ codeBlock: false, code: false, horizontalRule: false }),
+      Underline,
+      Link.configure({
+        openOnClick: false,
+        autolink: false,
+        defaultProtocol: "https",
+        isAllowedUri: (url, ctx) => ctx.defaultValidate(url) && isAllowedManagedContentLink(url),
+      }),
+      Placeholder.configure({ placeholder }),
+    ],
+    content: value || "<p></p>",
+    onUpdate: ({ editor: currentEditor }) => onChange(currentEditor.getHTML()),
+  });
+
+  useEffect(() => {
+    if (!editor) {
+      return;
+    }
+
+    const nextValue = value || "<p></p>";
+    if (editor.getHTML() !== nextValue) {
+      editor.commands.setContent(nextValue, { emitUpdate: false });
+    }
+  }, [editor, value]);
+
+  if (!editor) {
+    return null;
+  }
+
+  const addLink = () => {
+    const currentHref = editor.getAttributes("link").href as string | undefined;
+    const href = window.prompt("Link URL", currentHref ?? "");
+
+    if (href === null) {
+      return;
+    }
+
+    const normalizedHref = href.trim();
+    if (!normalizedHref) {
+      editor.chain().focus().extendMarkRange("link").unsetLink().run();
+      return;
+    }
+
+    if (!isAllowedManagedContentLink(normalizedHref)) {
+      toast.error("Sadece http(s), mailto veya tel linkleri kullanılabilir.");
+      return;
+    }
+
+    editor.chain().focus().extendMarkRange("link").setLink({ href: normalizedHref }).run();
+  };
+
+  return (
+    <div className="rounded-md border bg-background">
+      <div className="flex flex-wrap gap-1 border-b p-2">
+        <Button
+          type="button"
+          variant={editor.isActive("bold") ? "secondary" : "ghost"}
+          size="icon-sm"
+          onClick={() => editor.chain().focus().toggleBold().run()}
+          aria-label="Kalın"
+          aria-pressed={editor.isActive("bold")}
+        >
+          <Bold className="h-4 w-4" />
+        </Button>
+        <Button
+          type="button"
+          variant={editor.isActive("italic") ? "secondary" : "ghost"}
+          size="icon-sm"
+          onClick={() => editor.chain().focus().toggleItalic().run()}
+          aria-label="İtalik"
+          aria-pressed={editor.isActive("italic")}
+        >
+          <Italic className="h-4 w-4" />
+        </Button>
+        <Button
+          type="button"
+          variant={editor.isActive("underline") ? "secondary" : "ghost"}
+          size="icon-sm"
+          onClick={() => editor.chain().focus().toggleUnderline().run()}
+          aria-label="Altı çizili"
+          aria-pressed={editor.isActive("underline")}
+        >
+          <UnderlineIcon className="h-4 w-4" />
+        </Button>
+        <Button
+          type="button"
+          variant={editor.isActive("bulletList") ? "secondary" : "ghost"}
+          size="icon-sm"
+          onClick={() => editor.chain().focus().toggleBulletList().run()}
+          aria-label="Madde listesi"
+          aria-pressed={editor.isActive("bulletList")}
+        >
+          <List className="h-4 w-4" />
+        </Button>
+        <Button
+          type="button"
+          variant={editor.isActive("orderedList") ? "secondary" : "ghost"}
+          size="icon-sm"
+          onClick={() => editor.chain().focus().toggleOrderedList().run()}
+          aria-label="Numaralı liste"
+          aria-pressed={editor.isActive("orderedList")}
+        >
+          <ListOrdered className="h-4 w-4" />
+        </Button>
+        <Button
+          type="button"
+          variant={editor.isActive("link") ? "secondary" : "ghost"}
+          size="icon-sm"
+          onClick={addLink}
+          aria-label="Link ekle"
+          aria-pressed={editor.isActive("link")}
+        >
+          <LinkIcon className="h-4 w-4" />
+        </Button>
+        <Button
+          type="button"
+          variant="ghost"
+          size="icon-sm"
+          onClick={() => editor.chain().focus().undo().run()}
+          aria-label="Geri al"
+          disabled={!editor.can().undo()}
+        >
+          <Undo className="h-4 w-4" />
+        </Button>
+        <Button
+          type="button"
+          variant="ghost"
+          size="icon-sm"
+          onClick={() => editor.chain().focus().redo().run()}
+          aria-label="Yinele"
+          disabled={!editor.can().redo()}
+        >
+          <Redo className="h-4 w-4" />
+        </Button>
+      </div>
+      <EditorContent
+        editor={editor}
+        className={cn(
+          "min-h-48 cursor-text px-3 py-2 text-sm",
+          "[&_.tiptap]:min-h-44 [&_.tiptap]:outline-none [&_.tiptap_p]:my-2",
+          "[&_.tiptap_ul]:my-2 [&_.tiptap_ul]:list-disc [&_.tiptap_ul]:pl-5",
+          "[&_.tiptap_ol]:my-2 [&_.tiptap_ol]:list-decimal [&_.tiptap_ol]:pl-5",
+          "[&_.tiptap_a]:text-primary [&_.tiptap_a]:underline",
+        )}
+      />
+    </div>
+  );
+}
diff --git a/frontend/components/admin/public-content/PageContentEditor.tsx b/frontend/components/admin/public-content/PageContentEditor.tsx
new file mode 100644
index 0000000..d4b4f8e
--- /dev/null
+++ b/frontend/components/admin/public-content/PageContentEditor.tsx
@@ -0,0 +1,345 @@
+"use client";
+
+import { useEffect, useMemo, useState } from "react";
+import { Eye, EyeOff, Loader2, Save } from "lucide-react";
+import { toast } from "sonner";
+import { Button } from "@/components/ui/button";
+import { Input } from "@/components/ui/input";
+import { Label } from "@/components/ui/label";
+import { cn } from "@/lib/utils";
+import {
+  publishAdminPublicPage,
+  unpublishAdminPublicPage,
+  updateAdminPublicPageDraft,
+} from "@/lib/api/admin/publicContent";
+import type { AdminPublicContent, AdminPublicManagedPage, PublicSettingsLocale } from "@/lib/api/admin/types";
+import ManagedContentRichTextEditor from "./ManagedContentRichTextEditor";
+
+const locales = ["tr", "en", "ru", "ar", "de"] satisfies PublicSettingsLocale[];
+
+const localeLabels: Record<PublicSettingsLocale, string> = {
+  tr: "TR",
+  en: "EN",
+  ru: "RU",
+  ar: "AR",
+  de: "DE",
+};
+
+type PageContentEditorProps = {
+  content: AdminPublicContent;
+  onContentChange: (content: AdminPublicContent) => void;
+};
+
+function clonePage(page: AdminPublicManagedPage): AdminPublicManagedPage {
+  return {
+    ...page,
+    blocks: page.blocks.map((block) => ({ ...block })),
+  };
+}
+
+function isPublicSettingsLocale(locale: string): locale is PublicSettingsLocale {
+  return (locales as readonly string[]).includes(locale);
+}
+
+function getPageLocale(page: AdminPublicManagedPage | undefined): PublicSettingsLocale {
+  if (page && isPublicSettingsLocale(page.locale)) {
+    return page.locale;
+  }
+
+  return "tr";
+}
+
+export default function PageContentEditor({ content, onContentChange }: PageContentEditorProps) {
+  const sortedPages = useMemo(
+    () =>
+      [...content.pages].sort(
+        (firstPage, secondPage) =>
+          firstPage.sortOrder - secondPage.sortOrder ||
+          firstPage.slug.localeCompare(secondPage.slug) ||
+          firstPage.locale.localeCompare(secondPage.locale),
+      ),
+    [content.pages],
+  );
+  const slugs = useMemo(() => Array.from(new Set(sortedPages.map((page) => page.slug))), [sortedPages]);
+  const firstPage = sortedPages[0];
+  const [selectedSlug, setSelectedSlug] = useState(firstPage?.slug ?? "");
+  const [selectedLocale, setSelectedLocale] = useState<PublicSettingsLocale>(getPageLocale(firstPage));
+  const [isSaving, setIsSaving] = useState(false);
+  const [isPublishing, setIsPublishing] = useState(false);
+  const [isUnpublishing, setIsUnpublishing] = useState(false);
+
+  useEffect(() => {
+    if (!selectedSlug && firstPage) {
+      setSelectedSlug(firstPage.slug);
+      setSelectedLocale(getPageLocale(firstPage));
+      return;
+    }
+
+    if (selectedSlug && slugs.length > 0 && !slugs.includes(selectedSlug)) {
+      setSelectedSlug(firstPage.slug);
+      setSelectedLocale(getPageLocale(firstPage));
+    }
+  }, [firstPage, selectedSlug, slugs]);
+
+  const selectedPage = useMemo(
+    () => content.pages.find((page) => page.slug === selectedSlug && page.locale === selectedLocale) ?? null,
+    [content.pages, selectedLocale, selectedSlug],
+  );
+  const [draft, setDraft] = useState<AdminPublicManagedPage | null>(() =>
+    selectedPage ? clonePage(selectedPage) : null,
+  );
+
+  useEffect(() => {
+    setDraft(selectedPage ? clonePage(selectedPage) : null);
+  }, [selectedPage]);
+
+  const isMutating = isSaving || isPublishing || isUnpublishing;
+
+  const selectSlug = (slug: string) => {
+    setSelectedSlug(slug);
+
+    if (!content.pages.some((page) => page.slug === slug && page.locale === selectedLocale)) {
+      const nextPage = content.pages.find((page) => page.slug === slug && page.locale === "tr") ??
+        content.pages.find((page) => page.slug === slug);
+      setSelectedLocale(getPageLocale(nextPage));
+    }
+  };
+
+  const updateDraft = <Key extends keyof AdminPublicManagedPage>(key: Key, value: AdminPublicManagedPage[Key]) => {
+    setDraft((currentDraft) => (currentDraft ? { ...currentDraft, [key]: value } : currentDraft));
+  };
+
+  const updateBlock = (blockIndex: number, patch: Partial<AdminPublicManagedPage["blocks"][number]>) => {
+    setDraft((currentDraft) =>
+      currentDraft
+        ? {
+            ...currentDraft,
+            blocks: currentDraft.blocks.map((block, index) => (index === blockIndex ? { ...block, ...patch } : block)),
+          }
+        : currentDraft,
+    );
+  };
+
+  const saveDraft = async () => {
+    if (!draft) {
+      return;
+    }
+
+    setIsSaving(true);
+
+    try {
+      const nextContent = await updateAdminPublicPageDraft(draft.slug, draft.locale as PublicSettingsLocale, {
+        version: content.version,
+        title: draft.title,
+        subtitle: draft.subtitle,
+        seoTitle: draft.seoTitle,
+        seoDescription: draft.seoDescription,
+        isPublished: draft.isPublished,
+        sortOrder: draft.sortOrder,
+        blocks: draft.blocks.map((block, index) => ({
+          ...block,
+          sortOrder: index,
+          bodyFormat: block.bodyFormat ?? "html",
+        })),
+      });
+
+      onContentChange(nextContent);
+      toast.success("Sayfa taslağı kaydedildi.");
+    } catch (error) {
+      toast.error(error instanceof Error && error.message ? error.message : "Sayfa taslağı kaydedilemedi.");
+    } finally {
+      setIsSaving(false);
+    }
+  };
+
+  const publishDraft = async () => {
+    if (!draft) {
+      return;
+    }
+
+    setIsPublishing(true);
+
+    try {
+      const nextContent = await publishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, content.version);
+      onContentChange(nextContent);
+      toast.success("Sayfa yayınlandı.");
+    } catch (error) {
+      toast.error(error instanceof Error && error.message ? error.message : "Sayfa yayınlanamadı.");
+    } finally {
+      setIsPublishing(false);
+    }
+  };
+
+  const unpublishDraft = async () => {
+    if (!draft) {
+      return;
+    }
+
+    setIsUnpublishing(true);
+
+    try {
+      const nextContent = await unpublishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, content.version);
+      onContentChange(nextContent);
+      toast.success("Sayfa yayından kaldırıldı.");
+    } catch (error) {
+      toast.error(error instanceof Error && error.message ? error.message : "Sayfa yayından kaldırılamadı.");
+    } finally {
+      setIsUnpublishing(false);
+    }
+  };
+
+  if (sortedPages.length === 0) {
+    return (
+      <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
+        Düzenlenecek public sayfa bulunamadı.
+      </div>
+    );
+  }
+
+  return (
+    <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
+      <div className="space-y-2 rounded-md border p-3">
+        <div className="px-1 text-xs font-medium uppercase text-muted-foreground">Sayfalar</div>
+        {slugs.map((slug) => (
+          <Button
+            key={slug}
+            type="button"
+            variant={slug === selectedSlug ? "secondary" : "ghost"}
+            className="w-full justify-start"
+            onClick={() => selectSlug(slug)}
+          >
+            {slug}
+          </Button>
+        ))}
+      </div>
+
+      <div className="space-y-4 rounded-md border p-4">
+        <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-4">
+          <div className="flex flex-wrap gap-2" aria-label="Dil seçimi">
+            {locales.map((locale) => (
+              <Button
+                key={locale}
+                type="button"
+                variant={locale === selectedLocale ? "default" : "outline"}
+                size="sm"
+                onClick={() => setSelectedLocale(locale)}
+              >
+                {localeLabels[locale]}
+              </Button>
+            ))}
+          </div>
+          {selectedPage ? (
+            <div className="text-sm text-muted-foreground">
+              Sürüm {content.version} · {selectedPage.isPublished ? "Yayında" : "Taslak"}
+            </div>
+          ) : null}
+        </div>
+
+        {!draft ? (
+          <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
+            Bu sayfa için seçili dilde içerik bulunamadı.
+          </div>
+        ) : (
+          <>
+            <div className="grid gap-3 md:grid-cols-2">
+              <div className="space-y-2">
+                <Label htmlFor="page-title">Sayfa Başlığı</Label>
+                <Input
+                  id="page-title"
+                  value={draft.title}
+                  onChange={(event) => updateDraft("title", event.target.value)}
+                />
+              </div>
+              <div className="space-y-2">
+                <Label htmlFor="page-subtitle">Alt Başlık</Label>
+                <Input
+                  id="page-subtitle"
+                  value={draft.subtitle}
+                  onChange={(event) => updateDraft("subtitle", event.target.value)}
+                />
+              </div>
+              <div className="space-y-2">
+                <Label htmlFor="page-seo-title">SEO Başlığı</Label>
+                <Input
+                  id="page-seo-title"
+                  value={draft.seoTitle}
+                  onChange={(event) => updateDraft("seoTitle", event.target.value)}
+                />
+              </div>
+              <div className="space-y-2">
+                <Label htmlFor="page-seo-description">SEO Açıklaması</Label>
+                <Input
+                  id="page-seo-description"
+                  value={draft.seoDescription}
+                  onChange={(event) => updateDraft("seoDescription", event.target.value)}
+                />
+              </div>
+              <div className="space-y-2 md:col-span-2">
+                <div className="text-sm font-medium">Yayın Durumu</div>
+                <div className="text-sm text-muted-foreground">
+                  {draft.isPublished ? "Yayında" : "Taslak"} · Yayın durumunu alttaki yayınla/yayından kaldır butonları değiştirir.
+                </div>
+              </div>
+            </div>
+
+            <div className="space-y-3">
+              {draft.blocks.length === 0 ? (
+                <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
+                  Bu sayfada düzenlenecek blok yok.
+                </div>
+              ) : (
+                draft.blocks.map((block, index) => (
+                  <div key={block.id} className="space-y-3 rounded-md border p-3">
+                    <div className="flex flex-wrap items-center justify-between gap-2">
+                      <div className="text-sm font-medium">Blok {index + 1}</div>
+                      <Button
+                        type="button"
+                        variant="ghost"
+                        size="sm"
+                        className={cn(!block.isVisible && "text-muted-foreground")}
+                        onClick={() => updateBlock(index, { isVisible: !block.isVisible })}
+                      >
+                        {block.isVisible ? <Eye className="h-4 w-4" /> : <EyeOff className="h-4 w-4" />}
+                        {block.isVisible ? "Görünür" : "Gizli"}
+                      </Button>
+                    </div>
+                    <div className="space-y-2">
+                      <Label htmlFor={`block-heading-${block.id}`}>Bölüm Başlığı</Label>
+                      <Input
+                        id={`block-heading-${block.id}`}
+                        value={block.heading}
+                        onChange={(event) => updateBlock(index, { heading: event.target.value })}
+                      />
+                    </div>
+                    <div className="space-y-2">
+                      <Label>İçerik</Label>
+                      <ManagedContentRichTextEditor
+                        value={block.body}
+                        onChange={(body) => updateBlock(index, { body, bodyFormat: "html" })}
+                      />
+                    </div>
+                  </div>
+                ))
+              )}
+            </div>
+
+            <div className="flex flex-wrap gap-2 border-t pt-4">
+              <Button type="button" onClick={saveDraft} disabled={isMutating}>
+                {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
+                Taslağı Kaydet
+              </Button>
+              <Button type="button" variant="outline" onClick={publishDraft} disabled={isMutating}>
+                {isPublishing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Eye className="h-4 w-4" />}
+                Yayınla
+              </Button>
+              <Button type="button" variant="outline" onClick={unpublishDraft} disabled={isMutating}>
+                {isUnpublishing ? <Loader2 className="h-4 w-4 animate-spin" /> : <EyeOff className="h-4 w-4" />}
+                Yayından Kaldır
+              </Button>
+            </div>
+          </>
+        )}
+      </div>
+    </div>
+  );
+}
diff --git a/frontend/components/admin/public-content/PublicContentManager.tsx b/frontend/components/admin/public-content/PublicContentManager.tsx
index 2292435..5e2945e 100644
--- a/frontend/components/admin/public-content/PublicContentManager.tsx
+++ b/frontend/components/admin/public-content/PublicContentManager.tsx
@@ -1,24 +1,25 @@
 "use client";
 
 import { FileText, Phone } from "lucide-react";
 import useSWR from "swr";
 import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
 import { Skeleton } from "@/components/ui/skeleton";
 import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
 import { getAdminPublicContent } from "@/lib/api/admin/publicContent";
 import type { AdminPublicContent } from "@/lib/api/admin/types";
+import PageContentEditor from "./PageContentEditor";
 
 const PUBLIC_CONTENT_CACHE_KEY = "admin-public-content";
 
 export default function PublicContentManager() {
-  const { data, error, isLoading } = useSWR<AdminPublicContent>(
+  const { data, error, isLoading, mutate } = useSWR<AdminPublicContent>(
     PUBLIC_CONTENT_CACHE_KEY,
     () => getAdminPublicContent(),
   );
   const hasLoadError = !data && (error || !isLoading);
 
   return (
     <div className="space-y-4">
       <h1 className="text-2xl font-bold tracking-tight">İçerik Yönetimi</h1>
 
       {hasLoadError ? (
@@ -43,35 +44,33 @@ export default function PublicContentManager() {
               <FileText className="mr-2 h-4 w-4" />
               Sayfalar
             </TabsTrigger>
             <TabsTrigger value="contact">
               <Phone className="mr-2 h-4 w-4" />
               İletişim
             </TabsTrigger>
           </TabsList>
 
           <TabsContent value="pages">
-            <Card>
-              <CardHeader>
-                <CardTitle className="text-base">Sayfalar</CardTitle>
-              </CardHeader>
-              <CardContent className="space-y-3">
-                {isLoading || !data ? (
-                  <>
-                    <Skeleton className="h-10 w-full" />
-                    <Skeleton className="h-16 w-full" />
-                  </>
-                ) : (
-                  <div className="text-sm text-muted-foreground">{data.pages.length} sayfa</div>
-                )}
-              </CardContent>
-            </Card>
+            {isLoading || !data ? (
+              <div className="space-y-3 rounded-md border p-4">
+                <Skeleton className="h-10 w-full" />
+                <Skeleton className="h-24 w-full" />
+              </div>
+            ) : (
+              <PageContentEditor
+                content={data}
+                onContentChange={(nextContent) => {
+                  void mutate(nextContent, false);
+                }}
+              />
+            )}
           </TabsContent>
 
           <TabsContent value="contact">
             <Card>
               <CardHeader>
                 <CardTitle className="text-base">İletişim</CardTitle>
               </CardHeader>
               <CardContent className="grid gap-3 text-sm text-muted-foreground md:grid-cols-3">
                 {isLoading || !data ? (
                   <>
```
