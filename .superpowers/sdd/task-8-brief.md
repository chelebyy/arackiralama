### Task 8: Contact Content Editor

**Files:**
- Create: `frontend/components/admin/public-content/ContactContentEditor.tsx`
- Modify: `frontend/components/admin/public-content/PublicContentManager.tsx`
- Test: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx`

**Interfaces:**
- Consumes: `AdminPublicContent.contactPageChannels`, offices, working hours, map fields.
- Produces: editable contact content form using `updateAdminPublicContact`.

- [ ] **Step 1: Add contact save test**

Add:

```tsx
it("saves contact map content", async () => {
  const user = userEvent.setup();
  updateAdminPublicContactMock.mockResolvedValue(adminContentFixture);
  getAdminPublicContentMock.mockResolvedValue(adminContentFixture);

  render(<PublicContentPage />);

  await user.click(await screen.findByRole("tab", { name: "İletişim" }));
  await user.clear(screen.getByLabelText("Harita Başlığı"));
  await user.type(screen.getByLabelText("Harita Başlığı"), "Alanya Ofisleri");
  await user.click(screen.getByRole("button", { name: "İletişimi Kaydet" }));

  expect(updateAdminPublicContactMock).toHaveBeenCalledWith(
    expect.objectContaining({ version: "1", contactPageMapTitle: "Alanya Ofisleri" })
  );
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: FAIL because contact editor is not implemented.

- [ ] **Step 3: Implement contact editor**

Create `ContactContentEditor.tsx`:

```tsx
"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import type { AdminPublicContent } from "@/lib/api/admin/types";
import { updateAdminPublicContact } from "@/lib/api/admin/publicContent";

type ContactContentEditorProps = {
  content: AdminPublicContent;
  onContentChange: (content: AdminPublicContent) => void;
};

export default function ContactContentEditor({ content, onContentChange }: ContactContentEditorProps) {
  const [mapTitle, setMapTitle] = useState(content.contactPageMapTitle);
  const [mapEmbedUrl, setMapEmbedUrl] = useState(content.contactPageMapEmbedUrl);
  const [mapVisible, setMapVisible] = useState(content.contactPageMapIsVisible);

  async function saveContact() {
    const next = await updateAdminPublicContact({
      version: content.version,
      contactPageChannels: content.contactPageChannels,
      contactPageOffices: content.contactPageOffices,
      contactPageWorkingHours: content.contactPageWorkingHours,
      contactPageMapTitle: mapTitle,
      contactPageMapEmbedUrl: mapEmbedUrl,
      contactPageMapIsVisible: mapVisible,
    });
    onContentChange(next);
    toast.success("İletişim içeriği kaydedildi.");
  }

  return (
    <div className="space-y-4 rounded-md border p-4">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="contact-map-title">Harita Başlığı</Label>
          <Input id="contact-map-title" value={mapTitle} onChange={(event) => setMapTitle(event.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="contact-map-url">Google Maps Embed URL</Label>
          <Input id="contact-map-url" value={mapEmbedUrl} onChange={(event) => setMapEmbedUrl(event.target.value)} />
        </div>
      </div>
      <div className="flex items-center gap-2">
        <Switch checked={mapVisible} onCheckedChange={setMapVisible} />
        <span className="text-sm">Harita görünsün</span>
      </div>
      <div className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
        Kanal, ofis ve çalışma saati listeleri mevcut veri modelinden yüklendi. Bu task harita kaydını bağlar; liste satırı düzenleme aynı component içinde sonraki küçük adımda genişletilir.
      </div>
      <Button type="button" onClick={saveContact}>İletişimi Kaydet</Button>
    </div>
  );
}
```

Wire it into `PublicContentManager`:

```tsx
<TabsContent value="contact">
  <ContactContentEditor content={data} onContentChange={(next) => mutate(next, false)} />
</TabsContent>
```

- [ ] **Step 4: Extend contact editor list rows**

Add editable row components inside `ContactContentEditor` for:

```ts
contactPageChannels: { label, value, href, description, type, isVisible }
contactPageOffices: { name, address, phone, hours, type, isVisible }
contactPageWorkingHours: { day, hours, isVisible }
```

Each row must use `Input` and `Switch`, and update local state arrays before `saveContact()`. The saved payload must include the edited arrays.

- [ ] **Step 5: Run contact tests**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/components/admin/public-content/ContactContentEditor.tsx frontend/components/admin/public-content/PublicContentManager.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
git commit -m "feat(admin): add contact content editor"
```

---

