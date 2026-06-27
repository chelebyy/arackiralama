"use client";

import { useEffect, useState } from "react";
import { Loader2, Save } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { updateAdminPublicContact } from "@/lib/api/admin/publicContent";
import type {
  AdminPublicContent,
  PublicContactChannel,
  PublicContactOffice,
  PublicContactWorkingHour,
} from "@/lib/api/admin/types";

type ContactContentEditorProps = {
  content: AdminPublicContent;
  onContentChange: (content: AdminPublicContent) => void;
};

const cloneChannels = (channels: PublicContactChannel[]) => channels.map((channel) => ({ ...channel }));
const cloneOffices = (offices: PublicContactOffice[]) => offices.map((office) => ({ ...office }));
const cloneWorkingHours = (workingHours: PublicContactWorkingHour[]) =>
  workingHours.map((workingHour) => ({ ...workingHour }));

export default function ContactContentEditor({ content, onContentChange }: ContactContentEditorProps) {
  const [mapTitle, setMapTitle] = useState(content.contactPageMapTitle);
  const [mapEmbedUrl, setMapEmbedUrl] = useState(content.contactPageMapEmbedUrl);
  const [mapIsVisible, setMapIsVisible] = useState(content.contactPageMapIsVisible);
  const [channels, setChannels] = useState(() => cloneChannels(content.contactPageChannels));
  const [offices, setOffices] = useState(() => cloneOffices(content.contactPageOffices));
  const [workingHours, setWorkingHours] = useState(() => cloneWorkingHours(content.contactPageWorkingHours));
  const [isSaving, setIsSaving] = useState(false);
  const [isDirty, setIsDirty] = useState(false);
  const [draftVersion, setDraftVersion] = useState(content.version);

  useEffect(() => {
    if (isDirty) {
      return;
    }

    setMapTitle(content.contactPageMapTitle);
    setMapEmbedUrl(content.contactPageMapEmbedUrl);
    setMapIsVisible(content.contactPageMapIsVisible);
    setChannels(cloneChannels(content.contactPageChannels));
    setOffices(cloneOffices(content.contactPageOffices));
    setWorkingHours(cloneWorkingHours(content.contactPageWorkingHours));
    setDraftVersion(content.version);
  }, [content, isDirty]);

  const updateChannel = (
    channelIndex: number,
    patch: Partial<Pick<PublicContactChannel, "label" | "value" | "href" | "description" | "type" | "isVisible">>,
  ) => {
    setIsDirty(true);
    setChannels((currentChannels) =>
      currentChannels.map((channel, index) => (index === channelIndex ? { ...channel, ...patch } : channel)),
    );
  };

  const updateOffice = (
    officeIndex: number,
    patch: Partial<Pick<PublicContactOffice, "name" | "address" | "phone" | "hours" | "type" | "isVisible">>,
  ) => {
    setIsDirty(true);
    setOffices((currentOffices) =>
      currentOffices.map((office, index) => (index === officeIndex ? { ...office, ...patch } : office)),
    );
  };

  const updateWorkingHour = (
    workingHourIndex: number,
    patch: Partial<Pick<PublicContactWorkingHour, "day" | "hours" | "isVisible">>,
  ) => {
    setIsDirty(true);
    setWorkingHours((currentWorkingHours) =>
      currentWorkingHours.map((workingHour, index) =>
        index === workingHourIndex ? { ...workingHour, ...patch } : workingHour,
      ),
    );
  };

  const saveContact = async () => {
    setIsSaving(true);

    try {
      const nextContent = await updateAdminPublicContact({
        version: draftVersion,
        contactPageChannels: channels,
        contactPageOffices: offices,
        contactPageWorkingHours: workingHours,
        contactPageMapTitle: mapTitle,
        contactPageMapEmbedUrl: mapEmbedUrl,
        contactPageMapIsVisible: mapIsVisible,
      });

      setIsDirty(false);
      setDraftVersion(nextContent.version);
      onContentChange(nextContent);
      toast.success("İletişim içeriği kaydedildi.");
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : "İletişim içeriği kaydedilemedi.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-4 rounded-md border p-4">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-4">
        <div>
          <h2 className="text-base font-semibold">İletişim</h2>
          <div className="text-sm text-muted-foreground">Sürüm {content.version}</div>
        </div>
        <Button type="button" onClick={saveContact} disabled={isSaving}>
          {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          İletişimi Kaydet
        </Button>
      </div>

      <section className="space-y-3">
        <h3 className="text-sm font-medium">Harita</h3>
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="contact-map-title">Harita Başlığı</Label>
            <Input
              id="contact-map-title"
              value={mapTitle}
              onChange={(event) => {
                setIsDirty(true);
                setMapTitle(event.target.value);
              }}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="contact-map-embed-url">Google Maps Embed URL</Label>
            <Input
              id="contact-map-embed-url"
              value={mapEmbedUrl}
              onChange={(event) => {
                setIsDirty(true);
                setMapEmbedUrl(event.target.value);
              }}
            />
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Switch
            id="contact-map-visible"
            checked={mapIsVisible}
            onCheckedChange={(isVisible) => {
              setIsDirty(true);
              setMapIsVisible(isVisible);
            }}
          />
          <Label htmlFor="contact-map-visible">Harita görünür</Label>
        </div>
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">Kanallar</h3>
        {channels.length === 0 ? (
          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">Kanal bulunamadı.</div>
        ) : (
          channels.map((channel, index) => (
            <div key={channel.id} className="space-y-3 rounded-md border p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="text-sm font-medium">Kanal {index + 1}</div>
                <div className="flex items-center gap-2">
                  <Switch
                    id={`contact-channel-visible-${index}`}
                    checked={channel.isVisible}
                    onCheckedChange={(isVisible) => updateChannel(index, { isVisible })}
                  />
                  <Label htmlFor={`contact-channel-visible-${index}`}>Görünür</Label>
                </div>
              </div>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                <div className="space-y-2">
                  <Label htmlFor={`contact-channel-label-${index}`}>Kanal {index + 1} Etiket</Label>
                  <Input
                    id={`contact-channel-label-${index}`}
                    value={channel.label}
                    onChange={(event) => updateChannel(index, { label: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-channel-value-${index}`}>Kanal {index + 1} Değer</Label>
                  <Input
                    id={`contact-channel-value-${index}`}
                    value={channel.value}
                    onChange={(event) => updateChannel(index, { value: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-channel-href-${index}`}>Kanal {index + 1} Bağlantı</Label>
                  <Input
                    id={`contact-channel-href-${index}`}
                    value={channel.href}
                    onChange={(event) => updateChannel(index, { href: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-channel-description-${index}`}>Kanal {index + 1} Açıklama</Label>
                  <Input
                    id={`contact-channel-description-${index}`}
                    value={channel.description}
                    onChange={(event) => updateChannel(index, { description: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-channel-type-${index}`}>Kanal {index + 1} Tip</Label>
                  <Input
                    id={`contact-channel-type-${index}`}
                    value={channel.type}
                    onChange={(event) => updateChannel(index, { type: event.target.value })}
                  />
                </div>
              </div>
            </div>
          ))
        )}
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">Ofisler</h3>
        {offices.length === 0 ? (
          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">Ofis bulunamadı.</div>
        ) : (
          offices.map((office, index) => (
            <div key={office.id} className="space-y-3 rounded-md border p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="text-sm font-medium">Ofis {index + 1}</div>
                <div className="flex items-center gap-2">
                  <Switch
                    id={`contact-office-visible-${index}`}
                    checked={office.isVisible}
                    onCheckedChange={(isVisible) => updateOffice(index, { isVisible })}
                  />
                  <Label htmlFor={`contact-office-visible-${index}`}>Görünür</Label>
                </div>
              </div>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                <div className="space-y-2">
                  <Label htmlFor={`contact-office-name-${index}`}>Ofis {index + 1} Ad</Label>
                  <Input
                    id={`contact-office-name-${index}`}
                    value={office.name}
                    onChange={(event) => updateOffice(index, { name: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-office-address-${index}`}>Ofis {index + 1} Adres</Label>
                  <Input
                    id={`contact-office-address-${index}`}
                    value={office.address}
                    onChange={(event) => updateOffice(index, { address: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-office-phone-${index}`}>Ofis {index + 1} Telefon</Label>
                  <Input
                    id={`contact-office-phone-${index}`}
                    value={office.phone}
                    onChange={(event) => updateOffice(index, { phone: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-office-hours-${index}`}>Ofis {index + 1} Saat</Label>
                  <Input
                    id={`contact-office-hours-${index}`}
                    value={office.hours}
                    onChange={(event) => updateOffice(index, { hours: event.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor={`contact-office-type-${index}`}>Ofis {index + 1} Tip</Label>
                  <Input
                    id={`contact-office-type-${index}`}
                    value={office.type}
                    onChange={(event) => updateOffice(index, { type: event.target.value })}
                  />
                </div>
              </div>
            </div>
          ))
        )}
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">Çalışma Saatleri</h3>
        {workingHours.length === 0 ? (
          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
            Çalışma saati bulunamadı.
          </div>
        ) : (
          workingHours.map((workingHour, index) => (
            <div key={workingHour.id} className="grid gap-3 rounded-md border p-3 md:grid-cols-[1fr_1fr_auto]">
              <div className="space-y-2">
                <Label htmlFor={`contact-working-hour-day-${index}`}>Gün {index + 1}</Label>
                <Input
                  id={`contact-working-hour-day-${index}`}
                  value={workingHour.day}
                  onChange={(event) => updateWorkingHour(index, { day: event.target.value })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor={`contact-working-hour-hours-${index}`}>Saat {index + 1}</Label>
                <Input
                  id={`contact-working-hour-hours-${index}`}
                  value={workingHour.hours}
                  onChange={(event) => updateWorkingHour(index, { hours: event.target.value })}
                />
              </div>
              <div className="flex items-end gap-2 pb-2">
                <Switch
                  id={`contact-working-hour-visible-${index}`}
                  checked={workingHour.isVisible}
                  onCheckedChange={(isVisible) => updateWorkingHour(index, { isVisible })}
                />
                <Label htmlFor={`contact-working-hour-visible-${index}`}>Görünür</Label>
              </div>
            </div>
          ))
        )}
      </section>
    </div>
  );
}
