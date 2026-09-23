### Task 4: Frontend API Types and Client

**Files:**
- Modify: `frontend/lib/api/admin/types.ts`
- Create: `frontend/lib/api/admin/publicContent.ts`
- Modify: `frontend/lib/api/admin/index.ts`
- Test: `frontend/lib/api/admin/admin-api.test.ts`

**Interfaces:**
- Consumes: backend endpoint paths from Task 3.
- Produces: typed frontend admin content client functions.

- [ ] **Step 1: Add API client test**

Add to `admin-api.test.ts`:

```ts
it("updates an admin public page draft through the public-content endpoint", async () => {
  adminPutMock.mockResolvedValueOnce({
    data: { version: "2", updatedAt: "2026-06-27T00:00:00Z", pages: [] },
  });

  const { updateAdminPublicPageDraft } = await import("./publicContent");

  await updateAdminPublicPageDraft("privacy", "tr", {
    version: "1",
    title: "Title",
    subtitle: "",
    seoTitle: "",
    seoDescription: "",
    isPublished: true,
    sortOrder: 0,
    blocks: [],
  });

  expect(adminPutMock).toHaveBeenCalledWith("/v1/public-content/pages/privacy/tr/draft", {
    version: "1",
    title: "Title",
    subtitle: "",
    seoTitle: "",
    seoDescription: "",
    isPublished: true,
    sortOrder: 0,
    blocks: [],
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test lib/api/admin/admin-api.test.ts
```

Expected: FAIL because `publicContent` client does not exist.

- [ ] **Step 3: Add types**

Add these interfaces to `types.ts`:

```ts
export type PublicPageBlockBodyFormat = "plain" | "html";

export interface PublicPagePublishedSnapshot {
  title: string;
  subtitle: string;
  seoTitle: string;
  seoDescription: string;
  blocks: PublicPageBlock[];
  publishedAtUtc: string | null;
}

export interface AdminPublicManagedPage extends PublicManagedPage {
  published: PublicPagePublishedSnapshot | null;
  draftUpdatedAtUtc: string | null;
  publishedAtUtc: string | null;
}

export interface AdminPublicContent {
  version: string;
  updatedAt: string;
  pages: AdminPublicManagedPage[];
  contactPageChannels: PublicContactChannel[];
  contactPageOffices: PublicContactOffice[];
  contactPageWorkingHours: PublicContactWorkingHour[];
  contactPageMapTitle: string;
  contactPageMapEmbedUrl: string;
  contactPageMapIsVisible: boolean;
}

export interface UpdateAdminPublicPageDraftData {
  version: string;
  title: string;
  subtitle: string;
  seoTitle: string;
  seoDescription: string;
  isPublished: boolean;
  sortOrder: number;
  blocks: PublicPageBlock[];
}

export interface UpdateAdminPublicContactData {
  version: string;
  contactPageChannels: PublicContactChannel[];
  contactPageOffices: PublicContactOffice[];
  contactPageWorkingHours: PublicContactWorkingHour[];
  contactPageMapTitle: string;
  contactPageMapEmbedUrl: string;
  contactPageMapIsVisible: boolean;
}
```

Update `PublicPageBlock`:

```ts
export interface PublicPageBlock {
  id: string;
  heading: string;
  body: string;
  isVisible: boolean;
  sortOrder: number;
  bodyFormat?: PublicPageBlockBodyFormat;
}
```

- [ ] **Step 4: Add API client**

Create `publicContent.ts`:

```ts
import { adminGet, adminPost, adminPut } from "../client";
import type {
  AdminResponse,
  AdminPublicContent,
  PublicSettingsLocale,
  UpdateAdminPublicContactData,
  UpdateAdminPublicPageDraftData,
} from "./types";

const PUBLIC_CONTENT_ENDPOINT = "/v1/public-content";

export async function getAdminPublicContent() {
  const response = await adminGet<AdminResponse<AdminPublicContent>>(PUBLIC_CONTENT_ENDPOINT);
  return response.data;
}

export async function updateAdminPublicPageDraft(
  slug: string,
  locale: PublicSettingsLocale,
  data: UpdateAdminPublicPageDraftData
) {
  const response = await adminPut<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/draft`,
    data
  );
  return response.data;
}

export async function publishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
) {
  const response = await adminPost<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/publish`,
    { version }
  );
  return response.data;
}

export async function unpublishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
) {
  const response = await adminPost<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/unpublish`,
    { version }
  );
  return response.data;
}

export async function updateAdminPublicContact(data: UpdateAdminPublicContactData) {
  const response = await adminPut<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/contact`,
    data
  );
  return response.data;
}
```

Export it from `frontend/lib/api/admin/index.ts`:

```ts
export * from "./publicContent";
```

- [ ] **Step 5: Run admin API test**

Run:

```powershell
corepack pnpm -C frontend test lib/api/admin/admin-api.test.ts
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/lib/api/admin/types.ts frontend/lib/api/admin/publicContent.ts frontend/lib/api/admin/index.ts frontend/lib/api/admin/admin-api.test.ts
git commit -m "feat(admin): add public content api client"
```

---

