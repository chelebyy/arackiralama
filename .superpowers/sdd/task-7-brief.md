### Task 7: Page Content Editor

**Files:**
- Create: `frontend/components/admin/public-content/PageContentEditor.tsx`
- Create: `frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx`
- Modify: `frontend/components/admin/public-content/PublicContentManager.tsx`
- Test: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx`

**Interfaces:**
- Consumes: `AdminPublicContent.pages`, `updateAdminPublicPageDraft`, `publishAdminPublicPage`, `unpublishAdminPublicPage`.
- Produces: page list, locale editor, block editor, draft save, publish/unpublish actions.

- [ ] **Step 1: Add page editor behavior test**

Add:

```tsx
it("saves a selected page draft", async () => {
  const user = userEvent.setup();
  updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
  getAdminPublicContentMock.mockResolvedValue(adminContentFixture);

  render(<PublicContentPage />);

  await user.click(await screen.findByRole("button", { name: /privacy/i }));
  await user.clear(screen.getByLabelText("Sayfa Başlığı"));
  await user.type(screen.getByLabelText("Sayfa Başlığı"), "Yeni Gizlilik");
  await user.click(screen.getByRole("button", { name: "Taslağı Kaydet" }));

  expect(updateAdminPublicPageDraftMock).toHaveBeenCalledWith(
    "privacy",
    "tr",
    expect.objectContaining({ title: "Yeni Gizlilik", version: "1" })
  );
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: FAIL because the page editor is not implemented.

- [ ] **Step 3: Create constrained rich text editor**

Create `ManagedContentRichTextEditor.tsx`:

```tsx
"use client";

import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import Link from "@tiptap/extension-link";
import Placeholder from "@tiptap/extension-placeholder";
import { Bold, Italic, LinkIcon, List, ListOrdered, Redo, UnderlineIcon, Undo } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type ManagedContentRichTextEditorProps = {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
};

export default function ManagedContentRichTextEditor({
  value,
  onChange,
  placeholder = "İçeriği yazın",
}: ManagedContentRichTextEditorProps) {
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({ codeBlock: false, code: false, horizontalRule: false }),
      Underline,
      Link.configure({
        openOnClick: false,
        autolink: false,
        defaultProtocol: "https",
        isAllowedUri: (url, ctx) =>
          ctx.defaultValidate(url) &&
          /^(https?:|mailto:|tel:)/i.test(url) &&
          !url.startsWith("//"),
      }),
      Placeholder.configure({ placeholder }),
    ],
    content: value || "<p></p>",
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
  });

  if (!editor) {
    return null;
  }

  return (
    <div className="rounded-md border bg-background">
      <div className="flex flex-wrap gap-1 border-b p-2">
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleBold().run()} aria-label="Kalın">
          <Bold className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleItalic().run()} aria-label="İtalik">
          <Italic className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleUnderline().run()} aria-label="Altı çizili">
          <UnderlineIcon className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleBulletList().run()} aria-label="Madde listesi">
          <List className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleOrderedList().run()} aria-label="Numaralı liste">
          <ListOrdered className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          onClick={() => {
            const href = window.prompt("Link URL");
            if (href && /^(https?:|mailto:|tel:)/i.test(href) && !href.startsWith("//")) {
              editor.chain().focus().setLink({ href }).run();
            }
          }}
          aria-label="Link ekle"
        >
          <LinkIcon className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().undo().run()} aria-label="Geri al">
          <Undo className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().redo().run()} aria-label="Yinele">
          <Redo className="h-4 w-4" />
        </Button>
      </div>
      <EditorContent editor={editor} className={cn("min-h-48 cursor-text px-3 py-2")} />
    </div>
  );
}
```

- [ ] **Step 4: Create page editor**

Create `PageContentEditor.tsx` with these core behaviors:

```tsx
"use client";

import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import type { AdminPublicContent, AdminPublicManagedPage, PublicSettingsLocale } from "@/lib/api/admin/types";
import { publishAdminPublicPage, unpublishAdminPublicPage, updateAdminPublicPageDraft } from "@/lib/api/admin/publicContent";
import ManagedContentRichTextEditor from "./ManagedContentRichTextEditor";

const locales: PublicSettingsLocale[] = ["tr", "en", "ru", "ar", "de"];

type PageContentEditorProps = {
  content: AdminPublicContent;
  onContentChange: (content: AdminPublicContent) => void;
};

export default function PageContentEditor({ content, onContentChange }: PageContentEditorProps) {
  const [selectedSlug, setSelectedSlug] = useState(content.pages[0]?.slug ?? "privacy");
  const [selectedLocale, setSelectedLocale] = useState<PublicSettingsLocale>("tr");
  const page = content.pages.find((item) => item.slug === selectedSlug && item.locale === selectedLocale);
  const [draft, setDraft] = useState<AdminPublicManagedPage | null>(page ?? null);

  const slugs = useMemo(() => Array.from(new Set(content.pages.map((item) => item.slug))).sort(), [content.pages]);

  if (!draft) {
    return <div className="rounded-md border p-4 text-sm text-muted-foreground">Düzenlenecek sayfa seçin.</div>;
  }

  async function saveDraft() {
    const next = await updateAdminPublicPageDraft(draft.slug, draft.locale as PublicSettingsLocale, {
      version: content.version,
      title: draft.title,
      subtitle: draft.subtitle,
      seoTitle: draft.seoTitle,
      seoDescription: draft.seoDescription,
      isPublished: draft.isPublished,
      sortOrder: draft.sortOrder,
      blocks: draft.blocks.map((block, index) => ({ ...block, sortOrder: index, bodyFormat: block.bodyFormat ?? "html" })),
    });
    onContentChange(next);
    toast.success("Sayfa taslağı kaydedildi.");
  }

  async function publishDraft() {
    const next = await publishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, content.version);
    onContentChange(next);
    toast.success("Sayfa yayınlandı.");
  }

  async function unpublishDraft() {
    const next = await unpublishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, content.version);
    onContentChange(next);
    toast.success("Sayfa yayından kaldırıldı.");
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
      <div className="space-y-2 rounded-md border p-3">
        {slugs.map((slug) => (
          <Button key={slug} type="button" variant={slug === selectedSlug ? "default" : "ghost"} className="w-full justify-start" onClick={() => setSelectedSlug(slug)}>
            {slug}
          </Button>
        ))}
      </div>
      <div className="space-y-4 rounded-md border p-4">
        <div className="flex flex-wrap gap-2">
          {locales.map((locale) => (
            <Button key={locale} type="button" variant={locale === selectedLocale ? "default" : "outline"} onClick={() => setSelectedLocale(locale)}>
              {locale.toUpperCase()}
            </Button>
          ))}
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="page-title">Sayfa Başlığı</Label>
            <Input id="page-title" value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} />
          </div>
          <div className="flex items-center gap-2 pt-7">
            <Switch checked={draft.isPublished} onCheckedChange={(checked) => setDraft({ ...draft, isPublished: checked })} />
            <span className="text-sm">Yayında</span>
          </div>
        </div>
        {draft.blocks.map((block, index) => (
          <div key={block.id} className="space-y-2 rounded-md border p-3">
            <Label htmlFor={`block-heading-${block.id}`}>Bölüm Başlığı</Label>
            <Input
              id={`block-heading-${block.id}`}
              value={block.heading}
              onChange={(event) =>
                setDraft({
                  ...draft,
                  blocks: draft.blocks.map((item, itemIndex) => (itemIndex === index ? { ...item, heading: event.target.value } : item)),
                })
              }
            />
            <ManagedContentRichTextEditor
              value={block.body}
              onChange={(body) =>
                setDraft({
                  ...draft,
                  blocks: draft.blocks.map((item, itemIndex) => (itemIndex === index ? { ...item, body, bodyFormat: "html" } : item)),
                })
              }
            />
          </div>
        ))}
        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={saveDraft}>Taslağı Kaydet</Button>
          <Button type="button" variant="outline" onClick={publishDraft}>Yayınla</Button>
          <Button type="button" variant="outline" onClick={unpublishDraft}>Yayından Kaldır</Button>
        </div>
      </div>
    </div>
  );
}
```

Wire it into `PublicContentManager` pages tab:

```tsx
<TabsContent value="pages">
  <PageContentEditor content={data} onContentChange={(next) => mutate(next, false)} />
</TabsContent>
```

- [ ] **Step 5: Run page editor tests**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/components/admin/public-content/PageContentEditor.tsx frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx frontend/components/admin/public-content/PublicContentManager.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
git commit -m "feat(admin): add page content editor"
```

---

