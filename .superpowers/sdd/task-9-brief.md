### Task 9: Remove Duplicate Content Editing from System Settings

**Files:**
- Modify: `frontend/app/(admin)/dashboard/(auth)/settings/system/page.tsx`
- Modify: `frontend/app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx`
- Modify: `frontend/e2e/tests/admin-public-settings.spec.ts`

**Interfaces:**
- Consumes: public content workspace from Tasks 6-8.
- Produces: system settings screen that no longer embeds managed pages/contact editing.

- [ ] **Step 1: Update system settings test expectations**

Change tests so `SystemSettingsPage` still saves company/link/social fields but no longer expects:

```ts
screen.getByText("Sayfalar")
screen.getByText("İletişim Sayfası Kanalları")
screen.getByText("İletişim Sayfası Ofisleri")
screen.getByText("İletişim Sayfası Çalışma Saatleri")
```

Add:

```ts
expect(screen.queryByText("Sayfalar")).not.toBeInTheDocument();
expect(screen.queryByText("İletişim Sayfası Kanalları")).not.toBeInTheDocument();
```

- [ ] **Step 2: Run system test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx"
```

Expected: FAIL because old sections still render.

- [ ] **Step 3: Remove managed page/contact sections**

In `system/page.tsx`, remove:

- `managedPageSchema`, `pageBlockSchema`, page helper functions, and page field-array rendering.
- Contact page channels/offices/working-hours/map rendering and field arrays.
- UI markers `Sayfalar`, `İletişim Sayfası Kanalları`, `İletişim Sayfası Ofisleri`, `İletişim Sayfası Çalışma Saatleri`, `İletişim Sayfası Haritası`.

Keep company identity and global public navigation/link/social settings.

- [ ] **Step 4: Update e2e route**

In `admin-public-settings.spec.ts`, replace:

```ts
await page.goto("/dashboard/settings/system");
```

with:

```ts
await page.goto("/dashboard/settings/public-content");
```

Update expected visible texts:

```ts
await expect(page.getByRole("heading", { name: "İçerik Yönetimi" })).toBeVisible();
await expect(page.getByRole("tab", { name: "Sayfalar" })).toBeVisible();
await expect(page.getByRole("tab", { name: "İletişim" })).toBeVisible();
```

- [ ] **Step 5: Run affected frontend tests**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx" "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/app/(admin)/dashboard/(auth)/settings/system/page.tsx frontend/app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx frontend/e2e/tests/admin-public-settings.spec.ts
git commit -m "refactor(admin): separate public content from system settings"
```

---

