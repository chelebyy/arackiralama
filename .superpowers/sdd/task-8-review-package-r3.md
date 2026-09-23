# Task 8 Review Package R3

Base: c563ddf
Head: 2697be8ca478ec94d49c7b47d0176d85ae502f5d

## Commit Log
```
2697be8 fix(admin): save contact edits with draft version
dd32024 fix(admin): preserve unsaved contact edits
9c50c04 feat(admin): add contact content editor
```

## Diff Stat
```
 .../public-content/PublicContentManager.test.tsx   | 150 +++++++++-
 .../admin/public-content/ContactContentEditor.tsx  | 331 +++++++++++++++++++++
 .../admin/public-content/PublicContentManager.tsx  |  35 +--
 3 files changed, 491 insertions(+), 25 deletions(-)
```

## Diff
```diff
diff --git a/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
index a9c1d99..40d4325 100644
--- a/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
+++ b/frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
@@ -1,21 +1,23 @@
 import { render, screen } from "@testing-library/react";
 import userEvent from "@testing-library/user-event";
 import { beforeEach, describe, expect, it, vi } from "vitest";
 import { SWRConfig } from "swr";
 import {
   getAdminPublicContent,
   publishAdminPublicPage,
   unpublishAdminPublicPage,
+  updateAdminPublicContact,
   updateAdminPublicPageDraft,
 } from "@/lib/api/admin/publicContent";
 import type { AdminPublicContent } from "@/lib/api/admin/types";
+import ContactContentEditor from "@/components/admin/public-content/ContactContentEditor";
 import PublicContentPage from "./page";
 
 const adminContentFixture: AdminPublicContent = {
   version: "1",
   updatedAt: "2026-06-27T00:00:00Z",
   pages: [
     {
       id: "page-privacy-tr",
       slug: "privacy",
       locale: "tr",
@@ -32,57 +34,109 @@ const adminContentFixture: AdminPublicContent = {
           body: "<p>Rezervasyon verileri.</p>",
           isVisible: true,
           sortOrder: 99,
         },
       ],
       published: null,
       draftUpdatedAtUtc: null,
       publishedAtUtc: null,
     },
   ],
-  contactPageChannels: [],
-  contactPageOffices: [],
-  contactPageWorkingHours: [],
-  contactPageMapTitle: "",
-  contactPageMapEmbedUrl: "",
+  contactPageChannels: [
+    {
+      id: "contact-whatsapp",
+      type: "whatsapp",
+      label: "WhatsApp",
+      value: "+90 555 000 00 00",
+      href: "https://wa.me/905550000000",
+      description: "Hızlı destek",
+      isVisible: true,
+      sortOrder: 2,
+      translations: {
+        en: {
+          label: "WhatsApp",
+          description: "Fast support",
+        },
+      },
+    },
+  ],
+  contactPageOffices: [
+    {
+      id: "office-alanya",
+      name: "Alanya Ofis",
+      address: "Saray Mahallesi",
+      phone: "+90 242 000 00 00",
+      hours: "09:00-18:00",
+      type: "main",
+      isVisible: true,
+      sortOrder: 1,
+      translations: {
+        en: {
+          name: "Alanya Office",
+          address: "Saray District",
+        },
+      },
+    },
+  ],
+  contactPageWorkingHours: [
+    {
+      id: "hours-weekday",
+      day: "Hafta içi",
+      hours: "09:00-18:00",
+      isVisible: true,
+      sortOrder: 1,
+      translations: {
+        en: {
+          day: "Weekdays",
+          hours: "09:00-18:00",
+        },
+      },
+    },
+  ],
+  contactPageMapTitle: "Alanya Merkez",
+  contactPageMapEmbedUrl: "https://maps.example/embed",
   contactPageMapIsVisible: true,
 };
 
 vi.mock("@/lib/api/admin/publicContent", () => ({
   getAdminPublicContent: vi.fn(),
   publishAdminPublicPage: vi.fn(),
   unpublishAdminPublicPage: vi.fn(),
+  updateAdminPublicContact: vi.fn(),
   updateAdminPublicPageDraft: vi.fn(),
 }));
 
 const getAdminPublicContentMock = vi.mocked(getAdminPublicContent);
 const publishAdminPublicPageMock = vi.mocked(publishAdminPublicPage);
 const unpublishAdminPublicPageMock = vi.mocked(unpublishAdminPublicPage);
+const updateAdminPublicContactMock = vi.mocked(updateAdminPublicContact);
 const updateAdminPublicPageDraftMock = vi.mocked(updateAdminPublicPageDraft);
 
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
     publishAdminPublicPageMock.mockReset();
     unpublishAdminPublicPageMock.mockReset();
+    updateAdminPublicContactMock.mockReset();
     updateAdminPublicPageDraftMock.mockReset();
     getAdminPublicContentMock.mockResolvedValue(adminContentFixture);
     publishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
     unpublishAdminPublicPageMock.mockResolvedValue(adminContentFixture);
+    updateAdminPublicContactMock.mockResolvedValue(adminContentFixture);
     updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
   });
 
   it("renders the public content workspace", async () => {
     renderPublicContentPage();
 
     expect(await screen.findByRole("heading", { name: "İçerik Yönetimi" })).toBeInTheDocument();
     expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
     expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
   });
@@ -124,20 +178,106 @@ describe("PublicContentPage", () => {
           expect.objectContaining({
             id: "privacy-body",
             bodyFormat: "html",
             sortOrder: 0,
           }),
         ],
       }),
     );
   });
 
+  it("saves contact map content", async () => {
+    const user = userEvent.setup();
+    updateAdminPublicContactMock.mockResolvedValue(adminContentFixture);
+    getAdminPublicContentMock.mockResolvedValue(adminContentFixture);
+
+    renderPublicContentPage();
+
+    await user.click(await screen.findByRole("tab", { name: "İletişim" }));
+    await user.clear(screen.getByLabelText("Harita Başlığı"));
+    await user.type(screen.getByLabelText("Harita Başlığı"), "Alanya Ofisleri");
+    await user.clear(screen.getByLabelText("Google Maps Embed URL"));
+    await user.type(screen.getByLabelText("Google Maps Embed URL"), "https://maps.example/new");
+    await user.click(screen.getByLabelText("Harita görünür"));
+    await user.clear(screen.getByLabelText("Kanal 1 Etiket"));
+    await user.type(screen.getByLabelText("Kanal 1 Etiket"), "WhatsApp Destek");
+    await user.clear(screen.getByLabelText("Ofis 1 Ad"));
+    await user.type(screen.getByLabelText("Ofis 1 Ad"), "Damlataş Ofis");
+    await user.clear(screen.getByLabelText("Saat 1"));
+    await user.type(screen.getByLabelText("Saat 1"), "10:00-19:00");
+    await user.click(screen.getByRole("button", { name: "İletişimi Kaydet" }));
+
+    expect(updateAdminPublicContactMock).toHaveBeenCalledWith(
+      expect.objectContaining({
+        version: "1",
+        contactPageMapTitle: "Alanya Ofisleri",
+        contactPageMapEmbedUrl: "https://maps.example/new",
+        contactPageMapIsVisible: false,
+        contactPageChannels: [
+          expect.objectContaining({
+            id: "contact-whatsapp",
+            label: "WhatsApp Destek",
+            sortOrder: 2,
+            translations: {
+              en: {
+                label: "WhatsApp",
+                description: "Fast support",
+              },
+            },
+          }),
+        ],
+        contactPageOffices: [
+          expect.objectContaining({
+            id: "office-alanya",
+            name: "Damlataş Ofis",
+            sortOrder: 1,
+          }),
+        ],
+        contactPageWorkingHours: [
+          expect.objectContaining({
+            id: "hours-weekday",
+            hours: "10:00-19:00",
+            sortOrder: 1,
+          }),
+        ],
+      }),
+    );
+  });
+
+  it("keeps unsaved contact edits and base version when refreshed content arrives", async () => {
+    const user = userEvent.setup();
+    const refreshedContent = {
+      ...adminContentFixture,
+      version: "2",
+      contactPageMapTitle: "Sunucu Yenilemesi",
+    };
+    const onContentChange = vi.fn();
+    updateAdminPublicContactMock.mockResolvedValue(refreshedContent);
+    const { rerender } = render(
+      <ContactContentEditor content={adminContentFixture} onContentChange={onContentChange} />,
+    );
+
+    await user.clear(screen.getByLabelText("Harita Başlığı"));
+    await user.type(screen.getByLabelText("Harita Başlığı"), "Kaydedilmemiş Başlık");
+    rerender(<ContactContentEditor content={refreshedContent} onContentChange={onContentChange} />);
+
+    expect(screen.getByLabelText("Harita Başlığı")).toHaveValue("Kaydedilmemiş Başlık");
+
+    await user.click(screen.getByRole("button", { name: "İletişimi Kaydet" }));
+    expect(updateAdminPublicContactMock).toHaveBeenCalledWith(
+      expect.objectContaining({
+        version: "1",
+        contactPageMapTitle: "Kaydedilmemiş Başlık",
+      }),
+    );
+  });
+
   it("publishes and unpublishes the selected page", async () => {
     const user = userEvent.setup();
 
     renderPublicContentPage();
 
     await user.click(await screen.findByRole("button", { name: "Yayınla" }));
     expect(publishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");
 
     await user.click(screen.getByRole("button", { name: "Yayından Kaldır" }));
     expect(unpublishAdminPublicPageMock).toHaveBeenCalledWith("privacy", "tr", "1");
diff --git a/frontend/components/admin/public-content/ContactContentEditor.tsx b/frontend/components/admin/public-content/ContactContentEditor.tsx
new file mode 100644
index 0000000..3f0f7d5
--- /dev/null
+++ b/frontend/components/admin/public-content/ContactContentEditor.tsx
@@ -0,0 +1,331 @@
+"use client";
+
+import { useEffect, useState } from "react";
+import { Loader2, Save } from "lucide-react";
+import { toast } from "sonner";
+import { Button } from "@/components/ui/button";
+import { Input } from "@/components/ui/input";
+import { Label } from "@/components/ui/label";
+import { Switch } from "@/components/ui/switch";
+import { updateAdminPublicContact } from "@/lib/api/admin/publicContent";
+import type {
+  AdminPublicContent,
+  PublicContactChannel,
+  PublicContactOffice,
+  PublicContactWorkingHour,
+} from "@/lib/api/admin/types";
+
+type ContactContentEditorProps = {
+  content: AdminPublicContent;
+  onContentChange: (content: AdminPublicContent) => void;
+};
+
+const cloneChannels = (channels: PublicContactChannel[]) => channels.map((channel) => ({ ...channel }));
+const cloneOffices = (offices: PublicContactOffice[]) => offices.map((office) => ({ ...office }));
+const cloneWorkingHours = (workingHours: PublicContactWorkingHour[]) =>
+  workingHours.map((workingHour) => ({ ...workingHour }));
+
+export default function ContactContentEditor({ content, onContentChange }: ContactContentEditorProps) {
+  const [mapTitle, setMapTitle] = useState(content.contactPageMapTitle);
+  const [mapEmbedUrl, setMapEmbedUrl] = useState(content.contactPageMapEmbedUrl);
+  const [mapIsVisible, setMapIsVisible] = useState(content.contactPageMapIsVisible);
+  const [channels, setChannels] = useState(() => cloneChannels(content.contactPageChannels));
+  const [offices, setOffices] = useState(() => cloneOffices(content.contactPageOffices));
+  const [workingHours, setWorkingHours] = useState(() => cloneWorkingHours(content.contactPageWorkingHours));
+  const [isSaving, setIsSaving] = useState(false);
+  const [isDirty, setIsDirty] = useState(false);
+  const [draftVersion, setDraftVersion] = useState(content.version);
+
+  useEffect(() => {
+    if (isDirty) {
+      return;
+    }
+
+    setMapTitle(content.contactPageMapTitle);
+    setMapEmbedUrl(content.contactPageMapEmbedUrl);
+    setMapIsVisible(content.contactPageMapIsVisible);
+    setChannels(cloneChannels(content.contactPageChannels));
+    setOffices(cloneOffices(content.contactPageOffices));
+    setWorkingHours(cloneWorkingHours(content.contactPageWorkingHours));
+    setDraftVersion(content.version);
+  }, [content, isDirty]);
+
+  const updateChannel = (
+    channelIndex: number,
+    patch: Partial<Pick<PublicContactChannel, "label" | "value" | "href" | "description" | "type" | "isVisible">>,
+  ) => {
+    setIsDirty(true);
+    setChannels((currentChannels) =>
+      currentChannels.map((channel, index) => (index === channelIndex ? { ...channel, ...patch } : channel)),
+    );
+  };
+
+  const updateOffice = (
+    officeIndex: number,
+    patch: Partial<Pick<PublicContactOffice, "name" | "address" | "phone" | "hours" | "type" | "isVisible">>,
+  ) => {
+    setIsDirty(true);
+    setOffices((currentOffices) =>
+      currentOffices.map((office, index) => (index === officeIndex ? { ...office, ...patch } : office)),
+    );
+  };
+
+  const updateWorkingHour = (
+    workingHourIndex: number,
+    patch: Partial<Pick<PublicContactWorkingHour, "day" | "hours" | "isVisible">>,
+  ) => {
+    setIsDirty(true);
+    setWorkingHours((currentWorkingHours) =>
+      currentWorkingHours.map((workingHour, index) =>
+        index === workingHourIndex ? { ...workingHour, ...patch } : workingHour,
+      ),
+    );
+  };
+
+  const saveContact = async () => {
+    setIsSaving(true);
+
+    try {
+      const nextContent = await updateAdminPublicContact({
+        version: draftVersion,
+        contactPageChannels: channels,
+        contactPageOffices: offices,
+        contactPageWorkingHours: workingHours,
+        contactPageMapTitle: mapTitle,
+        contactPageMapEmbedUrl: mapEmbedUrl,
+        contactPageMapIsVisible: mapIsVisible,
+      });
+
+      setIsDirty(false);
+      setDraftVersion(nextContent.version);
+      onContentChange(nextContent);
+      toast.success("İletişim içeriği kaydedildi.");
+    } catch (error) {
+      toast.error(error instanceof Error && error.message ? error.message : "İletişim içeriği kaydedilemedi.");
+    } finally {
+      setIsSaving(false);
+    }
+  };
+
+  return (
+    <div className="space-y-4 rounded-md border p-4">
+      <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-4">
+        <div>
+          <h2 className="text-base font-semibold">İletişim</h2>
+          <div className="text-sm text-muted-foreground">Sürüm {content.version}</div>
+        </div>
+        <Button type="button" onClick={saveContact} disabled={isSaving}>
+          {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
+          İletişimi Kaydet
+        </Button>
+      </div>
+
+      <section className="space-y-3">
+        <h3 className="text-sm font-medium">Harita</h3>
+        <div className="grid gap-3 md:grid-cols-2">
+          <div className="space-y-2">
+            <Label htmlFor="contact-map-title">Harita Başlığı</Label>
+            <Input
+              id="contact-map-title"
+              value={mapTitle}
+              onChange={(event) => {
+                setIsDirty(true);
+                setMapTitle(event.target.value);
+              }}
+            />
+          </div>
+          <div className="space-y-2">
+            <Label htmlFor="contact-map-embed-url">Google Maps Embed URL</Label>
+            <Input
+              id="contact-map-embed-url"
+              value={mapEmbedUrl}
+              onChange={(event) => {
+                setIsDirty(true);
+                setMapEmbedUrl(event.target.value);
+              }}
+            />
+          </div>
+        </div>
+        <div className="flex items-center gap-2">
+          <Switch
+            id="contact-map-visible"
+            checked={mapIsVisible}
+            onCheckedChange={(isVisible) => {
+              setIsDirty(true);
+              setMapIsVisible(isVisible);
+            }}
+          />
+          <Label htmlFor="contact-map-visible">Harita görünür</Label>
+        </div>
+      </section>
+
+      <section className="space-y-3 border-t pt-4">
+        <h3 className="text-sm font-medium">Kanallar</h3>
+        {channels.length === 0 ? (
+          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">Kanal bulunamadı.</div>
+        ) : (
+          channels.map((channel, index) => (
+            <div key={channel.id} className="space-y-3 rounded-md border p-3">
+              <div className="flex flex-wrap items-center justify-between gap-2">
+                <div className="text-sm font-medium">Kanal {index + 1}</div>
+                <div className="flex items-center gap-2">
+                  <Switch
+                    id={`contact-channel-visible-${index}`}
+                    checked={channel.isVisible}
+                    onCheckedChange={(isVisible) => updateChannel(index, { isVisible })}
+                  />
+                  <Label htmlFor={`contact-channel-visible-${index}`}>Görünür</Label>
+                </div>
+              </div>
+              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-channel-label-${index}`}>Kanal {index + 1} Etiket</Label>
+                  <Input
+                    id={`contact-channel-label-${index}`}
+                    value={channel.label}
+                    onChange={(event) => updateChannel(index, { label: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-channel-value-${index}`}>Kanal {index + 1} Değer</Label>
+                  <Input
+                    id={`contact-channel-value-${index}`}
+                    value={channel.value}
+                    onChange={(event) => updateChannel(index, { value: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-channel-href-${index}`}>Kanal {index + 1} Bağlantı</Label>
+                  <Input
+                    id={`contact-channel-href-${index}`}
+                    value={channel.href}
+                    onChange={(event) => updateChannel(index, { href: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-channel-description-${index}`}>Kanal {index + 1} Açıklama</Label>
+                  <Input
+                    id={`contact-channel-description-${index}`}
+                    value={channel.description}
+                    onChange={(event) => updateChannel(index, { description: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-channel-type-${index}`}>Kanal {index + 1} Tip</Label>
+                  <Input
+                    id={`contact-channel-type-${index}`}
+                    value={channel.type}
+                    onChange={(event) => updateChannel(index, { type: event.target.value })}
+                  />
+                </div>
+              </div>
+            </div>
+          ))
+        )}
+      </section>
+
+      <section className="space-y-3 border-t pt-4">
+        <h3 className="text-sm font-medium">Ofisler</h3>
+        {offices.length === 0 ? (
+          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">Ofis bulunamadı.</div>
+        ) : (
+          offices.map((office, index) => (
+            <div key={office.id} className="space-y-3 rounded-md border p-3">
+              <div className="flex flex-wrap items-center justify-between gap-2">
+                <div className="text-sm font-medium">Ofis {index + 1}</div>
+                <div className="flex items-center gap-2">
+                  <Switch
+                    id={`contact-office-visible-${index}`}
+                    checked={office.isVisible}
+                    onCheckedChange={(isVisible) => updateOffice(index, { isVisible })}
+                  />
+                  <Label htmlFor={`contact-office-visible-${index}`}>Görünür</Label>
+                </div>
+              </div>
+              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-office-name-${index}`}>Ofis {index + 1} Ad</Label>
+                  <Input
+                    id={`contact-office-name-${index}`}
+                    value={office.name}
+                    onChange={(event) => updateOffice(index, { name: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-office-address-${index}`}>Ofis {index + 1} Adres</Label>
+                  <Input
+                    id={`contact-office-address-${index}`}
+                    value={office.address}
+                    onChange={(event) => updateOffice(index, { address: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-office-phone-${index}`}>Ofis {index + 1} Telefon</Label>
+                  <Input
+                    id={`contact-office-phone-${index}`}
+                    value={office.phone}
+                    onChange={(event) => updateOffice(index, { phone: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-office-hours-${index}`}>Ofis {index + 1} Saat</Label>
+                  <Input
+                    id={`contact-office-hours-${index}`}
+                    value={office.hours}
+                    onChange={(event) => updateOffice(index, { hours: event.target.value })}
+                  />
+                </div>
+                <div className="space-y-2">
+                  <Label htmlFor={`contact-office-type-${index}`}>Ofis {index + 1} Tip</Label>
+                  <Input
+                    id={`contact-office-type-${index}`}
+                    value={office.type}
+                    onChange={(event) => updateOffice(index, { type: event.target.value })}
+                  />
+                </div>
+              </div>
+            </div>
+          ))
+        )}
+      </section>
+
+      <section className="space-y-3 border-t pt-4">
+        <h3 className="text-sm font-medium">Çalışma Saatleri</h3>
+        {workingHours.length === 0 ? (
+          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
+            Çalışma saati bulunamadı.
+          </div>
+        ) : (
+          workingHours.map((workingHour, index) => (
+            <div key={workingHour.id} className="grid gap-3 rounded-md border p-3 md:grid-cols-[1fr_1fr_auto]">
+              <div className="space-y-2">
+                <Label htmlFor={`contact-working-hour-day-${index}`}>Gün {index + 1}</Label>
+                <Input
+                  id={`contact-working-hour-day-${index}`}
+                  value={workingHour.day}
+                  onChange={(event) => updateWorkingHour(index, { day: event.target.value })}
+                />
+              </div>
+              <div className="space-y-2">
+                <Label htmlFor={`contact-working-hour-hours-${index}`}>Saat {index + 1}</Label>
+                <Input
+                  id={`contact-working-hour-hours-${index}`}
+                  value={workingHour.hours}
+                  onChange={(event) => updateWorkingHour(index, { hours: event.target.value })}
+                />
+              </div>
+              <div className="flex items-end gap-2 pb-2">
+                <Switch
+                  id={`contact-working-hour-visible-${index}`}
+                  checked={workingHour.isVisible}
+                  onCheckedChange={(isVisible) => updateWorkingHour(index, { isVisible })}
+                />
+                <Label htmlFor={`contact-working-hour-visible-${index}`}>Görünür</Label>
+              </div>
+            </div>
+          ))
+        )}
+      </section>
+    </div>
+  );
+}
diff --git a/frontend/components/admin/public-content/PublicContentManager.tsx b/frontend/components/admin/public-content/PublicContentManager.tsx
index 5e2945e..ae16c27 100644
--- a/frontend/components/admin/public-content/PublicContentManager.tsx
+++ b/frontend/components/admin/public-content/PublicContentManager.tsx
@@ -1,19 +1,20 @@
 "use client";
 
 import { FileText, Phone } from "lucide-react";
 import useSWR from "swr";
 import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
 import { Skeleton } from "@/components/ui/skeleton";
 import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
 import { getAdminPublicContent } from "@/lib/api/admin/publicContent";
 import type { AdminPublicContent } from "@/lib/api/admin/types";
+import ContactContentEditor from "./ContactContentEditor";
 import PageContentEditor from "./PageContentEditor";
 
 const PUBLIC_CONTENT_CACHE_KEY = "admin-public-content";
 
 export default function PublicContentManager() {
   const { data, error, isLoading, mutate } = useSWR<AdminPublicContent>(
     PUBLIC_CONTENT_CACHE_KEY,
     () => getAdminPublicContent(),
   );
   const hasLoadError = !data && (error || !isLoading);
@@ -60,36 +61,30 @@ export default function PublicContentManager() {
               <PageContentEditor
                 content={data}
                 onContentChange={(nextContent) => {
                   void mutate(nextContent, false);
                 }}
               />
             )}
           </TabsContent>
 
           <TabsContent value="contact">
-            <Card>
-              <CardHeader>
-                <CardTitle className="text-base">İletişim</CardTitle>
-              </CardHeader>
-              <CardContent className="grid gap-3 text-sm text-muted-foreground md:grid-cols-3">
-                {isLoading || !data ? (
-                  <>
-                    <Skeleton className="h-10 w-full" />
-                    <Skeleton className="h-10 w-full" />
-                    <Skeleton className="h-10 w-full" />
-                  </>
-                ) : (
-                  <>
-                    <div>{data.contactPageChannels.length} kanal</div>
-                    <div>{data.contactPageOffices.length} ofis</div>
-                    <div>{data.contactPageWorkingHours.length} çalışma saati</div>
-                  </>
-                )}
-              </CardContent>
-            </Card>
+            {isLoading || !data ? (
+              <div className="space-y-3 rounded-md border p-4">
+                <Skeleton className="h-10 w-full" />
+                <Skeleton className="h-10 w-full" />
+                <Skeleton className="h-24 w-full" />
+              </div>
+            ) : (
+              <ContactContentEditor
+                content={data}
+                onContentChange={(nextContent) => {
+                  void mutate(nextContent, false);
+                }}
+              />
+            )}
           </TabsContent>
         </Tabs>
       )}
     </div>
   );
 }
```
