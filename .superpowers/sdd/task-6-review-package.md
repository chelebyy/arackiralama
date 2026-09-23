# Task 6 Review Package

Base: 5e62aea
Head: 35c43042926865d17bb8d073e62c9b7f7a4b7d6a

## Commit Log
```
35c4304 feat(admin): add public content workspace
```

## Diff Stat
```
 .../(admin)/dashboard/(auth)/settings/layout.tsx   |  3 +
 .../public-content/PublicContentManager.test.tsx   | 27 +++++++
 .../(auth)/settings/public-content/page.tsx        |  7 ++
 .../admin/public-content/PublicContentManager.tsx  | 90 ++++++++++++++++++++++
 frontend/components/layout/sidebar/nav-main.tsx    |  1 +
 5 files changed, 128 insertions(+)
```

## Diff
```diff
diff --git a/frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx b/frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx
index b18e421..d14f370 100644
--- a/frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx
+++ b/frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx
@@ -14,19 +14,22 @@ export default function SettingsLayout({ children }: { children: React.ReactNode
         <h1 className="text-2xl font-bold tracking-tight">Ayarlar</h1>
       </div>
       <Tabs value={segment} className="w-full">
         <TabsList>
           <TabsTrigger value="feature-flags" asChild>
             <Link href="/dashboard/settings/feature-flags">Özellik Bayrakları</Link>
           </TabsTrigger>
           <TabsTrigger value="system" asChild>
             <Link href="/dashboard/settings/system">Public Site & İletişim</Link>
           </TabsTrigger>
+          <TabsTrigger value="public-content" asChild>
+            <Link href="/dashboard/settings/public-content">İçerik Yönetimi</Link>
+          </TabsTrigger>
           <TabsTrigger value="audit-logs" asChild>
             <Link href="/dashboard/settings/audit-logs">Denetim Kayıtları</Link>
           </TabsTrigger>
         </TabsList>
       </Tabs>
       <div className="mt-4">{children}</div>
     </div>
   );
 }
diff --git a/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
new file mode 100644
index 0000000..fb18cb3
--- /dev/null
+++ b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
@@ -0,0 +1,27 @@
+import { render, screen } from "@testing-library/react";
+import { describe, expect, it, vi } from "vitest";
+import PublicContentPage from "./page";
+
+vi.mock("@/lib/api/admin/publicContent", () => ({
+  getAdminPublicContent: vi.fn().mockResolvedValue({
+    version: "1",
+    updatedAt: "2026-06-27T00:00:00Z",
+    pages: [],
+    contactPageChannels: [],
+    contactPageOffices: [],
+    contactPageWorkingHours: [],
+    contactPageMapTitle: "",
+    contactPageMapEmbedUrl: "",
+    contactPageMapIsVisible: true,
+  }),
+}));
+
+describe("PublicContentPage", () => {
+  it("renders the public content workspace", async () => {
+    render(<PublicContentPage />);
+
+    expect(await screen.findByRole("heading", { name: "İçerik Yönetimi" })).toBeInTheDocument();
+    expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
+    expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
+  });
+});
diff --git a/frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx
new file mode 100644
index 0000000..17829fc
--- /dev/null
+++ b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx
@@ -0,0 +1,7 @@
+"use client";
+
+import PublicContentManager from "@/components/admin/public-content/PublicContentManager";
+
+export default function PublicContentPage() {
+  return <PublicContentManager />;
+}
diff --git a/frontend/components/admin/public-content/PublicContentManager.tsx b/frontend/components/admin/public-content/PublicContentManager.tsx
new file mode 100644
index 0000000..88faf3c
--- /dev/null
+++ b/frontend/components/admin/public-content/PublicContentManager.tsx
@@ -0,0 +1,90 @@
+"use client";
+
+import { FileText, Phone } from "lucide-react";
+import useSWR from "swr";
+import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
+import { Skeleton } from "@/components/ui/skeleton";
+import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
+import { getAdminPublicContent } from "@/lib/api/admin/publicContent";
+import type { AdminPublicContent } from "@/lib/api/admin/types";
+
+const PUBLIC_CONTENT_CACHE_KEY = "admin-public-content";
+
+export default function PublicContentManager() {
+  const { data, error, isLoading } = useSWR<AdminPublicContent>(
+    PUBLIC_CONTENT_CACHE_KEY,
+    () => getAdminPublicContent(),
+  );
+  const hasLoadError = error || (!isLoading && !data);
+
+  return (
+    <div className="space-y-4">
+      <h1 className="text-2xl font-bold tracking-tight">İçerik Yönetimi</h1>
+
+      {hasLoadError ? (
+        <Card>
+          <CardHeader>
+            <CardTitle className="text-base">İçerik Yönetimi</CardTitle>
+          </CardHeader>
+          <CardContent>
+            <div className="text-sm text-destructive">İçerik verisi yüklenemedi.</div>
+          </CardContent>
+        </Card>
+      ) : (
+        <Tabs defaultValue="pages" className="space-y-4">
+          <TabsList>
+            <TabsTrigger value="pages">
+              <FileText className="mr-2 h-4 w-4" />
+              Sayfalar
+            </TabsTrigger>
+            <TabsTrigger value="contact">
+              <Phone className="mr-2 h-4 w-4" />
+              İletişim
+            </TabsTrigger>
+          </TabsList>
+
+          <TabsContent value="pages">
+            <Card>
+              <CardHeader>
+                <CardTitle className="text-base">Sayfalar</CardTitle>
+              </CardHeader>
+              <CardContent className="space-y-3">
+                {isLoading || !data ? (
+                  <>
+                    <Skeleton className="h-10 w-full" />
+                    <Skeleton className="h-16 w-full" />
+                  </>
+                ) : (
+                  <div className="text-sm text-muted-foreground">{data.pages.length} sayfa</div>
+                )}
+              </CardContent>
+            </Card>
+          </TabsContent>
+
+          <TabsContent value="contact">
+            <Card>
+              <CardHeader>
+                <CardTitle className="text-base">İletişim</CardTitle>
+              </CardHeader>
+              <CardContent className="grid gap-3 text-sm text-muted-foreground md:grid-cols-3">
+                {isLoading || !data ? (
+                  <>
+                    <Skeleton className="h-10 w-full" />
+                    <Skeleton className="h-10 w-full" />
+                    <Skeleton className="h-10 w-full" />
+                  </>
+                ) : (
+                  <>
+                    <div>{data.contactPageChannels.length} kanal</div>
+                    <div>{data.contactPageOffices.length} ofis</div>
+                    <div>{data.contactPageWorkingHours.length} çalışma saati</div>
+                  </>
+                )}
+              </CardContent>
+            </Card>
+          </TabsContent>
+        </Tabs>
+      )}
+    </div>
+  );
+}
diff --git a/frontend/components/layout/sidebar/nav-main.tsx b/frontend/components/layout/sidebar/nav-main.tsx
index 4c13c0a..e8e715c 100644
--- a/frontend/components/layout/sidebar/nav-main.tsx
+++ b/frontend/components/layout/sidebar/nav-main.tsx
@@ -137,20 +137,21 @@ export const navItems: NavGroup[] = [
   },
   {
     title: "Sistem",
     items: [
       {
         title: "Ayarlar",
         href: "/dashboard/settings",
         icon: Settings,
         items: [
           { title: "Public Site & İletişim", href: "/dashboard/settings/system" },
+          { title: "İçerik Yönetimi", href: "/dashboard/settings/public-content" },
           { title: "Ozellik Bayraklari", href: "/dashboard/settings/feature-flags" },
           { title: "Denetim Kayitlari", href: "/dashboard/settings/audit-logs" },
         ],
       },
     ],
   },
 ];
 
 export function NavMain() {
   const pathname = usePathname();
```
