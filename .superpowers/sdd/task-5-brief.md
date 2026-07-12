### Task 5: Sanitized Public Rich Text Rendering

**Files:**
- Create: `frontend/lib/public-content/sanitize-managed-html.ts`
- Create: `frontend/lib/public-content/sanitize-managed-html.test.ts`
- Modify: `frontend/components/public/ManagedPageContent.tsx`
- Modify: `frontend/components/public/ManagedPageContent.test.tsx`
- Modify: `frontend/package.json`

**Interfaces:**
- Consumes: `PublicPageBlock.bodyFormat?: "plain" | "html"`.
- Produces: `sanitizeManagedHtml(value: string): string` and safe public rendering for rich text.

- [ ] **Step 1: Install DOMPurify if absent**

Run:

```powershell
corepack pnpm -C frontend add dompurify
corepack pnpm -C frontend add -D @types/dompurify
```

Expected: package and lockfile update. If `@types/dompurify` is unnecessary for the installed version, remove it in the same task.

- [ ] **Step 2: Add sanitizer tests**

Create `sanitize-managed-html.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { sanitizeManagedHtml } from "./sanitize-managed-html";

describe("sanitizeManagedHtml", () => {
  it("keeps approved rich text tags", () => {
    expect(sanitizeManagedHtml("<p>Hello <strong>world</strong></p>")).toBe(
      "<p>Hello <strong>world</strong></p>"
    );
  });

  it("removes script iframe style and event attributes", () => {
    const result = sanitizeManagedHtml(
      '<p style="color:red" onclick="alert(1)">Hello</p><script>alert(1)</script><iframe src="https://example.com"></iframe>'
    );

    expect(result).toBe("<p>Hello</p>");
  });

  it("removes unsafe and protocol-relative links", () => {
    expect(sanitizeManagedHtml('<a href="javascript:alert(1)">bad</a>')).toBe("<a>bad</a>");
    expect(sanitizeManagedHtml('<a href="//example.com">bad</a>')).toBe("<a>bad</a>");
  });

  it("keeps safe links with noopener noreferrer", () => {
    expect(sanitizeManagedHtml('<a href="https://example.com">safe</a>')).toContain(
      'rel="noopener noreferrer"'
    );
  });
});
```

- [ ] **Step 3: Run sanitizer test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test lib/public-content/sanitize-managed-html.test.ts
```

Expected: FAIL because sanitizer helper does not exist.

- [ ] **Step 4: Implement sanitizer helper**

Create `sanitize-managed-html.ts`:

```ts
import DOMPurify from "dompurify";

const SAFE_SCHEMES = /^(https?:|mailto:|tel:)/i;

export function sanitizeManagedHtml(value: string) {
  const clean = DOMPurify.sanitize(value, {
    ALLOWED_TAGS: [
      "p",
      "br",
      "strong",
      "em",
      "u",
      "s",
      "ul",
      "ol",
      "li",
      "blockquote",
      "h3",
      "h4",
      "a",
    ],
    ALLOWED_ATTR: ["href", "target", "rel"],
    FORBID_TAGS: ["script", "style", "iframe", "img", "object", "embed"],
    FORBID_ATTR: ["style", "class", "id"],
  });

  if (typeof window === "undefined") {
    return clean;
  }

  const template = document.createElement("template");
  template.innerHTML = clean;

  template.content.querySelectorAll("a").forEach((link) => {
    const href = link.getAttribute("href") ?? "";
    if (!SAFE_SCHEMES.test(href) || href.startsWith("//")) {
      link.removeAttribute("href");
      link.removeAttribute("target");
      link.removeAttribute("rel");
      return;
    }

    link.setAttribute("rel", "noopener noreferrer");
    if (/^https?:/i.test(href)) {
      link.setAttribute("target", "_blank");
    }
  });

  return template.innerHTML;
}
```

- [ ] **Step 5: Update public renderer**

In `ManagedPageContent.tsx`, keep existing plain text paragraph rendering and add:

```tsx
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
```

Replace the inline paragraph map with `<ManagedBlockBody block={block} />`.

- [ ] **Step 6: Add renderer tests**

Add assertions to `ManagedPageContent.test.tsx`:

```ts
it("renders html page blocks after sanitizing unsafe content", async () => {
  mockGetPublicSiteSettings.mockResolvedValue({
    pages: [
      {
        id: "tr-privacy",
        slug: "privacy",
        locale: "tr",
        title: "Privacy",
        subtitle: "",
        seoTitle: "",
        seoDescription: "",
        isPublished: true,
        sortOrder: 0,
        blocks: [
          {
            id: "block-1",
            heading: "Body",
            body: '<p>Hello <strong>safe</strong></p><script>alert(1)</script>',
            bodyFormat: "html",
            isVisible: true,
            sortOrder: 0,
          },
        ],
      },
    ],
  });

  render(<ManagedPageContent slug="privacy" />);

  expect(await screen.findByText("safe")).toBeInTheDocument();
  expect(screen.queryByText("alert(1)")).not.toBeInTheDocument();
});
```

- [ ] **Step 7: Run frontend render tests**

Run:

```powershell
corepack pnpm -C frontend test lib/public-content/sanitize-managed-html.test.ts components/public/ManagedPageContent.test.tsx
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add frontend/package.json frontend/pnpm-lock.yaml frontend/lib/public-content/sanitize-managed-html.ts frontend/lib/public-content/sanitize-managed-html.test.ts frontend/components/public/ManagedPageContent.tsx frontend/components/public/ManagedPageContent.test.tsx
git commit -m "fix(public): sanitize managed rich text content"
```

---

