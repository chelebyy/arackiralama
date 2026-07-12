# Task 5 Review Package

Base: 98b5a15
Head: b2edc448f65a01c36cbca2a46b62d1172e12a3db

## Commit Log
```
b2edc44 fix(public): sanitize managed rich text content
```

## Diff Stat
```
 .../components/public/ManagedPageContent.test.tsx  |  33 +++++++
 frontend/components/public/ManagedPageContent.tsx  |  30 ++++--
 .../public-content/sanitize-managed-html.test.ts   |  31 ++++++
 .../lib/public-content/sanitize-managed-html.ts    | 109 +++++++++++++++++++++
 frontend/package.json                              |   1 +
 frontend/pnpm-lock.yaml                            | 105 ++++++++++++++++++--
 6 files changed, 292 insertions(+), 17 deletions(-)
```

## Diff
```diff
diff --git a/frontend/components/public/ManagedPageContent.test.tsx b/frontend/components/public/ManagedPageContent.test.tsx
index 669ae0e..6b672f7 100644
--- a/frontend/components/public/ManagedPageContent.test.tsx
+++ b/frontend/components/public/ManagedPageContent.test.tsx
@@ -107,11 +107,44 @@ describe("ManagedPageContent", () => {
         managedPage("tr", "Turkish Managed Guide", "useful-guide"),
         managedPage("en", "Draft English Guide", "useful-guide", false),
       ],
     } as any);
 
     renderManagedPage("useful-guide", false);
 
     expect(await screen.findByRole("heading", { name: "Turkish Managed Guide" })).toBeInTheDocument();
     expect(screen.queryByText("Draft English Guide")).not.toBeInTheDocument();
   });
+
+  it("renders html page blocks after sanitizing unsafe content", async () => {
+    mockedGetPublicSiteSettings.mockResolvedValue({
+      pages: [
+        {
+          id: "en-privacy",
+          slug: "privacy",
+          locale: "en",
+          title: "Privacy",
+          subtitle: "",
+          seoTitle: "",
+          seoDescription: "",
+          isPublished: true,
+          sortOrder: 0,
+          blocks: [
+            {
+              id: "block-1",
+              heading: "Body",
+              body: '<p>Hello <strong>safe</strong></p><script>alert(1)</script>',
+              bodyFormat: "html",
+              isVisible: true,
+              sortOrder: 0,
+            },
+          ],
+        },
+      ],
+    } as any);
+
+    renderManagedPage();
+
+    expect(await screen.findByText("safe")).toBeInTheDocument();
+    expect(screen.queryByText("alert(1)")).not.toBeInTheDocument();
+  });
 });
diff --git a/frontend/components/public/ManagedPageContent.tsx b/frontend/components/public/ManagedPageContent.tsx
index 978f0ad..7265199 100644
--- a/frontend/components/public/ManagedPageContent.tsx
+++ b/frontend/components/public/ManagedPageContent.tsx
@@ -1,16 +1,17 @@
 "use client";
 
 import type { ReactNode } from "react";
 import useSWR from "swr";
 import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";
 import type { PublicManagedPage } from "@/lib/api/admin/types";
+import { sanitizeManagedHtml } from "@/lib/public-content/sanitize-managed-html";
 
 type ManagedPageContentProps = {
   slug: string;
   children?: ReactNode;
 };
 
 function findPage(
   pages: PublicManagedPage[] | undefined,
   slug: string,
   locale: string,
@@ -53,20 +54,41 @@ function NotPublishedPage() {
         <p className="text-sm font-semibold text-[#0369A1]">404</p>
         <h1 className="mt-3 text-3xl font-bold text-[#0F172A]">Sayfa yayında değil</h1>
         <p className="mt-4 text-[#64748B]">
           Bu public sayfa admin panelinden yayından kaldırılmış veya henüz yayınlanmamış.
         </p>
       </div>
     </div>
   );
 }
 
+function ManagedBlockBody({ block }: { block: PublicManagedPage["blocks"][number] }) {
+  if (block.bodyFormat === "html") {
+    return (
+      <div
+        className="space-y-4 text-[#475569] [&_a]:font-semibold [&_a]:text-[#0369A1] [&_blockquote]:border-l-4 [&_blockquote]:border-[#CBD5E1] [&_blockquote]:pl-4"
+        dangerouslySetInnerHTML={{ __html: sanitizeManagedHtml(block.body) }}
+      />
+    );
+  }
+
+  return (
+    <div className="space-y-4 text-[#475569]">
+      {splitParagraphs(block.body).map((paragraph) => (
+        <p key={paragraph.slice(0, 48)} className="leading-relaxed">
+          {paragraph}
+        </p>
+      ))}
+    </div>
+  );
+}
+
 function ManagedPage({ page }: { page: PublicManagedPage }) {
   const visibleBlocks = page.blocks
     .filter((block) => block.isVisible)
     .sort((a, b) => a.sortOrder - b.sortOrder);
 
   return (
     <div className="min-h-screen bg-[#F8FAFC]">
       <div className="bg-[#0F172A] py-16 lg:py-24">
         <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
           <div className="mx-auto max-w-3xl text-center">
@@ -80,27 +102,21 @@ function ManagedPage({ page }: { page: PublicManagedPage }) {
             )}
           </div>
         </div>
       </div>
 
       <div className="mx-auto max-w-4xl px-4 py-16 sm:px-6 lg:px-8 lg:py-24">
         <div className="space-y-8">
           {visibleBlocks.map((block) => (
             <section key={block.id} className="rounded-2xl border border-[#E2E8F0] bg-white p-6 lg:p-8">
               <h2 className="mb-4 text-2xl font-bold text-[#0F172A]">{block.heading}</h2>
-              <div className="space-y-4 text-[#475569]">
-                {splitParagraphs(block.body).map((paragraph) => (
-                  <p key={paragraph.slice(0, 48)} className="leading-relaxed">
-                    {paragraph}
-                  </p>
-                ))}
-              </div>
+              <ManagedBlockBody block={block} />
             </section>
           ))}
         </div>
       </div>
     </div>
   );
 }
 
 export default function ManagedPageContent({ slug, children }: ManagedPageContentProps) {
   const locale = getCurrentLocale();
diff --git a/frontend/lib/public-content/sanitize-managed-html.test.ts b/frontend/lib/public-content/sanitize-managed-html.test.ts
new file mode 100644
index 0000000..b85eb6e
--- /dev/null
+++ b/frontend/lib/public-content/sanitize-managed-html.test.ts
@@ -0,0 +1,31 @@
+import { describe, expect, it } from "vitest";
+
+import { sanitizeManagedHtml } from "./sanitize-managed-html";
+
+describe("sanitizeManagedHtml", () => {
+  it("keeps approved rich text tags", () => {
+    expect(sanitizeManagedHtml("<p>Hello <strong>world</strong></p>")).toBe(
+      "<p>Hello <strong>world</strong></p>",
+    );
+  });
+
+  it("removes script iframe style and event attributes", () => {
+    const result = sanitizeManagedHtml(
+      '<p style="color:red" onclick="alert(1)">Hello</p><script>alert(1)</script><iframe src="https://example.com"></iframe>',
+    );
+
+    expect(result).toBe("<p>Hello</p>");
+  });
+
+  it("removes unsafe and protocol-relative links", () => {
+    expect(sanitizeManagedHtml('<a href="javascript:alert(1)">bad</a>')).toBe("<a>bad</a>");
+    expect(sanitizeManagedHtml('<a href="//example.com">bad</a>')).toBe("<a>bad</a>");
+  });
+
+  it("keeps safe links with noopener noreferrer", () => {
+    const result = sanitizeManagedHtml('<a href="https://example.com">safe</a>');
+
+    expect(result).toContain('rel="noopener noreferrer"');
+    expect(result).toContain('target="_blank"');
+  });
+});
diff --git a/frontend/lib/public-content/sanitize-managed-html.ts b/frontend/lib/public-content/sanitize-managed-html.ts
new file mode 100644
index 0000000..15a9d0d
--- /dev/null
+++ b/frontend/lib/public-content/sanitize-managed-html.ts
@@ -0,0 +1,109 @@
+import DOMPurify from "dompurify";
+
+const ALLOWED_TAGS = [
+  "p",
+  "br",
+  "strong",
+  "em",
+  "u",
+  "s",
+  "ul",
+  "ol",
+  "li",
+  "blockquote",
+  "h3",
+  "h4",
+  "a",
+];
+
+const ALLOWED_ATTR = ["href", "target", "rel"];
+const FORBID_TAGS = ["script", "style", "iframe", "img", "object", "embed"];
+const FORBID_ATTR = ["style", "class", "id"];
+const SAFE_EXTERNAL_LINK = /^https?:/i;
+const SAFE_CONTACT_LINK = /^(mailto|tel):/i;
+const HAS_PROTOCOL = /^[a-z][a-z0-9+.-]*:/i;
+
+type ManagedDOMPurify = {
+  sanitize: (
+    value: string,
+    config: {
+      ALLOWED_TAGS: string[];
+      ALLOWED_ATTR: string[];
+      FORBID_TAGS: string[];
+      FORBID_ATTR: string[];
+    },
+  ) => string;
+};
+
+type DOMPurifyFactory = ((window: Window) => ManagedDOMPurify) & Partial<ManagedDOMPurify>;
+
+function getDOMPurify() {
+  if (typeof window === "undefined") {
+    return null;
+  }
+
+  const purifier = DOMPurify as unknown as DOMPurifyFactory;
+
+  if (typeof purifier.sanitize === "function") {
+    return purifier as ManagedDOMPurify;
+  }
+
+  return purifier(window);
+}
+
+function getSafeHrefType(href: string) {
+  const trimmedHref = href.trim();
+
+  if (!trimmedHref || trimmedHref.startsWith("//")) {
+    return null;
+  }
+
+  if (SAFE_EXTERNAL_LINK.test(trimmedHref)) {
+    return "http";
+  }
+
+  if (SAFE_CONTACT_LINK.test(trimmedHref) || !HAS_PROTOCOL.test(trimmedHref)) {
+    return "non-http";
+  }
+
+  return null;
+}
+
+export function sanitizeManagedHtml(value: string): string {
+  const purifier = getDOMPurify();
+
+  if (!purifier) {
+    return "";
+  }
+
+  const clean = purifier.sanitize(value, {
+    ALLOWED_TAGS,
+    ALLOWED_ATTR,
+    FORBID_TAGS,
+    FORBID_ATTR,
+  });
+
+  const template = document.createElement("template");
+  template.innerHTML = clean;
+
+  template.content.querySelectorAll("a").forEach((link) => {
+    const hrefType = getSafeHrefType(link.getAttribute("href") ?? "");
+
+    if (!hrefType) {
+      link.removeAttribute("href");
+      link.removeAttribute("target");
+      link.removeAttribute("rel");
+      return;
+    }
+
+    link.setAttribute("rel", "noopener noreferrer");
+
+    if (hrefType === "http") {
+      link.setAttribute("target", "_blank");
+    } else {
+      link.removeAttribute("target");
+    }
+  });
+
+  return template.innerHTML;
+}
diff --git a/frontend/package.json b/frontend/package.json
index 3003123..7530a10 100644
--- a/frontend/package.json
+++ b/frontend/package.json
@@ -71,20 +71,21 @@
     "@tiptap/extension-typography": "^3.22.5",
     "@tiptap/extension-underline": "^3.22.5",
     "@tiptap/pm": "^3.22.5",
     "@tiptap/react": "^3.22.5",
     "@tiptap/starter-kit": "^3.27.1",
     "@uidotdev/usehooks": "^2.4.1",
     "class-variance-authority": "^0.7.1",
     "clsx": "^2.1.1",
     "cmdk": "^1.1.1",
     "date-fns": "^4.4.0",
+    "dompurify": "^3.4.11",
     "dotenv": "^17.4.2",
     "embla-carousel-react": "^8.6.0",
     "input-otp": "^1.4.2",
     "lottie-react": "^2.4.1",
     "lowlight": "^3.3.0",
     "lucide-react": "^0.577.0",
     "marked": "^17.0.6",
     "motion": "^12.40.0",
     "next": "16.2.9",
     "next-intl": "^4.13.0",
diff --git a/frontend/pnpm-lock.yaml b/frontend/pnpm-lock.yaml
index 8fd44de..f3c6717 100644
--- a/frontend/pnpm-lock.yaml
+++ b/frontend/pnpm-lock.yaml
@@ -144,21 +144,21 @@ importers:
         specifier: ^4.9.0
         version: 4.9.0(react@19.2.7)
       '@tanstack/react-table':
         specifier: ^8.21.3
         version: 8.21.3(react-dom@19.2.7(react@19.2.7))(react@19.2.7)
       '@tiptap/core':
         specifier: ^3.22.5
         version: 3.22.5(@tiptap/pm@3.22.5)
       '@tiptap/extension-code-block-lowlight':
         specifier: ^3.22.5
-        version: 3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/extension-code-block@3.27.1(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)(highlight.js@11.11.1)(lowlight@3.3.0)
+        version: 3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/extension-code-block@3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)(highlight.js@11.11.1)(lowlight@3.3.0)
       '@tiptap/extension-color':
         specifier: ^3.22.5
         version: 3.22.5(@tiptap/extension-text-style@3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5)))
       '@tiptap/extension-heading':
         specifier: ^3.22.5
         version: 3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))
       '@tiptap/extension-highlight':
         specifier: ^3.22.5
         version: 3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))
       '@tiptap/extension-horizontal-rule':
@@ -208,20 +208,23 @@ importers:
         version: 0.7.1
       clsx:
         specifier: ^2.1.1
         version: 2.1.1
       cmdk:
         specifier: ^1.1.1
         version: 1.1.1(@types/react-dom@19.2.3(@types/react@19.2.17))(@types/react@19.2.17)(react-dom@19.2.7(react@19.2.7))(react@19.2.7)
       date-fns:
         specifier: ^4.4.0
         version: 4.4.0
+      dompurify:
+        specifier: ^3.4.11
+        version: 3.4.11
       dotenv:
         specifier: ^17.4.2
         version: 17.4.2
       embla-carousel-react:
         specifier: ^8.6.0
         version: 8.6.0(react@19.2.7)
       input-otp:
         specifier: ^1.4.2
         version: 1.4.2(react-dom@19.2.7(react@19.2.7))(react@19.2.7)
       lottie-react:
@@ -920,103 +923,119 @@ packages:
 
   '@img/sharp-libvips-darwin-x64@1.2.4':
     resolution: {integrity: sha512-1IOd5xfVhlGwX+zXv2N93k0yMONvUlANylbJw1eTah8K/Jtpi15KC+WSiaX/nBmbm2HxRM1gZ0nSdjSsrZbGKg==}
     cpu: [x64]
     os: [darwin]
 
   '@img/sharp-libvips-linux-arm64@1.2.4':
     resolution: {integrity: sha512-excjX8DfsIcJ10x1Kzr4RcWe1edC9PquDRRPx3YVCvQv+U5p7Yin2s32ftzikXojb1PIFc/9Mt28/y+iRklkrw==}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-libvips-linux-arm@1.2.4':
     resolution: {integrity: sha512-bFI7xcKFELdiNCVov8e44Ia4u2byA+l3XtsAj+Q8tfCwO6BQ8iDojYdvoPMqsKDkuoOo+X6HZA0s0q11ANMQ8A==}
     cpu: [arm]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-libvips-linux-ppc64@1.2.4':
     resolution: {integrity: sha512-FMuvGijLDYG6lW+b/UvyilUWu5Ayu+3r2d1S8notiGCIyYU/76eig1UfMmkZ7vwgOrzKzlQbFSuQfgm7GYUPpA==}
     cpu: [ppc64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-libvips-linux-riscv64@1.2.4':
     resolution: {integrity: sha512-oVDbcR4zUC0ce82teubSm+x6ETixtKZBh/qbREIOcI3cULzDyb18Sr/Wcyx7NRQeQzOiHTNbZFF1UwPS2scyGA==}
     cpu: [riscv64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-libvips-linux-s390x@1.2.4':
     resolution: {integrity: sha512-qmp9VrzgPgMoGZyPvrQHqk02uyjA0/QrTO26Tqk6l4ZV0MPWIW6LTkqOIov+J1yEu7MbFQaDpwdwJKhbJvuRxQ==}
     cpu: [s390x]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-libvips-linux-x64@1.2.4':
     resolution: {integrity: sha512-tJxiiLsmHc9Ax1bz3oaOYBURTXGIRDODBqhveVHonrHJ9/+k89qbLl0bcJns+e4t4rvaNBxaEZsFtSfAdquPrw==}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-libvips-linuxmusl-arm64@1.2.4':
     resolution: {integrity: sha512-FVQHuwx1IIuNow9QAbYUzJ+En8KcVm9Lk5+uGUQJHaZmMECZmOlix9HnH7n1TRkXMS0pGxIJokIVB9SuqZGGXw==}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@img/sharp-libvips-linuxmusl-x64@1.2.4':
     resolution: {integrity: sha512-+LpyBk7L44ZIXwz/VYfglaX/okxezESc6UxDSoyo2Ks6Jxc4Y7sGjpgU9s4PMgqgjj1gZCylTieNamqA1MF7Dg==}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@img/sharp-linux-arm64@0.34.5':
     resolution: {integrity: sha512-bKQzaJRY/bkPOXyKx5EVup7qkaojECG6NLYswgktOZjaXecSAeCWiZwwiFf3/Y+O1HrauiE3FVsGxFg8c24rZg==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-linux-arm@0.34.5':
     resolution: {integrity: sha512-9dLqsvwtg1uuXBGZKsxem9595+ujv0sJ6Vi8wcTANSFpwV/GONat5eCkzQo/1O6zRIkh0m/8+5BjrRr7jDUSZw==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [arm]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-linux-ppc64@0.34.5':
     resolution: {integrity: sha512-7zznwNaqW6YtsfrGGDA6BRkISKAAE1Jo0QdpNYXNMHu2+0dTrPflTLNkpc8l7MUP5M16ZJcUvysVWWrMefZquA==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [ppc64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-linux-riscv64@0.34.5':
     resolution: {integrity: sha512-51gJuLPTKa7piYPaVs8GmByo7/U7/7TZOq+cnXJIHZKavIRHAP77e3N2HEl3dgiqdD/w0yUfiJnII77PuDDFdw==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [riscv64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-linux-s390x@0.34.5':
     resolution: {integrity: sha512-nQtCk0PdKfho3eC5MrbQoigJ2gd1CgddUMkabUj+rBevs8tZ2cULOx46E7oyX+04WGfABgIwmMC0VqieTiR4jg==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [s390x]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-linux-x64@0.34.5':
     resolution: {integrity: sha512-MEzd8HPKxVxVenwAa+JRPwEC7QFjoPWuS5NZnBt6B3pu7EG2Ge0id1oLHZpPJdn3OQK+BQDiw9zStiHBTJQQQQ==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@img/sharp-linuxmusl-arm64@0.34.5':
     resolution: {integrity: sha512-fprJR6GtRsMt6Kyfq44IsChVZeGN97gTD331weR1ex1c1rypDEABN6Tm2xa1wE6lYb5DdEnk03NZPqA7Id21yg==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@img/sharp-linuxmusl-x64@0.34.5':
     resolution: {integrity: sha512-Jg8wNT1MUzIvhBFxViqrEhWDGzqymo3sV7z7ZsaWbZNDLXRJZoRGrjulp60YYtV4wfY8VIKcWidjojlLcWrd8Q==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@img/sharp-wasm32@0.34.5':
     resolution: {integrity: sha512-OdWTEiVkY2PHwqkbBI8frFxQQFekHaSSkUIJkwzclWZe64O1X4UlUjqqqLaPbUpMOQk6FBu/HtlGXNblIs0huw==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [wasm32]
 
   '@img/sharp-win32-arm64@0.34.5':
     resolution: {integrity: sha512-WQ3AgWCWYSb2yt+IG8mnC6Jdk9Whs7O0gxphblsLvdhSpSTtmu69ZG1Gkb6NuvxsNACwiPV6cNSZNzt0KPsw7g==}
     engines: {node: ^18.17.0 || ^20.3.0 || >=21.0.0}
     cpu: [arm64]
@@ -1072,38 +1091,42 @@ packages:
     resolution: {integrity: sha512-7IAtK4MeybpqRV9GRABWEhJ62mOS+rzWOzOTFie4cSEtm12xsoOMJRcECoZx3FHPzFAqN/IJtHqWAFOLfl152w==}
     engines: {node: '>= 10'}
     cpu: [x64]
     os: [darwin]
 
   '@next/swc-linux-arm64-gnu@16.2.9':
     resolution: {integrity: sha512-hBD75iWpUtkL9SmQmcRhmLomn9jgkPzCEkbOcLgHymPEKzv+6ONy13RRiIEz/iEObjkS2Jlb5gYS2XGoS3X4rw==}
     engines: {node: '>= 10'}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@next/swc-linux-arm64-musl@16.2.9':
     resolution: {integrity: sha512-qZTI3pf9SGc/obr8NkQAekBxmp1QK+kVm+VAf3BALLfFAj+1kUhkTxmrWpVos9R/UYIA8AWX2p6cGI5WdwzVUA==}
     engines: {node: '>= 10'}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@next/swc-linux-x64-gnu@16.2.9':
     resolution: {integrity: sha512-xm0HfRNX+UkH4R3c18ynswjj5o5uEj/7iI9p9omdtTSIsRCzQqkGMA+10nzJ4EHnYC3as65IMhbbl5fWRUWHYg==}
     engines: {node: '>= 10'}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@next/swc-linux-x64-musl@16.2.9':
     resolution: {integrity: sha512-QumimHkGEG6vM3PfEDWKyKen03NcqLOkeKB1EfcPe7VxzmEiCa4jNnMyBn/US5zcd/VE1CI+O8Ovb3lfjVHfGw==}
     engines: {node: '>= 10'}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@next/swc-win32-arm64-msvc@16.2.9':
     resolution: {integrity: sha512-hzQpKZvw8rAwI6A2uQh6SacCSvNAXaIkPNsWwzqqfRiIMiXMfH936skDhz1OO6KpvdKkJrgHHtqQOq5PIXOvdQ==}
     engines: {node: '>= 10'}
     cpu: [arm64]
     os: [win32]
 
   '@next/swc-win32-x64-msvc@16.2.9':
     resolution: {integrity: sha512-qr2VL3Ce5QrwgO2yh1ujSBawrimjVKX8FGF/cOynmdYKJY0BdHpGVNIRK1tqONB10Vkm25Ub1BD2bkjWs4+96w==}
     engines: {node: '>= 10'}
@@ -1148,50 +1171,56 @@ packages:
     resolution: {integrity: sha512-vJVi8yd/qzJxEKHkeemh7w3YAn6RJCtYlE4HPMoVnCpIXEzSrxErBW5SJBgKLbXU3WdIpkjBTeUNtyBVn8TRng==}
     engines: {node: '>= 10.0.0'}
     cpu: [x64]
     os: [freebsd]
 
   '@parcel/watcher-linux-arm-glibc@2.5.6':
     resolution: {integrity: sha512-9JiYfB6h6BgV50CCfasfLf/uvOcJskMSwcdH1PHH9rvS1IrNy8zad6IUVPVUfmXr+u+Km9IxcfMLzgdOudz9EQ==}
     engines: {node: '>= 10.0.0'}
     cpu: [arm]
     os: [linux]
+    libc: [glibc]
 
   '@parcel/watcher-linux-arm-musl@2.5.6':
     resolution: {integrity: sha512-Ve3gUCG57nuUUSyjBq/MAM0CzArtuIOxsBdQ+ftz6ho8n7s1i9E1Nmk/xmP323r2YL0SONs1EuwqBp2u1k5fxg==}
     engines: {node: '>= 10.0.0'}
     cpu: [arm]
     os: [linux]
+    libc: [musl]
 
   '@parcel/watcher-linux-arm64-glibc@2.5.6':
     resolution: {integrity: sha512-f2g/DT3NhGPdBmMWYoxixqYr3v/UXcmLOYy16Bx0TM20Tchduwr4EaCbmxh1321TABqPGDpS8D/ggOTaljijOA==}
     engines: {node: '>= 10.0.0'}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@parcel/watcher-linux-arm64-musl@2.5.6':
     resolution: {integrity: sha512-qb6naMDGlbCwdhLj6hgoVKJl2odL34z2sqkC7Z6kzir8b5W65WYDpLB6R06KabvZdgoHI/zxke4b3zR0wAbDTA==}
     engines: {node: '>= 10.0.0'}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@parcel/watcher-linux-x64-glibc@2.5.6':
     resolution: {integrity: sha512-kbT5wvNQlx7NaGjzPFu8nVIW1rWqV780O7ZtkjuWaPUgpv2NMFpjYERVi0UYj1msZNyCzGlaCWEtzc+exjMGbQ==}
     engines: {node: '>= 10.0.0'}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@parcel/watcher-linux-x64-musl@2.5.6':
     resolution: {integrity: sha512-1JRFeC+h7RdXwldHzTsmdtYR/Ku8SylLgTU/reMuqdVD7CtLwf0VR1FqeprZ0eHQkO0vqsbvFLXUmYm/uNKJBg==}
     engines: {node: '>= 10.0.0'}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@parcel/watcher-win32-arm64@2.5.6':
     resolution: {integrity: sha512-3ukyebjc6eGlw9yRt678DxVF7rjXatWiHvTXqphZLvo7aC5NdEgFufVwjFfY51ijYEWpXbqF5jtrK275z52D4Q==}
     engines: {node: '>= 10.0.0'}
     cpu: [arm64]
     os: [win32]
 
   '@parcel/watcher-win32-ia32@2.5.6':
     resolution: {integrity: sha512-k35yLp1ZMwwee3Ez/pxBi5cf4AoBKYXj00CZ80jUz5h8prpiaQsiRPKQMxoLstNuqe2vR4RNPEAEcjEFzhEz/g==}
     engines: {node: '>= 10.0.0'}
@@ -2274,24 +2303,24 @@ packages:
         optional: true
 
   '@radix-ui/react-visually-hidden@1.2.6':
     resolution: {integrity: sha512-jCE0WljWifTI4niIMCll06kGpsJTAPiZVU9H4WR1N6qW7At9ystHbN7dDB+we2xH535roFHj7qKS+RGj0FMDWQ==}
     peerDependencies:
       '@types/react': '*'
       '@types/react-dom': '*'
       react: ^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc
       react-dom: ^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc
     peerDependenciesMeta:
-       '@types/react':
-         optional: true
-       '@types/react-dom':
-         optional: true
+      '@types/react':
+        optional: true
+      '@types/react-dom':
+        optional: true
 
   '@radix-ui/rect@1.1.1':
     resolution: {integrity: sha512-HPwpGIzkl28mWyZqG52jiqDJ12waP11Pa1lGoiyUkIEuMLBP0oeK/C89esbXrxsky5we7dfd8U58nm0SgAWpVw==}
 
   '@radix-ui/rect@1.1.2':
     resolution: {integrity: sha512-xnXE7wG13PI+cxieVssYXlQJuYVRhH9NBoxt3KNwzghDIA69GMm7d4wXRouHIYjE+KvS6U/MsMO73NdS2MH9ZA==}
 
   '@remixicon/react@4.9.0':
     resolution: {integrity: sha512-5/jLDD4DtKxH2B4QVXTobvV1C2uL8ab9D5yAYNtFt+w80O0Ys1xFOrspqROL3fjrZi+7ElFUWE37hBfaAl6U+Q==}
     peerDependencies:
@@ -2327,80 +2356,93 @@ packages:
 
   '@rollup/rollup-freebsd-x64@4.62.2':
     resolution: {integrity: sha512-6nU5F2wCW+qvCBhTn1pdIU3bzsIoF7EUwsCDRxilWGprQR6yd508YnH9+OKFCwpfS8pjZqDUmnCAr7exax0XCg==}
     cpu: [x64]
     os: [freebsd]
 
   '@rollup/rollup-linux-arm-gnueabihf@4.62.2':
     resolution: {integrity: sha512-n1GJHPOvpIfhi3TmrCeh6S6URt9BFCt0KQE3qvexyGCTAKpR4Lg+eWvNZEqu7epxwus/8ElT3hacYEucm49SZg==}
     cpu: [arm]
     os: [linux]
+    libc: [glibc]
 
   '@rollup/rollup-linux-arm-musleabihf@4.62.2':
     resolution: {integrity: sha512-JqgflS8wEB+UXV/vS1RpRbifGBeN4D5lz8D8oOFbFZw4vedvdOgCFAjfBmIMdW3yL10XpQQ0Ambepw6MXrhOnA==}
     cpu: [arm]
     os: [linux]
+    libc: [musl]
 
   '@rollup/rollup-linux-arm64-gnu@4.62.2':
     resolution: {integrity: sha512-wnFJkogWvN4jm/hQRF2UBaeUmk20j5+DmHvoyWii2b8HJDyvz1MF2OU/6ynXt2KR63rbZLWkFpoytpdc/yBuSA==}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@rollup/rollup-linux-arm64-musl@4.62.2':
     resolution: {integrity: sha512-HVu2bp0zhvJ8xHEV9+UUs7S90VadmBSY3LcIMvozbPo4AuMGDWlz3ymHLHZPX4hR67TKTt8Qp5PJ5RBg/i+RMQ==}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@rollup/rollup-linux-loong64-gnu@4.62.2':
     resolution: {integrity: sha512-mQqqAV8QaoSgr9I2fKDLY2BAVvmKjWoGiu/cSYQonsLvtqwEn1E4QYfnCOcp5zoEqNhsDYin1s6jx/VJmrxlZg==}
     cpu: [loong64]
     os: [linux]
+    libc: [glibc]
 
   '@rollup/rollup-linux-loong64-musl@4.62.2':
     resolution: {integrity: sha512-IxKLoxCQ2IWi6bT2akyDUBGsOImDKB+sPp4EsTmwFQ/fMwpCKm8uLSSgP/Kx/QYUgKis6SEZ5/Nlhup0DIA0PQ==}
     cpu: [loong64]
     os: [linux]
+    libc: [musl]
 
   '@rollup/rollup-linux-ppc64-gnu@4.62.2':
     resolution: {integrity: sha512-Mk5ha2RQSgyFfmYYLkBpPnUk8D8FriBxesO1u9O75X0mHgXL1UQcH5Itl2lurWL2tj0RxV9b9tJgipac0hRY9A==}
     cpu: [ppc64]
     os: [linux]
+    libc: [glibc]
 
   '@rollup/rollup-linux-ppc64-musl@4.62.2':
     resolution: {integrity: sha512-CjvEnqJL/0/TQ3TXX3OPIJ/kmBellrWd4heXUmHeJlTnmwjKpSJzoehLaL6Xk0ZnMHBu9dZuFADNOrtjF4v+2w==}
     cpu: [ppc64]
     os: [linux]
+    libc: [musl]
 
   '@rollup/rollup-linux-riscv64-gnu@4.62.2':
     resolution: {integrity: sha512-1SiZbzwdkaDURsew/tSOrooKiYy7EQGT6m8ufavAi9NEyQb/6VuIxFXAL1fqa4iZe3g4NbNk4P7J32z2tw5Mgg==}
     cpu: [riscv64]
     os: [linux]
+    libc: [glibc]
 
   '@rollup/rollup-linux-riscv64-musl@4.62.2':
     resolution: {integrity: sha512-nQts12zJ3NQRoE6uYljOH89v7szzLDvG2JD/vsX+vGXU8w/At1GowTZ5/7qeFQ8m7L55rpR8Okugnuo5bgjy2Q==}
     cpu: [riscv64]
     os: [linux]
+    libc: [musl]
 
   '@rollup/rollup-linux-s390x-gnu@4.62.2':
     resolution: {integrity: sha512-E9/ll019jhPIJgpzfZoIkBGhcz+kKNgVWYRY0zr9srBdPPFVpvOKW8VaJKUbeK+eZXyQF9ltME+Kk6affeaPgg==}
     cpu: [s390x]
     os: [linux]
+    libc: [glibc]
 
   '@rollup/rollup-linux-x64-gnu@4.62.2':
     resolution: {integrity: sha512-5BqxR/pshjey51iliyzTD5Xi3EN0aLmQ2lZ3lvefVV9c82BvrLo2/6OT55iifpWBufs6kdwWbuOKS841DrmK9A==}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@rollup/rollup-linux-x64-musl@4.62.2':
     resolution: {integrity: sha512-uNN83XxQrRAh/w0/pmAfibcwyb6YWt4gP+dpnQKPVJshAloQ785ii8CT8ZCIxkGg9opVsvAlGhFitSm6D1Jjpg==}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@rollup/rollup-openbsd-x64@4.62.2':
     resolution: {integrity: sha512-srjEIxSH3LRnJN6THczDHWQplqEMFiAJrTab0msUryh9kwNpkICf3Ea6q6MN/2cZwRFUNx5w+h6Hpi4QuHS6Zg==}
     cpu: [x64]
     os: [openbsd]
 
   '@rollup/rollup-openharmony-arm64@4.62.2':
     resolution: {integrity: sha512-8hOJnxgbyObnCm5AlRA3A931xX19xq80RjVTKgJOvEKWqJruP/Uf12IbAOaDjjEXYRewwHLfmF0YRIdK3OwKWA==}
     cpu: [arm64]
     os: [openharmony]
@@ -2484,50 +2526,56 @@ packages:
     resolution: {integrity: sha512-SlRZsCjOCPR2LvFs0Ri/Xrx/5o5TCt8vl4gW6mX1hEZOG0a625RxzRHpHdAQNGykmAN/7IeaFAJG+QnNmxlHcA==}
     engines: {node: '>=10'}
     cpu: [arm]
     os: [linux]
 
   '@swc/core-linux-arm64-gnu@1.15.40':
     resolution: {integrity: sha512-Q8byxJt2fh8CR3EUX6snBpy47AoBVm+In/+Z3rjDHMjC38ZvR9/gtUUNCT0tfrn4EdVsO8/QPi59nxrxvqxvBQ==}
     engines: {node: '>=10'}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@swc/core-linux-arm64-musl@1.15.40':
     resolution: {integrity: sha512-4z0MgHU+7M0pZDqBN1El7mFXDI1SBwinfcUkAyA4v8QrhOIUOZltySt2aStQLZGrdXVXM4Y4ylfiTC04ED+MoQ==}
     engines: {node: '>=10'}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@swc/core-linux-ppc64-gnu@1.15.40':
     resolution: {integrity: sha512-fLI4iUgeSZu0eRWUXwe6YzPFx9gHbFiPkl8Rp3mJfP8OpNR3nTQCGPvHdDh9xniW7mVvgMY4ni7A4VzqI1KrpA==}
     engines: {node: '>=10'}
     cpu: [ppc64]
     os: [linux]
+    libc: [glibc]
 
   '@swc/core-linux-s390x-gnu@1.15.40':
     resolution: {integrity: sha512-YqeKMAb7d4nQSGMJQ454IlaCENpzcDqhvBE9+CPfdnYpnUXxd+BSrB6Xk0YjW8UyoEhUj4p6quATCxbsp6J3jg==}
     engines: {node: '>=10'}
     cpu: [s390x]
     os: [linux]
+    libc: [glibc]
 
   '@swc/core-linux-x64-gnu@1.15.40':
     resolution: {integrity: sha512-7HOuS1iGcme/j/TuL1TfmmLGiMQrjv/GmjyZeydl00FKPtpGXEldwqfI56xgd1YzrzoB2svWjxbGGyQ0TEASxg==}
     engines: {node: '>=10'}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@swc/core-linux-x64-musl@1.15.40':
     resolution: {integrity: sha512-h4kZYHc7dpc9P9u4brRJaS8Pl7tPVHAeiLSzw7T5RfIJgAoSdaCMKzI/2Uay9gFhaw8uyCDl0L5q37r0EpAfIA==}
     engines: {node: '>=10'}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@swc/core-win32-arm64-msvc@1.15.40':
     resolution: {integrity: sha512-+mQgKZXSj6mV38Zh05QaxSjUDmGP/R2JWlXZTDLSPkDzHU6p3GxN9eeSf5dfyDVU86946fmCvSzyl/ucImx8+A==}
     engines: {node: '>=10'}
     cpu: [arm64]
     os: [win32]
 
   '@swc/core-win32-ia32-msvc@1.15.40':
     resolution: {integrity: sha512-yvwdPLGd25mcj/mNatjNQ0lZujtQD6psH3v9PNmMb+fSzjbNG8KIDxjFWrcV+fsFVLOkyOmdJsFmX7NAFjVyPw==}
     engines: {node: '>=10'}
@@ -2589,38 +2637,42 @@ packages:
     resolution: {integrity: sha512-/Ah/xik0LaMYfv9DZ0S/t4pBlBNYOcqtRwusjgovHkvT8ixueWCLyJjsaF5kQIckjb4IT8Q6K6p/iPmZMixYgg==}
     engines: {node: '>= 20'}
     cpu: [arm]
     os: [linux]
 
   '@tailwindcss/oxide-linux-arm64-gnu@4.3.1':
     resolution: {integrity: sha512-gqdFoVJlw444GvpnheZLHmvTzSxI/cOUUh2KSNejQjTcYkW062SVD+En0rUgD+QV91bz1XGIGtt1HJd48xUGbQ==}
     engines: {node: '>= 20'}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@tailwindcss/oxide-linux-arm64-musl@4.3.1':
     resolution: {integrity: sha512-Bwv9KwOvE0VKa86xPFif9b9c3Y1NxOV1P0gLti/IYaWEsQYZXDlxfGEtA8mdDZ7SG3wyNXAWYT5SIn3giL57oA==}
     engines: {node: '>= 20'}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@tailwindcss/oxide-linux-x64-gnu@4.3.1':
     resolution: {integrity: sha512-Ymi8O8T15HYQdOUWUtTI6ldN0neHP85FC+Qz32xTcZ7iJXtem/x8ITev0o1e9e5rkqj4lONZfTRLvkmin1+tKg==}
     engines: {node: '>= 20'}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@tailwindcss/oxide-linux-x64-musl@4.3.1':
     resolution: {integrity: sha512-M+P/91qJ6uILLw4k2G93GMDRAXj61SMvFQYt39AqvUqYgExXpLL5aepfns7sj4HiAQeolirQF9E0lzRvdf4zPQ==}
     engines: {node: '>= 20'}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@tailwindcss/oxide-wasm32-wasi@4.3.1':
     resolution: {integrity: sha512-zsM8uOeqvVGHsAXsJxsT28ttosFahLJKCLOTUBqRAtKnVgGSRitds9T432QiT8b77Yga7JIBkulIRRlJPtYhRA==}
     engines: {node: '>=14.0.0'}
     cpu: [wasm32]
     bundledDependencies:
       - '@napi-rs/wasm-runtime'
       - '@emnapi/core'
       - '@emnapi/runtime'
       - '@tybys/wasm-util'
@@ -2719,20 +2771,26 @@ packages:
 
   '@tiptap/extension-code-block-lowlight@3.22.5':
     resolution: {integrity: sha512-lT0SxhjkDL1tKSeVDduV+SJ6kHdNFcbYBaUAwTufRtDt8FIYcSX6tWj5cPEXOFrC0PlJu7ybCnTEbXBdFP8Bnw==}
     peerDependencies:
       '@tiptap/core': 3.22.5
       '@tiptap/extension-code-block': 3.22.5
       '@tiptap/pm': 3.22.5
       highlight.js: ^11
       lowlight: ^2 || ^3
 
+  '@tiptap/extension-code-block@3.22.5':
+    resolution: {integrity: sha512-d123kCfLdJTi4fue1m0+TNFztDkmIRSZGZmGu6H9KqwG5Q7IzjT9o8lzRsz+pXxYqHvqgYmXoEpM6srbzXx/Ag==}
+    peerDependencies:
+      '@tiptap/core': 3.22.5
+      '@tiptap/pm': 3.22.5
+
   '@tiptap/extension-code-block@3.27.1':
     resolution: {integrity: sha512-pHlzmZx2OlHfyQ0yRlT5UL4mGokz947DthZuYefN1OleVqOkHpWBG+2JQwqoNq6bmzMne92zbH32rhcJUEYSjA==}
     peerDependencies:
       '@tiptap/core': 3.27.1
       '@tiptap/pm': 3.27.1
 
   '@tiptap/extension-code@3.27.1':
     resolution: {integrity: sha512-epOUpFfEmBzjvnqvjv2qHX7NAuLo5dlOGV690lWu+sAYMjibuJBeVvAiKPyFCfRCCTUxdbDB3jbaOA1yEcEJ7w==}
     peerDependencies:
       '@tiptap/core': 3.27.1
@@ -3006,20 +3064,23 @@ packages:
     resolution: {integrity: sha512-jp2L/eY6fn+KgVVQAOqYItbF0VY/YApe5Mz2F0aykSO8gx31bYCZyvSeYxCHKvzHG5eZjc+zyaS5BrBWya2+kQ==}
     peerDependencies:
       '@types/react': ^19.2.0
 
   '@types/react-world-flags@1.6.0':
     resolution: {integrity: sha512-j/uVy2fnG8gX3Ckic4sccYm9XjieasUsJDMqBDtdPdcwe3aFfz+iBbds+wxOiTzfe5BErVGjdFu6NO1hCg/7lw==}
 
   '@types/react@19.2.17':
     resolution: {integrity: sha512-MXfmqaVPEVgkBT/aY0aGCkRWWtByiYQXo3xdQ8r5RzuFrPiRn8Gar2tQdXSUQ2GKV3bkXckek89V8wQBY2Q/Aw==}
 
+  '@types/trusted-types@2.0.7':
+    resolution: {integrity: sha512-ScaPdn1dQczgbl0QFTeTOmVHFULt394XJgOQNoyVhZ6r2vLnMLJfBPd53SB52T/3G36VI1/g2MZaX0cwDuXsfw==}
+
   '@types/unist@2.0.11':
     resolution: {integrity: sha512-CmBKiL6NNo/OqgmMn95Fk9Whlp2mtvIv+KNpQKN2F4SjvrEesubTRWGYSg+BnWZOnlCaSTU1sMpsBOzgbYhnsA==}
 
   '@types/unist@3.0.3':
     resolution: {integrity: sha512-ko/gIFJRv177XgZsZcBwnqJN5x/Gien8qNOn0D5bQU/zAzVf9Zt3BlcUiLqhV9y4ARk0GbT3tnUiPNgnTXzc/Q==}
 
   '@types/use-sync-external-store@0.0.6':
     resolution: {integrity: sha512-zFDAD+tlpf2r4asuHEj0XH6pY6i0g5NeAHPn+15wk3BV6JA69eERFXC1gyGThDkVa1zCyKr5jox1+2LbV/AMLg==}
 
   '@typescript-eslint/eslint-plugin@8.59.2':
@@ -3124,65 +3185,75 @@ packages:
 
   '@unrs/resolver-binding-linux-arm-musleabihf@1.12.2':
     resolution: {integrity: sha512-BiPI+IrIlwcW4nLLMM21+B1dFPzd55yAVgVGrdgDjNef+ch03GdxrcyaIz8X9SsQirh/kCQ7mviyWlMxdh2D7g==}
     cpu: [arm]
     os: [linux]
 
   '@unrs/resolver-binding-linux-arm64-gnu@1.12.2':
     resolution: {integrity: sha512-zJc0H99FEPoFfSrNpa91HYfxzfAJCr502oxNK1cfdC9hlaFI43RT+JFCann9JUgZmLzzntChHyn13Sgn9ljHNg==}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   '@unrs/resolver-binding-linux-arm64-musl@1.12.2':
     resolution: {integrity: sha512-KQ3Lki6l+Pz1k/eBipN41ES+YUK30beLGb9YqcB1O542cyLCNE6GaxrfcY3T6EezmGGk84wb5XyO9loTM9tkcA==}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   '@unrs/resolver-binding-linux-loong64-gnu@1.12.2':
     resolution: {integrity: sha512-3SJGEh1DborhG6pyxvhPzCT4bbSIVihsvgJc13P1bHG7KLdNDaF9T3gsTwFc7Jw/5Y5/iWOjkEx7Zy0NvCGX3Q==}
     cpu: [loong64]
     os: [linux]
+    libc: [glibc]
 
   '@unrs/resolver-binding-linux-loong64-musl@1.12.2':
     resolution: {integrity: sha512-jiuG/Obbel7uw1PwHNFfrkiKhLAF6mnyZ6aWlOAVN9WqKm8v0OFGnciJIHu8+CMvXLQ8AD51LPzAoUfT21D5Ew==}
     cpu: [loong64]
     os: [linux]
+    libc: [musl]
 
   '@unrs/resolver-binding-linux-ppc64-gnu@1.12.2':
     resolution: {integrity: sha512-q7xRvVpmcfeL+LlZg8Pbbo6QaTZwDU5BaGZbwfhkEsXJn3Was8xYfE0RBH266xZt0rM6B7i8xAYIvjthuUIWHg==}
     cpu: [ppc64]
     os: [linux]
+    libc: [glibc]
 
   '@unrs/resolver-binding-linux-riscv64-gnu@1.12.2':
     resolution: {integrity: sha512-0CVdx6lcnT3Q9inOH8tsMIOJ6ImndllMjqJHg8RLVdB7Vq4SfkEXl9mCSsVNuNA4MCYycRicCUxPCabVHJRr6A==}
     cpu: [riscv64]
     os: [linux]
+    libc: [glibc]
 
   '@unrs/resolver-binding-linux-riscv64-musl@1.12.2':
     resolution: {integrity: sha512-iOwlRo9vnp6R6ohHQS11n0NnfdXx/omhkocmIfaPRpQhKZ+3BDMkkdRVh53qjkFkpPddf+FETA28NwGN7l5l+w==}
     cpu: [riscv64]
     os: [linux]
+    libc: [musl]
 
   '@unrs/resolver-binding-linux-s390x-gnu@1.12.2':
     resolution: {integrity: sha512-HYJtLfXq94q8iZNFT1lknx258wlkkWhZeUXJRqzKBBUJ00CvZ+N33zgbCqimLjsyw5Va6uUxhVa12mI+kaveEw==}
     cpu: [s390x]
     os: [linux]
+    libc: [glibc]
 
   '@unrs/resolver-binding-linux-x64-gnu@1.12.2':
     resolution: {integrity: sha512-mPsUhunKKDih5O96Y6enDQyHc1SqBPlY1E/SfMWDM3EdJ95Z9CArPeCVwCCqbP45ljvivdEk8Fxn+SIb1rDAJQ==}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   '@unrs/resolver-binding-linux-x64-musl@1.12.2':
     resolution: {integrity: sha512-azrt6+5ydLd8Vt210AAFis/lZevSfPw93EJRIJG+xPu4WCJ8K0kppCTpMyLPcKT7H15M4Jnt2tMp5bOvCkRC6A==}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   '@unrs/resolver-binding-openharmony-arm64@1.12.2':
     resolution: {integrity: sha512-YZ9hP4O0X9PQb8eO980qmLNGH4zT3I9+SZTdt0Pr0YyuGQhYKoOZkV02VzrzyOZJ5xIJ3UFIenKkUkGg8GjgWQ==}
     cpu: [arm64]
     os: [openharmony]
 
   '@unrs/resolver-binding-wasm32-wasi@1.12.2':
     resolution: {integrity: sha512-tYFDIkMxSflfEc/h92ZWNsZlHSwgimbNHSO3PL2JWQHfCuC2q316jMyYU9TIWZsFK2bQwyK5VAdYgn8ygPj69A==}
     engines: {node: '>=14.0.0'}
     cpu: [wasm32]
@@ -3632,20 +3703,23 @@ packages:
   dom-serializer@2.0.0:
     resolution: {integrity: sha512-wIkAryiqt/nV5EQKqQpo3SToSOV9J0DnbJqwK7Wv/Trc92zIAYZ4FlMu+JPFW1DfGFt81ZTCGgDEabffXeLyJg==}
 
   domelementtype@2.3.0:
     resolution: {integrity: sha512-OLETBj6w0OsagBwdXnPdN0cnMfF9opN69co+7ZrbfPGrdpPVNBUj02spi6B1N7wChLQiPn4CSH/zJvXw56gmHw==}
 
   domhandler@5.0.3:
     resolution: {integrity: sha512-cgwlv/1iFQiFnU96XXgROh8xTeetsnJiDsTc7TYCLFd9+/WNkIqPTxiM/8pSd8VIrhXGTf1Ny1q1hquVqDJB5w==}
     engines: {node: '>= 4'}
 
+  dompurify@3.4.11:
+    resolution: {integrity: sha512-zhlUV12GsaRzMsf9q5M254YhA4+VuF0fG+QFqu6aYpoGlKtz+w8//jBcGVYBgQkR5GHjUomejY84AV+/uPbWdw==}
+
   domutils@3.2.2:
     resolution: {integrity: sha512-6kZKyUajlDuqlHKVX1w7gyslj9MPIXzIFiz/rGu35uC1wMi+kMhQwGhl4lt9unC9Vb9INnY9Z3/ZA3+FhASLaw==}
 
   dotenv@17.4.2:
     resolution: {integrity: sha512-nI4U3TottKAcAD9LLud4Cb7b2QztQMUEfHbvhTH09bqXTxnSie8WnjPALV/WMCrJZ6UV/qHJ6L03OqO3LcdYZw==}
     engines: {node: '>=12'}
 
   dunder-proto@1.0.1:
     resolution: {integrity: sha512-KIN/nDJBQRcXw0MLVhZE9iQHmG68qAVIBg9CqmUYjmQIhgij9U5MFvrqkUL5FbtyyzZuOeOt0zdeRe4UY7ct+A==}
     engines: {node: '>= 0.4'}
@@ -4380,38 +4454,42 @@ packages:
     resolution: {integrity: sha512-x6rnnpRa2GL0zQOkt6rts3YDPzduLpWvwAF6EMhXFVZXD4tPrBkEFqzGowzCsIWsPjqSK+tyNEODUBXeeVHSkw==}
     engines: {node: '>= 12.0.0'}
     cpu: [arm]
     os: [linux]
 
   lightningcss-linux-arm64-gnu@1.32.0:
     resolution: {integrity: sha512-0nnMyoyOLRJXfbMOilaSRcLH3Jw5z9HDNGfT/gwCPgaDjnx0i8w7vBzFLFR1f6CMLKF8gVbebmkUN3fa/kQJpQ==}
     engines: {node: '>= 12.0.0'}
     cpu: [arm64]
     os: [linux]
+    libc: [glibc]
 
   lightningcss-linux-arm64-musl@1.32.0:
     resolution: {integrity: sha512-UpQkoenr4UJEzgVIYpI80lDFvRmPVg6oqboNHfoH4CQIfNA+HOrZ7Mo7KZP02dC6LjghPQJeBsvXhJod/wnIBg==}
     engines: {node: '>= 12.0.0'}
     cpu: [arm64]
     os: [linux]
+    libc: [musl]
 
   lightningcss-linux-x64-gnu@1.32.0:
     resolution: {integrity: sha512-V7Qr52IhZmdKPVr+Vtw8o+WLsQJYCTd8loIfpDaMRWGUZfBOYEJeyJIkqGIDMZPwPx24pUMfwSxxI8phr/MbOA==}
     engines: {node: '>= 12.0.0'}
     cpu: [x64]
     os: [linux]
+    libc: [glibc]
 
   lightningcss-linux-x64-musl@1.32.0:
     resolution: {integrity: sha512-bYcLp+Vb0awsiXg/80uCRezCYHNg1/l3mt0gzHnWV9XP1W5sKa5/TCdGWaR/zBM2PeF/HbsQv/j2URNOiVuxWg==}
     engines: {node: '>= 12.0.0'}
     cpu: [x64]
     os: [linux]
+    libc: [musl]
 
   lightningcss-win32-arm64-msvc@1.32.0:
     resolution: {integrity: sha512-8SbC8BR40pS6baCM8sbtYDSwEVQd4JlFTOlaD3gWGHfThTcABnNDBda6eTZeqbofalIJhFx0qKzgHJmcPTnGdw==}
     engines: {node: '>= 12.0.0'}
     cpu: [arm64]
     os: [win32]
 
   lightningcss-win32-x64-msvc@1.32.0:
     resolution: {integrity: sha512-Amq9B/SoZYdDi1kFrojnoqPLxYhQ4Wo5XiL8EVJrVsB8ARoC1PWW6VGtT0WKCemjy8aC+louJnjS7U18x3b06Q==}
     engines: {node: '>= 12.0.0'}
@@ -7555,22 +7633,22 @@ snapshots:
     optionalDependencies:
       '@types/react': 19.2.17
       '@types/react-dom': 19.2.3(@types/react@19.2.17)
 
   '@radix-ui/react-visually-hidden@1.2.6(@types/react-dom@19.2.3(@types/react@19.2.17))(@types/react@19.2.17)(react-dom@19.2.7(react@19.2.7))(react@19.2.7)':
     dependencies:
       '@radix-ui/react-primitive': 2.1.6(@types/react-dom@19.2.3(@types/react@19.2.17))(@types/react@19.2.17)(react-dom@19.2.7(react@19.2.7))(react@19.2.7)
       react: 19.2.7
       react-dom: 19.2.7(react@19.2.7)
     optionalDependencies:
-       '@types/react': 19.2.17
-       '@types/react-dom': 19.2.3(@types/react@19.2.17)
+      '@types/react': 19.2.17
+      '@types/react-dom': 19.2.3(@types/react@19.2.17)
 
   '@radix-ui/rect@1.1.1': {}
 
   '@radix-ui/rect@1.1.2': {}
 
   '@remixicon/react@4.9.0(react@19.2.7)':
     dependencies:
       react: 19.2.7
 
   '@rolldown/pluginutils@1.0.0-beta.27': {}
@@ -7893,29 +7971,29 @@ snapshots:
     dependencies:
       '@floating-ui/dom': 1.7.6
       '@tiptap/core': 3.22.5(@tiptap/pm@3.22.5)
       '@tiptap/pm': 3.22.5
     optional: true
 
   '@tiptap/extension-bullet-list@3.27.1(@tiptap/extension-list@3.27.1(@tiptap/core@3.27.1(@tiptap/pm@3.27.1))(@tiptap/pm@3.27.1))':
     dependencies:
       '@tiptap/extension-list': 3.27.1(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)
 
-  '@tiptap/extension-code-block-lowlight@3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/extension-code-block@3.27.1(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)(highlight.js@11.11.1)(lowlight@3.3.0)':
+  '@tiptap/extension-code-block-lowlight@3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/extension-code-block@3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)(highlight.js@11.11.1)(lowlight@3.3.0)':
     dependencies:
       '@tiptap/core': 3.22.5(@tiptap/pm@3.22.5)
-      '@tiptap/extension-code-block': 3.27.1(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)
+      '@tiptap/extension-code-block': 3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)
       '@tiptap/pm': 3.22.5
       highlight.js: 11.11.1
       lowlight: 3.3.0
 
-  '@tiptap/extension-code-block@3.27.1(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)':
+  '@tiptap/extension-code-block@3.22.5(@tiptap/core@3.22.5(@tiptap/pm@3.22.5))(@tiptap/pm@3.22.5)':
     dependencies:
       '@tiptap/core': 3.22.5(@tiptap/pm@3.22.5)
       '@tiptap/pm': 3.22.5
 
   '@tiptap/extension-code-block@3.27.1(@tiptap/core@3.27.1(@tiptap/pm@3.27.1))(@tiptap/pm@3.27.1)':
     dependencies:
       '@tiptap/core': 3.27.1(@tiptap/pm@3.27.1)
       '@tiptap/pm': 3.27.1
 
   '@tiptap/extension-code@3.27.1(@tiptap/core@3.27.1(@tiptap/pm@3.27.1))':
@@ -8233,20 +8311,23 @@ snapshots:
       '@types/react': 19.2.17
 
   '@types/react-world-flags@1.6.0':
     dependencies:
       '@types/react': 19.2.17
 
   '@types/react@19.2.17':
     dependencies:
       csstype: 3.2.3
 
+  '@types/trusted-types@2.0.7':
+    optional: true
+
   '@types/unist@2.0.11': {}
 
   '@types/unist@3.0.3': {}
 
   '@types/use-sync-external-store@0.0.6': {}
 
   '@typescript-eslint/eslint-plugin@8.59.2(@typescript-eslint/parser@8.59.2(eslint@9.39.4(jiti@2.7.0))(typescript@5.9.3))(eslint@9.39.4(jiti@2.7.0))(typescript@5.9.3)':
     dependencies:
       '@eslint-community/regexpp': 4.12.2
       '@typescript-eslint/parser': 8.59.2(eslint@9.39.4(jiti@2.7.0))(typescript@5.9.3)
@@ -8880,20 +8961,24 @@ snapshots:
       domelementtype: 2.3.0
       domhandler: 5.0.3
       entities: 4.5.0
 
   domelementtype@2.3.0: {}
 
   domhandler@5.0.3:
     dependencies:
       domelementtype: 2.3.0
 
+  dompurify@3.4.11:
+    optionalDependencies:
+      '@types/trusted-types': 2.0.7
+
   domutils@3.2.2:
     dependencies:
       dom-serializer: 2.0.0
       domelementtype: 2.3.0
       domhandler: 5.0.3
 
   dotenv@17.4.2: {}
 
   dunder-proto@1.0.1:
     dependencies:
```
