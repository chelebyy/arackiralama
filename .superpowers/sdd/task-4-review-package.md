# Task 4 Review Package

Base: ddbb8266e55a3ea2458f97e37a663ba58f9b10c8
Head: cdfe401fc6ee6111c2cf4a7856f243ebc5cf0d9f

## Commit Log
```
cdfe401 feat(admin): add public content api client
```

## Diff Stat
```
 frontend/lib/api/admin/admin-api.test.ts | 118 +++++++++++++++++++++++++++++++
 frontend/lib/api/admin/index.ts          |   1 +
 frontend/lib/api/admin/publicContent.ts  |  74 +++++++++++++++++++
 frontend/lib/api/admin/types.ts          |  51 +++++++++++++
 4 files changed, 244 insertions(+)
```

## Diff
```diff
diff --git a/frontend/lib/api/admin/admin-api.test.ts b/frontend/lib/api/admin/admin-api.test.ts
index 301bed7..31d2474 100644
--- a/frontend/lib/api/admin/admin-api.test.ts
+++ b/frontend/lib/api/admin/admin-api.test.ts
@@ -431,10 +431,128 @@ describe("admin pricing, users, settings, and reports APIs", () => {
     expect(mockedGet).toHaveBeenNthCalledWith(3, "/v1/public-site-settings");
     expect(mockedPut).toHaveBeenCalledWith("/v1/public-site-settings", publicSiteSettings);
     expect(mockedGet).toHaveBeenNthCalledWith(4, "/v1/reports/revenue?period=month");
     expect(mockedGet).toHaveBeenNthCalledWith(5, "/v1/reports/occupancy?period=week");
     expect(mockedGet).toHaveBeenNthCalledWith(
       6,
       "/v1/reports/popular-vehicles?period=year"
     );
   });
 });
+
+describe("admin public content API", () => {
+  beforeEach(() => {
+    vi.clearAllMocks();
+  });
+
+  it("gets admin public content", async () => {
+    const publicContent = {
+      version: "1",
+      updatedAt: "2026-06-27T00:00:00Z",
+      pages: [],
+      contactPageChannels: [],
+      contactPageOffices: [],
+      contactPageWorkingHours: [],
+      contactPageMapTitle: "Map",
+      contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=managed",
+      contactPageMapIsVisible: true,
+    };
+    mockedGet.mockResolvedValueOnce({ data: publicContent } as never);
+
+    const { getAdminPublicContent } = await import("./publicContent");
+
+    await expect(getAdminPublicContent()).resolves.toBe(publicContent);
+
+    expect(mockedGet).toHaveBeenCalledWith("/v1/public-content");
+  });
+
+  it("updates an admin public page draft through the public-content endpoint", async () => {
+    mockedPut.mockResolvedValueOnce({
+      data: { version: "2", updatedAt: "2026-06-27T00:00:00Z", pages: [] },
+    } as never);
+
+    const { updateAdminPublicPageDraft } = await import("./publicContent");
+
+    await updateAdminPublicPageDraft("privacy", "tr", {
+      version: "1",
+      title: "Title",
+      subtitle: "",
+      seoTitle: "",
+      seoDescription: "",
+      isPublished: true,
+      sortOrder: 0,
+      blocks: [],
+    });
+
+    expect(mockedPut).toHaveBeenCalledWith("/v1/public-content/pages/privacy/tr/draft", {
+      version: "1",
+      title: "Title",
+      subtitle: "",
+      seoTitle: "",
+      seoDescription: "",
+      isPublished: true,
+      sortOrder: 0,
+      blocks: [],
+    });
+  });
+
+  it("publishes and unpublishes public pages through encoded page endpoints", async () => {
+    const publicContent = {
+      version: "4",
+      updatedAt: "2026-06-27T00:00:00Z",
+      pages: [],
+      contactPageChannels: [],
+      contactPageOffices: [],
+      contactPageWorkingHours: [],
+      contactPageMapTitle: "",
+      contactPageMapEmbedUrl: "",
+      contactPageMapIsVisible: false,
+    };
+    mockedPost.mockResolvedValue({ data: publicContent } as never);
+
+    const { publishAdminPublicPage, unpublishAdminPublicPage } = await import("./publicContent");
+
+    await expect(publishAdminPublicPage("terms & fees", "en", "2")).resolves.toBe(publicContent);
+    await expect(unpublishAdminPublicPage("terms & fees", "en", "3")).resolves.toBe(publicContent);
+
+    expect(mockedPost).toHaveBeenNthCalledWith(
+      1,
+      "/v1/public-content/pages/terms%20%26%20fees/en/publish",
+      { version: "2" }
+    );
+    expect(mockedPost).toHaveBeenNthCalledWith(
+      2,
+      "/v1/public-content/pages/terms%20%26%20fees/en/unpublish",
+      { version: "3" }
+    );
+  });
+
+  it("updates admin public contact content", async () => {
+    const publicContent = {
+      version: "6",
+      updatedAt: "2026-06-27T00:00:00Z",
+      pages: [],
+      contactPageChannels: [],
+      contactPageOffices: [],
+      contactPageWorkingHours: [],
+      contactPageMapTitle: "Map",
+      contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=managed",
+      contactPageMapIsVisible: true,
+    };
+    const updateData = {
+      version: "5",
+      contactPageChannels: [],
+      contactPageOffices: [],
+      contactPageWorkingHours: [],
+      contactPageMapTitle: "Map",
+      contactPageMapEmbedUrl: "https://www.google.com/maps/embed?pb=managed",
+      contactPageMapIsVisible: true,
+    };
+    mockedPut.mockResolvedValueOnce({ data: publicContent } as never);
+
+    const { updateAdminPublicContact } = await import("./publicContent");
+
+    await expect(updateAdminPublicContact(updateData)).resolves.toBe(publicContent);
+
+    expect(mockedPut).toHaveBeenCalledWith("/v1/public-content/contact", updateData);
+  });
+});
diff --git a/frontend/lib/api/admin/index.ts b/frontend/lib/api/admin/index.ts
index 945f292..22032e8 100644
--- a/frontend/lib/api/admin/index.ts
+++ b/frontend/lib/api/admin/index.ts
@@ -1,8 +1,9 @@
 export * from './types';
 export * from './vehicles';
 export * from './reservations';
 export * from './pricing';
 export * from './users';
 export * from './reports';
 export * from './settings';
+export * from './publicContent';
 export type { PaginatedResponse } from '../types';
diff --git a/frontend/lib/api/admin/publicContent.ts b/frontend/lib/api/admin/publicContent.ts
new file mode 100644
index 0000000..860465e
--- /dev/null
+++ b/frontend/lib/api/admin/publicContent.ts
@@ -0,0 +1,74 @@
+import { adminGet, adminPost, adminPut } from '../client';
+import type {
+  AdminPublicContent,
+  AdminResponse,
+  PublicSettingsLocale,
+  UpdateAdminPublicContactData,
+  UpdateAdminPublicPageDraftData,
+} from './types';
+
+const PUBLIC_CONTENT_ENDPOINT = '/v1/public-content';
+
+function unwrapResponse<T>(response: AdminResponse<T>): T {
+  if (response && typeof response === 'object' && 'data' in response) {
+    return (response as { data: T }).data;
+  }
+  return response as T;
+}
+
+function getPublicContentPageEndpoint(
+  slug: string,
+  locale: PublicSettingsLocale,
+  action: 'draft' | 'publish' | 'unpublish'
+) {
+  return `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/${action}`;
+}
+
+export async function getAdminPublicContent() {
+  const response = await adminGet<AdminResponse<AdminPublicContent>>(PUBLIC_CONTENT_ENDPOINT);
+  return unwrapResponse(response);
+}
+
+export async function updateAdminPublicPageDraft(
+  slug: string,
+  locale: PublicSettingsLocale,
+  data: UpdateAdminPublicPageDraftData
+) {
+  const response = await adminPut<AdminResponse<AdminPublicContent>>(
+    getPublicContentPageEndpoint(slug, locale, 'draft'),
+    data
+  );
+  return unwrapResponse(response);
+}
+
+export async function publishAdminPublicPage(
+  slug: string,
+  locale: PublicSettingsLocale,
+  version: string
+) {
+  const response = await adminPost<AdminResponse<AdminPublicContent>>(
+    getPublicContentPageEndpoint(slug, locale, 'publish'),
+    { version }
+  );
+  return unwrapResponse(response);
+}
+
+export async function unpublishAdminPublicPage(
+  slug: string,
+  locale: PublicSettingsLocale,
+  version: string
+) {
+  const response = await adminPost<AdminResponse<AdminPublicContent>>(
+    getPublicContentPageEndpoint(slug, locale, 'unpublish'),
+    { version }
+  );
+  return unwrapResponse(response);
+}
+
+export async function updateAdminPublicContact(data: UpdateAdminPublicContactData) {
+  const response = await adminPut<AdminResponse<AdminPublicContent>>(
+    `${PUBLIC_CONTENT_ENDPOINT}/contact`,
+    data
+  );
+  return unwrapResponse(response);
+}
diff --git a/frontend/lib/api/admin/types.ts b/frontend/lib/api/admin/types.ts
index fe286bb..c1686c6 100644
--- a/frontend/lib/api/admin/types.ts
+++ b/frontend/lib/api/admin/types.ts
@@ -195,41 +195,92 @@ export interface PublicContactOffice {
 
 export interface PublicContactWorkingHour {
   id: string;
   day: string;
   hours: string;
   isVisible: boolean;
   sortOrder: number;
   translations?: PublicLocalizedTextMap | null;
 }
 
+export type PublicPageBlockBodyFormat = 'plain' | 'html';
+
 export interface PublicPageBlock {
   id: string;
   heading: string;
   body: string;
   isVisible: boolean;
   sortOrder: number;
+  bodyFormat?: PublicPageBlockBodyFormat;
 }
 
 export interface PublicManagedPage {
   id: string;
   slug: string;
   locale: string;
   title: string;
   subtitle: string;
   seoTitle: string;
   seoDescription: string;
   isPublished: boolean;
   sortOrder: number;
   blocks: PublicPageBlock[];
 }
 
+export interface PublicPagePublishedSnapshot {
+  title: string;
+  subtitle: string;
+  seoTitle: string;
+  seoDescription: string;
+  blocks: PublicPageBlock[];
+  publishedAtUtc: string | null;
+}
+
+export interface AdminPublicManagedPage extends PublicManagedPage {
+  published: PublicPagePublishedSnapshot | null;
+  draftUpdatedAtUtc: string | null;
+  publishedAtUtc: string | null;
+}
+
+export interface AdminPublicContent {
+  version: string;
+  updatedAt: string;
+  pages: AdminPublicManagedPage[];
+  contactPageChannels: PublicContactChannel[];
+  contactPageOffices: PublicContactOffice[];
+  contactPageWorkingHours: PublicContactWorkingHour[];
+  contactPageMapTitle: string;
+  contactPageMapEmbedUrl: string;
+  contactPageMapIsVisible: boolean;
+}
+
+export interface UpdateAdminPublicPageDraftData {
+  version: string;
+  title: string;
+  subtitle: string;
+  seoTitle: string;
+  seoDescription: string;
+  isPublished: boolean;
+  sortOrder: number;
+  blocks: PublicPageBlock[];
+}
+
+export interface UpdateAdminPublicContactData {
+  version: string;
+  contactPageChannels: PublicContactChannel[];
+  contactPageOffices: PublicContactOffice[];
+  contactPageWorkingHours: PublicContactWorkingHour[];
+  contactPageMapTitle: string;
+  contactPageMapEmbedUrl: string;
+  contactPageMapIsVisible: boolean;
+}
+
 export interface PublicPaymentMethods {
   creditCardEnabled: boolean;
   debitCardEnabled: boolean;
   unpaidRequestEnabled: boolean;
   paypalEnabled: boolean;
   anyEnabled: boolean;
 }
 
 export interface PublicSiteSettings {
   companyName: string;
```
