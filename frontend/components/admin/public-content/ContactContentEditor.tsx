"use client";

import { useEffect, useRef, useState } from "react";
import { Loader2, Save } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import { updateAdminPublicContact } from "@/lib/api/admin/publicContent";
import type {
  AdminPublicContent,
  PublicContactChannel,
  PublicContactOffice,
  PublicContactWorkingHour,
  PublicLocalizedText,
  PublicSettingsLocale,
} from "@/lib/api/admin/types";

type ContactContentEditorProps = {
  content: AdminPublicContent;
  onContentChange: (content: AdminPublicContent) => void;
};

const cloneChannels = (channels: PublicContactChannel[]) => channels.map((channel) => ({ ...channel }));
const cloneOffices = (offices: PublicContactOffice[]) => offices.map((office) => ({ ...office }));
const cloneWorkingHours = (workingHours: PublicContactWorkingHour[]) =>
  workingHours.map((workingHour) => ({ ...workingHour }));
const translationLocales = ["en", "ru", "ar", "de"] satisfies PublicSettingsLocale[];

export default function ContactContentEditor({ content, onContentChange }: ContactContentEditorProps) {
  const [mapTitle, setMapTitle] = useState(content.contactPageMapTitle);
  const [mapEmbedUrl, setMapEmbedUrl] = useState(content.contactPageMapEmbedUrl);
  const [mapIsVisible, setMapIsVisible] = useState(content.contactPageMapIsVisible);
  const [channels, setChannels] = useState(() => cloneChannels(content.contactPageChannels));
  const [offices, setOffices] = useState(() => cloneOffices(content.contactPageOffices));
  const [workingHours, setWorkingHours] = useState(() => cloneWorkingHours(content.contactPageWorkingHours));
  const [isSaving, setIsSaving] = useState(false);
  const [isDirty, setIsDirty] = useState(false);
  const isDirtyRef = useRef(false);
  const [draftVersion, setDraftVersion] = useState(content.version);
  const saveStateLabel = isSaving ? "Kaydediliyor" : isDirty ? "Kaydedilmemiş değişiklik" : "Kaydedildi";

  useEffect(() => {
    if (isDirtyRef.current) {
      return;
    }

    setMapTitle(content.contactPageMapTitle);
    setMapEmbedUrl(content.contactPageMapEmbedUrl);
    setMapIsVisible(content.contactPageMapIsVisible);
    setChannels(cloneChannels(content.contactPageChannels));
    setOffices(cloneOffices(content.contactPageOffices));
    setWorkingHours(cloneWorkingHours(content.contactPageWorkingHours));
    setDraftVersion(content.version);
  }, [content]);

  const markDirty = () => {
    isDirtyRef.current = true;
    setIsDirty(true);
  };

  const updateChannel = (
    channelIndex: number,
    patch: Partial<Pick<PublicContactChannel, "label" | "value" | "href" | "description" | "type" | "isVisible">>,
  ) => {
    markDirty();
    setChannels((currentChannels) =>
      currentChannels.map((channel, index) => (index === channelIndex ? { ...channel, ...patch } : channel)),
    );
  };

  const updateChannelTranslation = (
    channelIndex: number,
    locale: PublicSettingsLocale,
    patch: Partial<Pick<PublicLocalizedText, "label" | "value" | "description">>,
  ) => {
    markDirty();
    setChannels((currentChannels) =>
      currentChannels.map((channel, index) =>
        index === channelIndex
          ? {
              ...channel,
              translations: {
                ...(channel.translations ?? {}),
                [locale]: {
                  ...(channel.translations?.[locale] ?? {}),
                  ...patch,
                },
              },
            }
          : channel,
      ),
    );
  };

  const updateOffice = (
    officeIndex: number,
    patch: Partial<Pick<PublicContactOffice, "name" | "address" | "phone" | "hours" | "type" | "isVisible">>,
  ) => {
    markDirty();
    setOffices((currentOffices) =>
      currentOffices.map((office, index) => (index === officeIndex ? { ...office, ...patch } : office)),
    );
  };

  const updateOfficeTranslation = (
    officeIndex: number,
    locale: PublicSettingsLocale,
    patch: Partial<Pick<PublicLocalizedText, "name" | "address" | "hours">>,
  ) => {
    markDirty();
    setOffices((currentOffices) =>
      currentOffices.map((office, index) =>
        index === officeIndex
          ? {
              ...office,
              translations: {
                ...(office.translations ?? {}),
                [locale]: {
                  ...(office.translations?.[locale] ?? {}),
                  ...patch,
                },
              },
            }
          : office,
      ),
    );
  };

  const updateWorkingHour = (
    workingHourIndex: number,
    patch: Partial<Pick<PublicContactWorkingHour, "day" | "hours" | "isVisible">>,
  ) => {
    markDirty();
    setWorkingHours((currentWorkingHours) =>
      currentWorkingHours.map((workingHour, index) =>
        index === workingHourIndex ? { ...workingHour, ...patch } : workingHour,
      ),
    );
  };

  const updateWorkingHourTranslation = (
    workingHourIndex: number,
    locale: PublicSettingsLocale,
    patch: Partial<Pick<PublicLocalizedText, "day" | "hours">>,
  ) => {
    markDirty();
    setWorkingHours((currentWorkingHours) =>
      currentWorkingHours.map((workingHour, index) =>
        index === workingHourIndex
          ? {
              ...workingHour,
              translations: {
                ...(workingHour.translations ?? {}),
                [locale]: {
                  ...(workingHour.translations?.[locale] ?? {}),
                  ...patch,
                },
              },
            }
          : workingHour,
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

      isDirtyRef.current = false;
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
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <Badge variant="outline">Global iletişim</Badge>
            <Badge variant={isDirty ? "warning" : "success"}>{saveStateLabel}</Badge>
            <span>Sürüm {content.version}</span>
          </div>
        </div>
        <Button type="button" onClick={saveContact} disabled={isSaving}>
          {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          İletişimi Kaydet
        </Button>
      </div>

      <div
        aria-live="polite"
        className={cn(
          "rounded-md border px-3 py-2 text-sm",
          isDirty ? "border-orange-300 bg-orange-50 text-orange-900" : "border-green-200 bg-green-50 text-green-900",
        )}
      >
        {isDirty
          ? "İletişim alanlarında kaydedilmemiş değişiklik var. Gizli satırlar admin görünümünde kalır ama public sitede gösterilmez."
          : "Son kaydedilen global iletişim bilgileri gösteriliyor. Dil kutuları yalnızca public metin çevirilerini değiştirir."}
      </div>

      <section className="space-y-3">
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-medium">Harita</h3>
            <Badge variant={mapIsVisible ? "success" : "secondary"}>{mapIsVisible ? "Görünür" : "Gizli"}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            Başlık, embed URL ve görünürlük tüm diller için ortak public iletişim alanıdır.
          </p>
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="contact-map-title">Harita Başlığı</Label>
            <Input
              id="contact-map-title"
              value={mapTitle}
              onChange={(event) => {
                markDirty();
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
                markDirty();
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
              markDirty();
              setMapIsVisible(isVisible);
            }}
          />
          <Label htmlFor="contact-map-visible">Harita görünür</Label>
        </div>
      </section>

      <section className="space-y-3 border-t pt-4">
        <div className="space-y-1">
          <h3 className="text-sm font-medium">Kanallar</h3>
          <p className="text-sm text-muted-foreground">
            Temel kanal bilgileri globaldir; alttaki dil alanları public metin override değerleridir.
          </p>
        </div>
        {channels.length === 0 ? (
          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">Kanal bulunamadı.</div>
        ) : (
          channels.map((channel, index) => (
            <div
              key={channel.id}
              className={cn("space-y-3 rounded-md border p-3", !channel.isVisible && "border-dashed bg-muted/20")}
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex flex-wrap items-center gap-2">
                  <div className="text-sm font-medium">Kanal {index + 1}</div>
                  <Badge variant={channel.isVisible ? "success" : "secondary"}>
                    {channel.isVisible ? "Public sitede görünür" : "Public sitede gizli"}
                  </Badge>
                </div>
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
              <div className="text-xs font-medium uppercase text-muted-foreground">Dil override alanları</div>
              <div className="grid gap-3 rounded-md bg-muted/20 p-3 md:grid-cols-2 xl:grid-cols-4">
                {translationLocales.map((locale) => (
                  <div key={locale} className="space-y-2">
                    <Label htmlFor={`contact-channel-${locale}-label-${index}`}>
                      Kanal {index + 1} {locale.toUpperCase()} Etiket
                    </Label>
                    <Input
                      id={`contact-channel-${locale}-label-${index}`}
                      value={channel.translations?.[locale]?.label ?? ""}
                      onChange={(event) => updateChannelTranslation(index, locale, { label: event.target.value })}
                    />
                    <Label htmlFor={`contact-channel-${locale}-description-${index}`}>
                      Kanal {index + 1} {locale.toUpperCase()} Açıklama
                    </Label>
                    <Input
                      id={`contact-channel-${locale}-description-${index}`}
                      value={channel.translations?.[locale]?.description ?? ""}
                      onChange={(event) => updateChannelTranslation(index, locale, { description: event.target.value })}
                    />
                  </div>
                ))}
              </div>
            </div>
          ))
        )}
      </section>

      <section className="space-y-3 border-t pt-4">
        <div className="space-y-1">
          <h3 className="text-sm font-medium">Ofisler</h3>
          <p className="text-sm text-muted-foreground">
            Ofis satırları global iletişim bilgisidir; gizli satırlar kayıttan silinmez, public sitede pasif kalır.
          </p>
        </div>
        {offices.length === 0 ? (
          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">Ofis bulunamadı.</div>
        ) : (
          offices.map((office, index) => (
            <div
              key={office.id}
              className={cn("space-y-3 rounded-md border p-3", !office.isVisible && "border-dashed bg-muted/20")}
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex flex-wrap items-center gap-2">
                  <div className="text-sm font-medium">Ofis {index + 1}</div>
                  <Badge variant={office.isVisible ? "success" : "secondary"}>
                    {office.isVisible ? "Public sitede görünür" : "Public sitede gizli"}
                  </Badge>
                </div>
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
              <div className="text-xs font-medium uppercase text-muted-foreground">Dil override alanları</div>
              <div className="grid gap-3 rounded-md bg-muted/20 p-3 md:grid-cols-2 xl:grid-cols-4">
                {translationLocales.map((locale) => (
                  <div key={locale} className="space-y-2">
                    <Label htmlFor={`contact-office-${locale}-name-${index}`}>
                      Ofis {index + 1} {locale.toUpperCase()} Ad
                    </Label>
                    <Input
                      id={`contact-office-${locale}-name-${index}`}
                      value={office.translations?.[locale]?.name ?? ""}
                      onChange={(event) => updateOfficeTranslation(index, locale, { name: event.target.value })}
                    />
                    <Label htmlFor={`contact-office-${locale}-address-${index}`}>
                      Ofis {index + 1} {locale.toUpperCase()} Adres
                    </Label>
                    <Input
                      id={`contact-office-${locale}-address-${index}`}
                      value={office.translations?.[locale]?.address ?? ""}
                      onChange={(event) => updateOfficeTranslation(index, locale, { address: event.target.value })}
                    />
                    <Label htmlFor={`contact-office-${locale}-hours-${index}`}>
                      Ofis {index + 1} {locale.toUpperCase()} Saat
                    </Label>
                    <Input
                      id={`contact-office-${locale}-hours-${index}`}
                      value={office.translations?.[locale]?.hours ?? ""}
                      onChange={(event) => updateOfficeTranslation(index, locale, { hours: event.target.value })}
                    />
                  </div>
                ))}
              </div>
            </div>
          ))
        )}
      </section>

      <section className="space-y-3 border-t pt-4">
        <div className="space-y-1">
          <h3 className="text-sm font-medium">Çalışma Saatleri</h3>
          <p className="text-sm text-muted-foreground">
            Her satır ayrı görünürlük taşır; dil alanları yalnızca gün ve saat metnini yerelleştirir.
          </p>
        </div>
        {workingHours.length === 0 ? (
          <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
            Çalışma saati bulunamadı.
          </div>
        ) : (
          workingHours.map((workingHour, index) => (
            <div
              key={workingHour.id}
              className={cn(
                "grid gap-3 rounded-md border p-3 md:grid-cols-[1fr_1fr_auto]",
                !workingHour.isVisible && "border-dashed bg-muted/20",
              )}
            >
              <div className="flex flex-wrap items-center gap-2 md:col-span-3">
                <div className="text-sm font-medium">Saat satırı {index + 1}</div>
                <Badge variant={workingHour.isVisible ? "success" : "secondary"}>
                  {workingHour.isVisible ? "Public sitede görünür" : "Public sitede gizli"}
                </Badge>
              </div>
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
              <div className="grid gap-3 md:col-span-3 md:grid-cols-2 xl:grid-cols-4">
                {translationLocales.map((locale) => (
                  <div key={locale} className="space-y-2 rounded-md bg-muted/20 p-3">
                    <Label htmlFor={`contact-working-hour-${locale}-day-${index}`}>
                      Gün {index + 1} {locale.toUpperCase()}
                    </Label>
                    <Input
                      id={`contact-working-hour-${locale}-day-${index}`}
                      value={workingHour.translations?.[locale]?.day ?? ""}
                      onChange={(event) => updateWorkingHourTranslation(index, locale, { day: event.target.value })}
                    />
                    <Label htmlFor={`contact-working-hour-${locale}-hours-${index}`}>
                      Saat {index + 1} {locale.toUpperCase()}
                    </Label>
                    <Input
                      id={`contact-working-hour-${locale}-hours-${index}`}
                      value={workingHour.translations?.[locale]?.hours ?? ""}
                      onChange={(event) => updateWorkingHourTranslation(index, locale, { hours: event.target.value })}
                    />
                  </div>
                ))}
              </div>
            </div>
          ))
        )}
      </section>
    </div>
  );
}
