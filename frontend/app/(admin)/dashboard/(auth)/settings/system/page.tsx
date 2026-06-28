"use client";

import { useEffect, useState } from "react";
import { type Path, type UseFormReturn, useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Languages, Plus, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { usePublicSiteSettings, mutateUpdatePublicSiteSettings } from "@/hooks/admin";
import type {
  PublicContactChannel,
  PublicContactOffice,
  PublicContactWorkingHour,
  PublicManagedPage,
  UpdatePublicSiteSettingsData
} from "@/lib/api/admin/types";
import { toast } from "sonner";

const internalHrefSchema = z
  .string()
  .min(1, "Bağlantı zorunludur")
  .regex(/^\/(?!\/)[a-zA-Z0-9/_?=&.#-]*$/, "Site içi bağlantı / ile başlamalıdır");

const localizedTextSchema = z.object({
  label: z.string().max(80).nullable().optional(),
  value: z.string().max(120).nullable().optional(),
  description: z.string().max(180).nullable().optional(),
  name: z.string().max(120).nullable().optional(),
  address: z.string().max(240).nullable().optional(),
  hours: z.string().max(80).nullable().optional(),
  day: z.string().max(80).nullable().optional()
});

const translationsSchema = z.record(z.string(), localizedTextSchema).nullable().optional();

const siteLinkSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1, "Başlık zorunludur").max(80),
  href: internalHrefSchema,
  isVisible: z.boolean(),
  sortOrder: z.number(),
  translations: translationsSchema
});

const socialLinkSchema = z.object({
  id: z.string().min(1),
  platform: z.string().min(1, "Platform zorunludur").max(40),
  url: z.string().url("Geçerli URL giriniz"),
  isVisible: z.boolean(),
  sortOrder: z.number()
});

const publicSiteSettingsSchema = z.object({
  companyName: z.string().min(1, "Şirket adı gereklidir").max(160),
  companyAddress: z.string().min(1, "Adres gereklidir").max(500),
  companyPhone: z.string().min(1, "Telefon gereklidir").max(80),
  companyEmail: z.string().email("Geçerli bir email adresi giriniz").max(160),
  workingHours: z.string().min(1, "Çalışma saati gereklidir").max(160),
  headerLinks: z.array(siteLinkSchema).max(8),
  heroLinks: z.array(siteLinkSchema).max(4),
  quickLinks: z.array(siteLinkSchema).max(12),
  socialLinks: z.array(socialLinkSchema).max(8),
  footerBottomLinks: z.array(siteLinkSchema).max(6),
  contactPageChannels: z.custom<PublicContactChannel[]>((value) => Array.isArray(value)),
  contactPageOffices: z.custom<PublicContactOffice[]>((value) => Array.isArray(value)),
  contactPageWorkingHours: z.custom<PublicContactWorkingHour[]>((value) => Array.isArray(value)),
  contactPageMapTitle: z.string(),
  contactPageMapEmbedUrl: z.string(),
  contactPageMapIsVisible: z.boolean(),
  pages: z.custom<PublicManagedPage[]>((value) => Array.isArray(value))
});

type PublicSiteSettingsFormData = z.infer<typeof publicSiteSettingsSchema>;
type FormTranslationMap = NonNullable<
  PublicSiteSettingsFormData["headerLinks"][number]["translations"]
>;
type TranslationFieldKey = keyof z.infer<typeof localizedTextSchema>;
type TranslationBasePath =
  | `headerLinks.${number}`
  | `heroLinks.${number}`
  | `quickLinks.${number}`
  | `footerBottomLinks.${number}`;
type TranslationFieldConfig = {
  key: TranslationFieldKey;
  label: string;
  placeholder?: string;
  control?: "input" | "textarea";
};

const publicSettingsLocales = [
  { code: "tr", label: "TR", name: "Türkçe" },
  { code: "en", label: "EN", name: "English" },
  { code: "ru", label: "RU", name: "Русский" },
  { code: "ar", label: "AR", name: "العربية" },
  { code: "de", label: "DE", name: "Deutsch" }
] as const;

const linkTranslationFields = [
  { key: "label", label: "Başlık", placeholder: "Bu dilde bağlantı başlığı" }
] satisfies TranslationFieldConfig[];

function LocalizedTranslationTabs({
  form,
  baseName,
  fields
}: {
  form: UseFormReturn<PublicSiteSettingsFormData>;
  baseName: TranslationBasePath;
  fields: readonly TranslationFieldConfig[];
}) {
  return (
    <div className="bg-muted/20 col-span-full rounded-lg border p-3">
      <div className="text-muted-foreground mb-3 flex items-center gap-2 text-xs font-medium">
        <Languages className="h-4 w-4" />
        Dil bazlı içerik
      </div>
      <Tabs defaultValue="tr" className="space-y-3">
        <TabsList className="grid w-full grid-cols-5">
          {publicSettingsLocales.map((locale) => (
            <TabsTrigger key={locale.code} value={locale.code}>
              {locale.label}
            </TabsTrigger>
          ))}
        </TabsList>
        {publicSettingsLocales.map((locale) => (
          <TabsContent key={locale.code} value={locale.code} className="mt-0">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
              {fields.map((translationField) => (
                <FormField
                  key={translationField.key}
                  control={form.control}
                  name={
                    `${baseName}.translations.${locale.code}.${translationField.key}` as Path<PublicSiteSettingsFormData>
                  }
                  render={({ field }) => (
                    <FormItem
                      className={
                        translationField.control === "textarea" ? "md:col-span-3" : undefined
                      }
                    >
                      <FormLabel>{translationField.label}</FormLabel>
                      <FormControl>
                        {translationField.control === "textarea" ? (
                          <Textarea
                            {...field}
                            value={(field.value as string | undefined) ?? ""}
                            placeholder={translationField.placeholder}
                            className="resize-none"
                            rows={2}
                          />
                        ) : (
                          <Input
                            {...field}
                            value={(field.value as string | undefined) ?? ""}
                            placeholder={translationField.placeholder}
                          />
                        )}
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ))}
            </div>
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}

function withEmptyTranslations<T extends { translations?: unknown }>(
  items: T[] = []
): Array<Omit<T, "translations"> & { translations: FormTranslationMap }> {
  return items.map((item) => ({
    ...item,
    translations: (item.translations ?? {}) as FormTranslationMap
  }));
}

const emptyDefaults: PublicSiteSettingsFormData = {
  companyName: "",
  companyAddress: "",
  companyPhone: "",
  companyEmail: "",
  workingHours: "",
  headerLinks: [],
  heroLinks: [],
  quickLinks: [],
  socialLinks: [],
  footerBottomLinks: [],
  contactPageChannels: [],
  contactPageOffices: [],
  contactPageWorkingHours: [],
  contactPageMapTitle: "",
  contactPageMapEmbedUrl: "",
  contactPageMapIsVisible: true,
  pages: []
};

let generatedIdCounter = 0;

function createId(prefix: string) {
  generatedIdCounter += 1;
  return `${prefix}-${Date.now().toString(36)}-${generatedIdCounter}`;
}

function reindexLinks<T extends { sortOrder: number }>(links: T[]) {
  return links.map((link, index) => ({ ...link, sortOrder: index }));
}

export default function SystemSettingsPage() {
  const [isSaving, setIsSaving] = useState(false);
  const { settings, isLoading, isError, mutate } = usePublicSiteSettings();

  const form = useForm<PublicSiteSettingsFormData>({
    resolver: zodResolver(publicSiteSettingsSchema),
    defaultValues: emptyDefaults
  });

  const headerLinks = useFieldArray({ control: form.control, name: "headerLinks" });
  const heroLinks = useFieldArray({ control: form.control, name: "heroLinks" });
  const quickLinks = useFieldArray({ control: form.control, name: "quickLinks" });
  const socialLinks = useFieldArray({ control: form.control, name: "socialLinks" });
  const footerBottomLinks = useFieldArray({ control: form.control, name: "footerBottomLinks" });

  useEffect(() => {
    if (settings) {
      form.reset({
        companyName: settings.companyName,
        companyAddress: settings.companyAddress,
        companyPhone: settings.companyPhone,
        companyEmail: settings.companyEmail,
        workingHours: settings.workingHours,
        headerLinks: withEmptyTranslations(settings.headerLinks ?? []),
        heroLinks: withEmptyTranslations(settings.heroLinks ?? []),
        quickLinks: withEmptyTranslations(settings.quickLinks ?? []),
        socialLinks: settings.socialLinks ?? [],
        footerBottomLinks: withEmptyTranslations(settings.footerBottomLinks ?? []),
        contactPageChannels: settings.contactPageChannels ?? [],
        contactPageOffices: settings.contactPageOffices ?? [],
        contactPageWorkingHours: settings.contactPageWorkingHours ?? [],
        contactPageMapTitle: settings.contactPageMapTitle ?? "",
        contactPageMapEmbedUrl: settings.contactPageMapEmbedUrl ?? "",
        contactPageMapIsVisible: settings.contactPageMapIsVisible ?? true,
        pages: settings.pages ?? []
      });
    }
  }, [form, settings]);

  const onSubmit = async (data: PublicSiteSettingsFormData) => {
    setIsSaving(true);
    try {
      const latestSettings = await mutate();
      const contentSource = latestSettings ?? settings;

      const payload: UpdatePublicSiteSettingsData = {
        ...data,
        headerLinks: reindexLinks(data.headerLinks),
        heroLinks: reindexLinks(data.heroLinks),
        quickLinks: reindexLinks(data.quickLinks),
        socialLinks: reindexLinks(data.socialLinks),
        footerBottomLinks: reindexLinks(data.footerBottomLinks),
        contactPageChannels: contentSource?.contactPageChannels ?? [],
        contactPageOffices: contentSource?.contactPageOffices ?? [],
        contactPageWorkingHours: contentSource?.contactPageWorkingHours ?? [],
        contactPageMapTitle: contentSource?.contactPageMapTitle ?? "",
        contactPageMapEmbedUrl: contentSource?.contactPageMapEmbedUrl ?? "",
        contactPageMapIsVisible: contentSource?.contactPageMapIsVisible ?? true,
        pages: contentSource?.pages ?? []
      };

      await mutateUpdatePublicSiteSettings(payload);
      await mutate();
      toast.success("Public site ayarları kaydedildi");
    } catch {
      toast.error("Ayarlar kaydedilemedi");
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Public Site Ayarları</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {Array.from({ length: 6 }).map((_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </CardContent>
      </Card>
    );
  }

  if (isError) {
    return (
      <Card>
        <CardContent className="text-destructive p-6 text-sm">
          Public site ayarları yüklenirken hata oluştu.
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Public Site Ayarları</CardTitle>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8">
            <section className="space-y-4">
              <h2 className="text-sm font-semibold">İletişim Bilgileri</h2>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <FormField
                  control={form.control}
                  name="companyName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Şirket Adı</FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="companyEmail"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Email</FormLabel>
                      <FormControl>
                        <Input type="email" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="companyPhone"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Telefon</FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="workingHours"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Çalışma Saatleri</FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="companyAddress"
                  render={({ field }) => (
                    <FormItem className="md:col-span-2">
                      <FormLabel>Adres</FormLabel>
                      <FormControl>
                        <Textarea {...field} className="resize-none" />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">Header Navigasyonu</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    headerLinks.append({
                      id: createId("header"),
                      label: "",
                      href: "/",
                      isVisible: true,
                      sortOrder: headerLinks.fields.length,
                      translations: {}
                    })
                  }
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {headerLinks.fields.map((item, index) => (
                  <div
                    key={item.id}
                    className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]"
                  >
                    <FormField
                      control={form.control}
                      name={`headerLinks.${index}.label`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Başlık</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`headerLinks.${index}.href`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Sayfa Yolu</FormLabel>
                          <FormControl>
                            <Input placeholder="/vehicles" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`headerLinks.${index}.isVisible`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Görünsün</FormLabel>
                          <FormControl>
                            <Switch checked={field.value} onCheckedChange={field.onChange} />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => headerLinks.remove(index)}
                      aria-label="Header bağlantısını sil"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <LocalizedTranslationTabs
                      form={form}
                      baseName={`headerLinks.${index}` as TranslationBasePath}
                      fields={linkTranslationFields}
                    />
                  </div>
                ))}
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">Ana Sayfa CTA</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    heroLinks.append({
                      id: createId("hero"),
                      label: "",
                      href: "/",
                      isVisible: true,
                      sortOrder: heroLinks.fields.length,
                      translations: {}
                    })
                  }
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {heroLinks.fields.map((item, index) => (
                  <div
                    key={item.id}
                    className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]"
                  >
                    <FormField
                      control={form.control}
                      name={`heroLinks.${index}.label`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Başlık</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`heroLinks.${index}.href`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Sayfa Yolu</FormLabel>
                          <FormControl>
                            <Input placeholder="/booking" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`heroLinks.${index}.isVisible`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Görünsün</FormLabel>
                          <FormControl>
                            <Switch checked={field.value} onCheckedChange={field.onChange} />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => heroLinks.remove(index)}
                      aria-label="Ana sayfa CTA bağlantısını sil"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <LocalizedTranslationTabs
                      form={form}
                      baseName={`heroLinks.${index}` as TranslationBasePath}
                      fields={linkTranslationFields}
                    />
                  </div>
                ))}
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">Hızlı Bağlantılar</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    quickLinks.append({
                      id: createId("quick"),
                      label: "",
                      href: "/",
                      isVisible: true,
                      sortOrder: quickLinks.fields.length,
                      translations: {}
                    })
                  }
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {quickLinks.fields.map((item, index) => (
                  <div
                    key={item.id}
                    className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]"
                  >
                    <FormField
                      control={form.control}
                      name={`quickLinks.${index}.label`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Başlık</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`quickLinks.${index}.href`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Sayfa Yolu</FormLabel>
                          <FormControl>
                            <Input placeholder="/vehicles" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`quickLinks.${index}.isVisible`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Görünsün</FormLabel>
                          <FormControl>
                            <Switch checked={field.value} onCheckedChange={field.onChange} />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => quickLinks.remove(index)}
                      aria-label="Hızlı bağlantıyı sil"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <LocalizedTranslationTabs
                      form={form}
                      baseName={`quickLinks.${index}` as TranslationBasePath}
                      fields={linkTranslationFields}
                    />
                  </div>
                ))}
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">Sosyal Medya</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    socialLinks.append({
                      id: createId("social"),
                      platform: "Instagram",
                      url: "https://instagram.com",
                      isVisible: true,
                      sortOrder: socialLinks.fields.length
                    })
                  }
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {socialLinks.fields.map((item, index) => (
                  <div
                    key={item.id}
                    className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_2fr_auto_auto]"
                  >
                    <FormField
                      control={form.control}
                      name={`socialLinks.${index}.platform`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Platform</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`socialLinks.${index}.url`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>URL</FormLabel>
                          <FormControl>
                            <Input placeholder="https://instagram.com/..." {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`socialLinks.${index}.isVisible`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Görünsün</FormLabel>
                          <FormControl>
                            <Switch checked={field.value} onCheckedChange={field.onChange} />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => socialLinks.remove(index)}
                      aria-label="Sosyal medya bağlantısını sil"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                ))}
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">Footer Alt Bağlantıları</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    footerBottomLinks.append({
                      id: createId("bottom"),
                      label: "",
                      href: "/",
                      isVisible: true,
                      sortOrder: footerBottomLinks.fields.length,
                      translations: {}
                    })
                  }
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {footerBottomLinks.fields.map((item, index) => (
                  <div
                    key={item.id}
                    className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]"
                  >
                    <FormField
                      control={form.control}
                      name={`footerBottomLinks.${index}.label`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Başlık</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`footerBottomLinks.${index}.href`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Sayfa Yolu</FormLabel>
                          <FormControl>
                            <Input placeholder="/contact" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name={`footerBottomLinks.${index}.isVisible`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Görünsün</FormLabel>
                          <FormControl>
                            <Switch checked={field.value} onCheckedChange={field.onChange} />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => footerBottomLinks.remove(index)}
                      aria-label="Alt bağlantıyı sil"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <LocalizedTranslationTabs
                      form={form}
                      baseName={`footerBottomLinks.${index}` as TranslationBasePath}
                      fields={linkTranslationFields}
                    />
                  </div>
                ))}
              </div>
            </section>

            <div className="flex justify-end">
              <Button type="submit" disabled={isSaving}>
                {isSaving ? "Kaydediliyor..." : "Kaydet"}
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
