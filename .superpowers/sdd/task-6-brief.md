### Task 6: Admin Content Route, Navigation, and Loader

**Files:**
- Modify: `frontend/components/layout/sidebar/nav-main.tsx`
- Modify: `frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx`
- Create: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx`
- Create: `frontend/components/admin/public-content/PublicContentManager.tsx`
- Test: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx`

**Interfaces:**
- Consumes: `getAdminPublicContent()`.
- Produces: visible `/dashboard/settings/public-content` route with page/contact tabs.

- [ ] **Step 1: Add route smoke test**

Create `PublicContentManager.test.tsx`:

```tsx
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
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: FAIL because route does not exist.

- [ ] **Step 3: Create route and manager shell**

Create `page.tsx`:

```tsx
"use client";

import PublicContentManager from "@/components/admin/public-content/PublicContentManager";

export default function PublicContentPage() {
  return <PublicContentManager />;
}
```

Create `PublicContentManager.tsx`:

```tsx
"use client";

import useSWR from "swr";
import { FileText, Phone } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getAdminPublicContent } from "@/lib/api/admin/publicContent";

export default function PublicContentManager() {
  const { data, isLoading, error, mutate } = useSWR(["admin", "public-content"], getAdminPublicContent);

  if (isLoading) {
    return <Skeleton className="h-64 w-full" />;
  }

  if (error || !data) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>İçerik Yönetimi</CardTitle>
        </CardHeader>
        <CardContent>İçerik verisi yüklenemedi.</CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">İçerik Yönetimi</h1>
        <p className="text-sm text-muted-foreground">Public sayfa ve iletişim içeriklerini yönetin.</p>
      </div>
      <Tabs defaultValue="pages" className="space-y-4">
        <TabsList>
          <TabsTrigger value="pages">
            <FileText className="mr-2 h-4 w-4" />
            Sayfalar
          </TabsTrigger>
          <TabsTrigger value="contact">
            <Phone className="mr-2 h-4 w-4" />
            İletişim
          </TabsTrigger>
        </TabsList>
        <TabsContent value="pages">Sayfa editörü yükleniyor.</TabsContent>
        <TabsContent value="contact">İletişim editörü yükleniyor.</TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 4: Add navigation**

Add a settings child item in `nav-main.tsx`:

```ts
{ title: "İçerik Yönetimi", href: "/dashboard/settings/public-content" },
```

Add a settings tab in `settings/layout.tsx`:

```tsx
<TabsTrigger value="public-content" asChild>
  <Link href="/dashboard/settings/public-content">İçerik Yönetimi</Link>
</TabsTrigger>
```

- [ ] **Step 5: Run route test**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/components/layout/sidebar/nav-main.tsx frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx frontend/components/admin/public-content/PublicContentManager.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
git commit -m "feat(admin): add public content workspace"
```

---

