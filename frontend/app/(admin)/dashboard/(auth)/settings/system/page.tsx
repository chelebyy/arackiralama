"use client";

import { useEffect, useState } from "react";
import { type Path, type UseFormReturn, useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Copy, FileText, Languages, Plus, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { usePublicSiteSettings, mutateUpdatePublicSiteSettings } from "@/hooks/admin";
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
  day: z.string().max(80).nullable().optional(),
});

const translationsSchema = z.record(z.string(), localizedTextSchema).nullable().optional();

const siteLinkSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1, "Başlık zorunludur").max(80),
  href: internalHrefSchema,
  isVisible: z.boolean(),
  sortOrder: z.number(),
  translations: translationsSchema,
});

const socialLinkSchema = z.object({
  id: z.string().min(1),
  platform: z.string().min(1, "Platform zorunludur").max(40),
  url: z.string().url("Geçerli URL giriniz"),
  isVisible: z.boolean(),
  sortOrder: z.number(),
});

const contactHrefSchema = z
  .string()
  .min(1, "Bağlantı zorunludur")
  .refine(
    (value) => {
      if (
        value.startsWith("tel:") ||
        value.startsWith("mailto:") ||
        /^\/(?!\/)[a-zA-Z0-9/_?=&.#-]*$/.test(value)
      ) {
        return true;
      }

      try {
        const url = new URL(value);
        return url.protocol === "http:" || url.protocol === "https:";
      } catch {
        return false;
      }
    },
    "tel:, mailto:, site içi yol veya geçerli URL giriniz"
  );

const contactChannelSchema = z.object({
  id: z.string().min(1),
  type: z.string().min(1, "Tip zorunludur").max(30),
  label: z.string().min(1, "Başlık zorunludur").max(80),
  value: z.string().min(1, "Değer zorunludur").max(120),
  href: contactHrefSchema,
  description: z.string().max(180),
  isVisible: z.boolean(),
  sortOrder: z.number(),
  translations: translationsSchema,
});

const contactOfficeSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1, "Ofis adı zorunludur").max(120),
  address: z.string().min(1, "Adres zorunludur").max(240),
  phone: z.string().min(1, "Telefon zorunludur").max(80),
  hours: z.string().min(1, "Çalışma saati zorunludur").max(80),
  type: z.string().min(1, "Tip zorunludur").max(30),
  isVisible: z.boolean(),
  sortOrder: z.number(),
  translations: translationsSchema,
});

const contactWorkingHourSchema = z.object({
  id: z.string().min(1),
  day: z.string().min(1, "Gün zorunludur").max(80),
  hours: z.string().min(1, "Saat zorunludur").max(80),
  isVisible: z.boolean(),
  sortOrder: z.number(),
  translations: translationsSchema,
});

const pageBlockSchema = z.object({
  id: z.string().min(1),
  heading: z.string().min(1, "Bölüm başlığı zorunludur").max(160),
  body: z.string().max(5000),
  isVisible: z.boolean(),
  sortOrder: z.number(),
});

const managedPageSchema = z.object({
  id: z.string().min(1),
  slug: z
    .string()
    .min(1, "Slug zorunludur")
    .max(80)
    .regex(/^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$/, "Slug küçük harf, rakam ve tire içermelidir"),
  locale: z.string().min(2, "Dil zorunludur").max(12),
  title: z.string().min(1, "Sayfa başlığı zorunludur").max(160),
  subtitle: z.string().max(300),
  seoTitle: z.string().max(160),
  seoDescription: z.string().max(300),
  isPublished: z.boolean(),
  sortOrder: z.number(),
  blocks: z.array(pageBlockSchema).max(24),
});

const googleMapEmbedSchema = z
  .string()
  .min(1, "Harita embed URL zorunludur")
  .max(1200)
  .refine((value) => {
    try {
      const url = new URL(value);
      const hostname = url.hostname.toLowerCase();
      const isGoogleHost = hostname === "google.com" || hostname.endsWith(".google.com");
      return url.protocol === "https:" && isGoogleHost && url.pathname.startsWith("/maps/embed");
    } catch {
      return false;
    }
  }, "Google Maps embed URL giriniz");

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
  contactPageChannels: z.array(contactChannelSchema).max(8),
  contactPageOffices: z.array(contactOfficeSchema).max(8),
  contactPageWorkingHours: z.array(contactWorkingHourSchema).max(8),
  contactPageMapTitle: z.string().min(1, "Harita başlığı zorunludur").max(160),
  contactPageMapEmbedUrl: googleMapEmbedSchema,
  contactPageMapIsVisible: z.boolean(),
  pages: z.array(managedPageSchema).max(80),
});

type PublicSiteSettingsFormData = z.infer<typeof publicSiteSettingsSchema>;
type ManagedPageFormValue = PublicSiteSettingsFormData["pages"][number];
type ManagedPageEntry = { page: ManagedPageFormValue; index: number };
type FormTranslationMap = NonNullable<PublicSiteSettingsFormData["headerLinks"][number]["translations"]>;
type TranslationFieldKey = keyof z.infer<typeof localizedTextSchema>;
type TranslationBasePath =
  | `headerLinks.${number}`
  | `heroLinks.${number}`
  | `quickLinks.${number}`
  | `footerBottomLinks.${number}`
  | `contactPageChannels.${number}`
  | `contactPageOffices.${number}`
  | `contactPageWorkingHours.${number}`;
type TranslationFieldConfig = {
  key: TranslationFieldKey;
  label: string;
  placeholder?: string;
  control?: "input" | "textarea";
};

const managedPageLocales = [
  { code: "tr", label: "TR", name: "Türkçe" },
  { code: "en", label: "EN", name: "English" },
  { code: "ru", label: "RU", name: "Русский" },
  { code: "ar", label: "AR", name: "العربية" },
  { code: "de", label: "DE", name: "Deutsch" },
] as const;

type ManagedPageLocale = (typeof managedPageLocales)[number]["code"];

const builtInPageSlugs = new Set(["about", "terms", "privacy"]);

const linkTranslationFields = [
  { key: "label", label: "Başlık", placeholder: "Bu dilde bağlantı başlığı" },
] satisfies TranslationFieldConfig[];

const contactChannelTranslationFields = [
  { key: "label", label: "Başlık", placeholder: "Bu dilde kanal başlığı" },
  { key: "value", label: "Gösterilen Değer", placeholder: "Bu dilde görünen değer" },
  { key: "description", label: "Açıklama", placeholder: "Bu dilde kısa açıklama" },
] satisfies TranslationFieldConfig[];

const contactOfficeTranslationFields = [
  { key: "name", label: "Ofis Adı", placeholder: "Bu dilde ofis adı" },
  { key: "address", label: "Adres", placeholder: "Bu dilde adres", control: "textarea" },
  { key: "hours", label: "Saat", placeholder: "Bu dilde çalışma saati" },
] satisfies TranslationFieldConfig[];

const workingHourTranslationFields = [
  { key: "day", label: "Gün", placeholder: "Bu dilde gün aralığı" },
  { key: "hours", label: "Saat", placeholder: "Bu dilde saat aralığı" },
] satisfies TranslationFieldConfig[];

function isManagedPageLocale(value: string): value is ManagedPageLocale {
  return managedPageLocales.some((locale) => locale.code === value);
}

function LocalizedTranslationTabs({
  form,
  baseName,
  fields,
}: {
  form: UseFormReturn<PublicSiteSettingsFormData>;
  baseName: TranslationBasePath;
  fields: readonly TranslationFieldConfig[];
}) {
  return (
    <div className="col-span-full rounded-lg border bg-muted/20 p-3">
      <div className="mb-3 flex items-center gap-2 text-xs font-medium text-muted-foreground">
        <Languages className="h-4 w-4" />
        Dil bazlı içerik
      </div>
      <Tabs defaultValue="tr" className="space-y-3">
        <TabsList className="grid w-full grid-cols-5">
          {managedPageLocales.map((locale) => (
            <TabsTrigger key={locale.code} value={locale.code}>
              {locale.label}
            </TabsTrigger>
          ))}
        </TabsList>
        {managedPageLocales.map((locale) => (
          <TabsContent key={locale.code} value={locale.code} className="mt-0">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
              {fields.map((translationField) => (
                <FormField
                  key={translationField.key}
                  control={form.control}
                  name={`${baseName}.translations.${locale.code}.${translationField.key}` as Path<PublicSiteSettingsFormData>}
                  render={({ field }) => (
                    <FormItem className={translationField.control === "textarea" ? "md:col-span-3" : undefined}>
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
    translations: (item.translations ?? {}) as FormTranslationMap,
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
  pages: [],
};

let generatedIdCounter = 0;

function createId(prefix: string) {
  generatedIdCounter += 1;
  return `${prefix}-${Date.now().toString(36)}-${generatedIdCounter}`;
}

function reindexLinks<T extends { sortOrder: number }>(links: T[]) {
  return links.map((link, index) => ({ ...link, sortOrder: index }));
}

function createEmptyPageBlock(sortOrder = 0) {
  return {
    id: createId("block"),
    heading: "Yeni Bölüm",
    body: "",
    isVisible: true,
    sortOrder,
  };
}

function clonePageBlocks(blocks: ManagedPageFormValue["blocks"]) {
  if (!blocks.length) {
    return [createEmptyPageBlock()];
  }

  return blocks.map((block, index) => ({
    ...block,
    id: createId("block"),
    sortOrder: index,
  }));
}

function createManagedPageDraft(
  slug: string,
  locale: ManagedPageLocale,
  sortOrder: number,
  source?: ManagedPageFormValue
): ManagedPageFormValue {
  return {
    id: createId(`${locale}-${slug}`),
    slug,
    locale,
    title: source?.title ?? "Yeni Sayfa",
    subtitle: source?.subtitle ?? "",
    seoTitle: source?.seoTitle ?? "",
    seoDescription: source?.seoDescription ?? "",
    isPublished: false,
    sortOrder,
    blocks: source ? clonePageBlocks(source.blocks) : [createEmptyPageBlock()],
  };
}

function getNextCustomSlug(pages: ManagedPageFormValue[]) {
  const slugs = new Set(pages.map((page) => page.slug));
  let index = pages.length + 1;
  let slug = `new-page-${index}`;

  while (slugs.has(slug)) {
    index += 1;
    slug = `new-page-${index}`;
  }

  return slug;
}

function groupManagedPages(pages: ManagedPageFormValue[]) {
  const groups = new Map<
    string,
    {
      slug: string;
      firstEntry: ManagedPageEntry;
      pagesByLocale: Partial<Record<ManagedPageLocale, ManagedPageEntry>>;
      extraEntries: ManagedPageEntry[];
    }
  >();

  pages.forEach((page, index) => {
    const slug = page.slug || `page-${index + 1}`;
    const group =
      groups.get(slug) ??
      {
        slug,
        firstEntry: { page, index },
        pagesByLocale: {},
        extraEntries: [],
      };

    if (isManagedPageLocale(page.locale)) {
      group.pagesByLocale[page.locale] = { page, index };
    } else {
      group.extraEntries.push({ page, index });
    }

    groups.set(slug, group);
  });

  return Array.from(groups.values()).sort((left, right) => {
    const leftOrder = left.pagesByLocale.tr?.page.sortOrder ?? left.firstEntry.page.sortOrder;
    const rightOrder = right.pagesByLocale.tr?.page.sortOrder ?? right.firstEntry.page.sortOrder;
    return leftOrder - rightOrder || left.slug.localeCompare(right.slug);
  });
}

export default function SystemSettingsPage() {
  const [isSaving, setIsSaving] = useState(false);
  const { settings, isLoading, isError, mutate } = usePublicSiteSettings();

  const form = useForm<PublicSiteSettingsFormData>({
    resolver: zodResolver(publicSiteSettingsSchema),
    defaultValues: emptyDefaults,
  });

  const headerLinks = useFieldArray({ control: form.control, name: "headerLinks" });
  const heroLinks = useFieldArray({ control: form.control, name: "heroLinks" });
  const quickLinks = useFieldArray({ control: form.control, name: "quickLinks" });
  const socialLinks = useFieldArray({ control: form.control, name: "socialLinks" });
  const footerBottomLinks = useFieldArray({ control: form.control, name: "footerBottomLinks" });
  const contactPageChannels = useFieldArray({ control: form.control, name: "contactPageChannels" });
  const contactPageOffices = useFieldArray({ control: form.control, name: "contactPageOffices" });
  const contactPageWorkingHours = useFieldArray({ control: form.control, name: "contactPageWorkingHours" });
  const pages = useFieldArray({ control: form.control, name: "pages" });
  const watchedPages = form.watch("pages");

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
        contactPageChannels: withEmptyTranslations(settings.contactPageChannels ?? []),
        contactPageOffices: withEmptyTranslations(settings.contactPageOffices ?? []),
        contactPageWorkingHours: withEmptyTranslations(settings.contactPageWorkingHours ?? []),
        contactPageMapTitle: settings.contactPageMapTitle ?? "",
        contactPageMapEmbedUrl: settings.contactPageMapEmbedUrl ?? "",
        contactPageMapIsVisible: settings.contactPageMapIsVisible ?? true,
        pages: settings.pages ?? [],
      });
    }
  }, [form, settings]);

  const addPageBlock = (pageIndex: number) => {
    const blocks = form.getValues(`pages.${pageIndex}.blocks`) ?? [];
    form.setValue(`pages.${pageIndex}.blocks`, [
      ...blocks,
      {
        id: createId("block"),
        heading: "Yeni Bölüm",
        body: "",
        isVisible: true,
        sortOrder: blocks.length,
      },
    ], { shouldDirty: true });
  };

  const removePageBlock = (pageIndex: number, blockIndex: number) => {
    const blocks = form.getValues(`pages.${pageIndex}.blocks`) ?? [];
    form.setValue(`pages.${pageIndex}.blocks`, blocks.filter((_, index) => index !== blockIndex), { shouldDirty: true });
  };

  const unpublishPage = (pageIndex: number) => {
    form.setValue(`pages.${pageIndex}.isPublished`, false, { shouldDirty: true });
  };

  const addCustomPage = () => {
    const currentPages = form.getValues("pages") ?? [];
    const slug = getNextCustomSlug(currentPages);
    pages.append(createManagedPageDraft(slug, "tr", currentPages.length));
  };

  const addPageTranslation = (
    slug: string,
    locale: ManagedPageLocale,
    source?: ManagedPageFormValue
  ) => {
    const currentPages = form.getValues("pages") ?? [];
    const hasTranslation = currentPages.some(
      (page) => page.slug === slug && page.locale === locale
    );

    if (hasTranslation) {
      return;
    }

    pages.append(createManagedPageDraft(slug, locale, currentPages.length, source));
  };

  const pageGroups = groupManagedPages(watchedPages ?? []);

  const onSubmit = async (data: PublicSiteSettingsFormData) => {
    setIsSaving(true);
    try {
      await mutateUpdatePublicSiteSettings({
        ...data,
        headerLinks: reindexLinks(data.headerLinks),
        heroLinks: reindexLinks(data.heroLinks),
        quickLinks: reindexLinks(data.quickLinks),
        socialLinks: reindexLinks(data.socialLinks),
        footerBottomLinks: reindexLinks(data.footerBottomLinks),
        contactPageChannels: reindexLinks(data.contactPageChannels),
        contactPageOffices: reindexLinks(data.contactPageOffices),
        contactPageWorkingHours: reindexLinks(data.contactPageWorkingHours),
        pages: reindexLinks(data.pages).map((page) => ({
          ...page,
          locale: page.locale.toLowerCase(),
          slug: page.slug.toLowerCase(),
          blocks: reindexLinks(page.blocks),
        })),
      });
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
        <CardContent className="p-6 text-sm text-destructive">
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
                <FormField control={form.control} name="companyName" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Şirket Adı</FormLabel>
                    <FormControl><Input {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="companyEmail" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Email</FormLabel>
                    <FormControl><Input type="email" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="companyPhone" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Telefon</FormLabel>
                    <FormControl><Input {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="workingHours" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Çalışma Saatleri</FormLabel>
                    <FormControl><Input {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="companyAddress" render={({ field }) => (
                  <FormItem className="md:col-span-2">
                    <FormLabel>Adres</FormLabel>
                    <FormControl><Textarea {...field} className="resize-none" /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">İletişim Sayfası Kanalları</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => contactPageChannels.append({
                    id: createId("contact"),
                    type: "phone",
                    label: "",
                    value: "",
                    href: "tel:",
                    description: "",
                    isVisible: true,
                    sortOrder: contactPageChannels.fields.length,
                    translations: {},
                  })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {contactPageChannels.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[0.8fr_1fr_1fr_1.3fr_auto_auto]">
                    <FormField control={form.control} name={`contactPageChannels.${index}.type`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Tip</FormLabel>
                        <FormControl><Input placeholder="phone" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageChannels.${index}.label`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Başlık</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageChannels.${index}.value`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Gösterilen Değer</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageChannels.${index}.href`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Link</FormLabel>
                        <FormControl><Input placeholder="tel:+902425551000" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageChannels.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => contactPageChannels.remove(index)} aria-label="İletişim kanalını sil">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <FormField control={form.control} name={`contactPageChannels.${index}.description`} render={({ field }) => (
                      <FormItem className="md:col-span-6">
                        <FormLabel>Açıklama</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <LocalizedTranslationTabs
                      form={form}
                      baseName={`contactPageChannels.${index}` as TranslationBasePath}
                      fields={contactChannelTranslationFields}
                    />
                  </div>
                ))}
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">İletişim Sayfası Ofisleri</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => contactPageOffices.append({
                    id: createId("office"),
                    name: "",
                    address: "",
                    phone: "",
                    hours: "",
                    type: "branch",
                    isVisible: true,
                    sortOrder: contactPageOffices.fields.length,
                    translations: {},
                  })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {contactPageOffices.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1.4fr_1fr_1fr_0.8fr_auto_auto]">
                    <FormField control={form.control} name={`contactPageOffices.${index}.name`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Ofis Adı</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageOffices.${index}.address`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Adres</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageOffices.${index}.phone`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Telefon</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageOffices.${index}.hours`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Saat</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageOffices.${index}.type`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Tip</FormLabel>
                        <FormControl><Input placeholder="branch" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageOffices.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => contactPageOffices.remove(index)} aria-label="İletişim ofisini sil">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <LocalizedTranslationTabs
                      form={form}
                      baseName={`contactPageOffices.${index}` as TranslationBasePath}
                      fields={contactOfficeTranslationFields}
                    />
                  </div>
                ))}
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">İletişim Sayfası Çalışma Saatleri</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => contactPageWorkingHours.append({
                    id: createId("hours"),
                    day: "",
                    hours: "",
                    isVisible: true,
                    sortOrder: contactPageWorkingHours.fields.length,
                    translations: {},
                  })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {contactPageWorkingHours.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]">
                    <FormField control={form.control} name={`contactPageWorkingHours.${index}.day`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Gün</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageWorkingHours.${index}.hours`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Saat</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`contactPageWorkingHours.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => contactPageWorkingHours.remove(index)} aria-label="Çalışma saatini sil">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <LocalizedTranslationTabs
                      form={form}
                      baseName={`contactPageWorkingHours.${index}` as TranslationBasePath}
                      fields={workingHourTranslationFields}
                    />
                  </div>
                ))}
              </div>
            </section>

            <section className="space-y-4">
              <h2 className="text-sm font-semibold">İletişim Sayfası Haritası</h2>
              <div className="grid grid-cols-1 items-end gap-4 rounded-lg border p-4 md:grid-cols-[1fr_2fr_auto]">
                <FormField control={form.control} name="contactPageMapTitle" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Harita Başlığı</FormLabel>
                    <FormControl><Input placeholder="Office Locations Map" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="contactPageMapEmbedUrl" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Google Maps Embed URL</FormLabel>
                    <FormControl><Input placeholder="https://www.google.com/maps/embed?pb=..." {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )} />
                <FormField control={form.control} name="contactPageMapIsVisible" render={({ field }) => (
                  <FormItem>
                    <FormLabel>Görünsün</FormLabel>
                    <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                  </FormItem>
                )} />
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h2 className="text-sm font-semibold">Sayfalar</h2>
                  <p className="text-xs text-muted-foreground">
                    Hakkımızda, kullanım koşulları, gizlilik ve yeni public sayfa içeriklerini 5 dil sekmesiyle yönetin.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={addCustomPage}
                >
                  <Plus className="mr-2 h-4 w-4" /> Sayfa Ekle
                </Button>
              </div>
              <div className="space-y-4">
                {pageGroups.length === 0 && (
                  <div className="rounded-lg border border-dashed p-6 text-sm text-muted-foreground">
                    Henüz yönetilen public sayfa yok. İlk sayfayı eklemek için Sayfa Ekle butonunu kullanın.
                  </div>
                )}

                {pageGroups.map((group) => {
                  const baseEntry = group.pagesByLocale.tr ?? group.firstEntry;
                  const defaultLocale =
                    group.pagesByLocale.tr
                      ? "tr"
                      : managedPageLocales.find((locale) => group.pagesByLocale[locale.code])?.code ?? "tr";
                  const completedLocales = managedPageLocales.filter((locale) => group.pagesByLocale[locale.code]);
                  const publishedLocales = completedLocales.filter((locale) => group.pagesByLocale[locale.code]?.page.isPublished);

                  return (
                    <div key={group.slug} className="space-y-4 rounded-lg border bg-muted/20 p-4">
                      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                        <div className="flex items-start gap-3">
                          <div className="rounded-md border bg-background p-2 text-muted-foreground">
                            <FileText className="h-4 w-4" />
                          </div>
                          <div>
                            <h3 className="text-sm font-semibold">{baseEntry.page.title || group.slug}</h3>
                            <p className="text-xs text-muted-foreground">
                              /{group.slug} · {completedLocales.length}/{managedPageLocales.length} dil hazır · {publishedLocales.length} dil yayında
                            </p>
                          </div>
                        </div>
                        <div className="flex flex-wrap items-center gap-2 text-xs">
                          {builtInPageSlugs.has(group.slug) && (
                            <span className="rounded-full border bg-background px-2 py-1 text-muted-foreground">
                              Sabit sayfa
                            </span>
                          )}
                          <span className="inline-flex items-center gap-1 rounded-full border bg-background px-2 py-1 text-muted-foreground">
                            <Languages className="h-3.5 w-3.5" />
                            {managedPageLocales.length} dil
                          </span>
                        </div>
                      </div>

                      <Tabs defaultValue={defaultLocale} className="space-y-4">
                        <TabsList className="grid h-auto w-full grid-cols-2 md:grid-cols-5">
                          {managedPageLocales.map((locale) => {
                            const entry = group.pagesByLocale[locale.code];

                            return (
                              <TabsTrigger key={locale.code} value={locale.code} className="h-auto flex-col gap-0.5 py-2 text-xs">
                                <span className="font-semibold">{locale.label}</span>
                                <span className="text-[11px] font-normal text-muted-foreground">
                                  {entry ? (entry.page.isPublished ? "Yayında" : "Taslak") : "Eksik"}
                                </span>
                              </TabsTrigger>
                            );
                          })}
                        </TabsList>

                        {managedPageLocales.map((locale) => {
                          const entry = group.pagesByLocale[locale.code];

                          if (!entry) {
                            const sourceEntry = group.pagesByLocale.tr ?? group.firstEntry;
                            const sourceLocaleLabel = sourceEntry.page.locale.toUpperCase();

                            return (
                              <TabsContent key={locale.code} value={locale.code} className="rounded-md border border-dashed bg-background p-4">
                                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                                  <div>
                                    <h4 className="text-sm font-medium">{locale.name} çevirisi eksik</h4>
                                    <p className="mt-1 text-xs text-muted-foreground">
                                      Bu dil için ayrı içerik oluşturulmazsa public sayfa statik çeviri içeriğine düşer.
                                    </p>
                                  </div>
                                  <div className="flex flex-wrap gap-2">
                                    <Button
                                      type="button"
                                      variant="outline"
                                      size="sm"
                                      onClick={() => addPageTranslation(group.slug, locale.code, sourceEntry.page)}
                                    >
                                      <Copy className="mr-2 h-4 w-4" /> {sourceLocaleLabel}'den Kopyala
                                    </Button>
                                    <Button
                                      type="button"
                                      variant="secondary"
                                      size="sm"
                                      onClick={() => addPageTranslation(group.slug, locale.code)}
                                    >
                                      Boş Çeviri Oluştur
                                    </Button>
                                  </div>
                                </div>
                              </TabsContent>
                            );
                          }

                          const pageIndex = entry.index;
                          const pageBlocks = entry.page.blocks ?? [];
                          const isBuiltInPage = builtInPageSlugs.has(group.slug);

                          return (
                            <TabsContent key={locale.code} value={locale.code} className="space-y-4 rounded-md border bg-background p-4">
                              <div className="grid grid-cols-1 items-end gap-3 md:grid-cols-[1fr_0.8fr_1.2fr_auto_auto]">
                                <FormField control={form.control} name={`pages.${pageIndex}.title`} render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>Sayfa Başlığı</FormLabel>
                                    <FormControl><Input {...field} /></FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )} />
                                <FormField control={form.control} name={`pages.${pageIndex}.locale`} render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>Dil</FormLabel>
                                    <FormControl><Input readOnly {...field} /></FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )} />
                                <FormField control={form.control} name={`pages.${pageIndex}.slug`} render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>Sayfa Yolu</FormLabel>
                                    <FormControl><Input readOnly={isBuiltInPage} placeholder="about" {...field} /></FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )} />
                                <FormField control={form.control} name={`pages.${pageIndex}.isPublished`} render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>Yayında</FormLabel>
                                    <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                                  </FormItem>
                                )} />
                                <Button
                                  type="button"
                                  variant="ghost"
                                  size="icon"
                                  onClick={() => (isBuiltInPage ? unpublishPage(pageIndex) : pages.remove(pageIndex))}
                                  aria-label={isBuiltInPage ? "Sayfayı yayından kaldır" : "Çeviriyi sil"}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              </div>

                              <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                                <FormField control={form.control} name={`pages.${pageIndex}.subtitle`} render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>Alt Başlık</FormLabel>
                                    <FormControl><Textarea {...field} className="resize-none" /></FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )} />
                                <FormField control={form.control} name={`pages.${pageIndex}.seoDescription`} render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>SEO Açıklaması</FormLabel>
                                    <FormControl><Textarea {...field} className="resize-none" /></FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )} />
                                <FormField control={form.control} name={`pages.${pageIndex}.seoTitle`} render={({ field }) => (
                                  <FormItem className="md:col-span-2">
                                    <FormLabel>SEO Başlığı</FormLabel>
                                    <FormControl><Input {...field} /></FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )} />
                              </div>

                              <div className="space-y-3">
                                <div className="flex items-center justify-between gap-3">
                                  <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">İçerik Blokları</h4>
                                  <Button type="button" variant="outline" size="sm" onClick={() => addPageBlock(pageIndex)}>
                                    <Plus className="mr-2 h-4 w-4" /> Blok Ekle
                                  </Button>
                                </div>
                                {pageBlocks.map((block, blockIndex) => (
                                  <div key={block.id} className="grid grid-cols-1 items-end gap-3 rounded-md border p-3 md:grid-cols-[1fr_auto_auto]">
                                    <FormField control={form.control} name={`pages.${pageIndex}.blocks.${blockIndex}.heading`} render={({ field }) => (
                                      <FormItem>
                                        <FormLabel>Bölüm Başlığı</FormLabel>
                                        <FormControl><Input {...field} /></FormControl>
                                        <FormMessage />
                                      </FormItem>
                                    )} />
                                    <FormField control={form.control} name={`pages.${pageIndex}.blocks.${blockIndex}.isVisible`} render={({ field }) => (
                                      <FormItem>
                                        <FormLabel>Görünsün</FormLabel>
                                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                                      </FormItem>
                                    )} />
                                    <Button type="button" variant="ghost" size="icon" onClick={() => removePageBlock(pageIndex, blockIndex)} aria-label="İçerik bloğunu sil">
                                      <Trash2 className="h-4 w-4" />
                                    </Button>
                                    <FormField control={form.control} name={`pages.${pageIndex}.blocks.${blockIndex}.body`} render={({ field }) => (
                                      <FormItem className="md:col-span-3">
                                        <FormLabel>İçerik</FormLabel>
                                        <FormControl><Textarea {...field} rows={5} /></FormControl>
                                        <FormMessage />
                                      </FormItem>
                                    )} />
                                  </div>
                                ))}
                              </div>
                            </TabsContent>
                          );
                        })}

                        {group.extraEntries.length > 0 && (
                          <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
                            Destek dışı dil kodu olan {group.extraEntries.length} kayıt var. Bu kayıtlar kayıtta korunur ama 5 dil editöründe gösterilmez.
                          </div>
                        )}
                      </Tabs>
                    </div>
                  );
                })}
              </div>
            </section>

            <section className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold">Header Navigasyonu</h2>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => headerLinks.append({ id: createId("header"), label: "", href: "/", isVisible: true, sortOrder: headerLinks.fields.length, translations: {} })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {headerLinks.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]">
                    <FormField control={form.control} name={`headerLinks.${index}.label`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Başlık</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`headerLinks.${index}.href`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Sayfa Yolu</FormLabel>
                        <FormControl><Input placeholder="/vehicles" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`headerLinks.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => headerLinks.remove(index)} aria-label="Header bağlantısını sil">
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
                  onClick={() => heroLinks.append({ id: createId("hero"), label: "", href: "/", isVisible: true, sortOrder: heroLinks.fields.length, translations: {} })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {heroLinks.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]">
                    <FormField control={form.control} name={`heroLinks.${index}.label`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Başlık</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`heroLinks.${index}.href`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Sayfa Yolu</FormLabel>
                        <FormControl><Input placeholder="/booking" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`heroLinks.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => heroLinks.remove(index)} aria-label="Ana sayfa CTA bağlantısını sil">
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
                  onClick={() => quickLinks.append({ id: createId("quick"), label: "", href: "/", isVisible: true, sortOrder: quickLinks.fields.length, translations: {} })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {quickLinks.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]">
                    <FormField control={form.control} name={`quickLinks.${index}.label`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Başlık</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`quickLinks.${index}.href`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Sayfa Yolu</FormLabel>
                        <FormControl><Input placeholder="/vehicles" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`quickLinks.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => quickLinks.remove(index)} aria-label="Hızlı bağlantıyı sil">
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
                  onClick={() => socialLinks.append({ id: createId("social"), platform: "Instagram", url: "https://instagram.com", isVisible: true, sortOrder: socialLinks.fields.length })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {socialLinks.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_2fr_auto_auto]">
                    <FormField control={form.control} name={`socialLinks.${index}.platform`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Platform</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`socialLinks.${index}.url`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>URL</FormLabel>
                        <FormControl><Input placeholder="https://instagram.com/..." {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`socialLinks.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => socialLinks.remove(index)} aria-label="Sosyal medya bağlantısını sil">
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
                  onClick={() => footerBottomLinks.append({ id: createId("bottom"), label: "", href: "/", isVisible: true, sortOrder: footerBottomLinks.fields.length, translations: {} })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Ekle
                </Button>
              </div>
              <div className="space-y-3">
                {footerBottomLinks.fields.map((item, index) => (
                  <div key={item.id} className="grid grid-cols-1 items-end gap-3 rounded-lg border p-4 md:grid-cols-[1fr_1fr_auto_auto]">
                    <FormField control={form.control} name={`footerBottomLinks.${index}.label`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Başlık</FormLabel>
                        <FormControl><Input {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`footerBottomLinks.${index}.href`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Sayfa Yolu</FormLabel>
                        <FormControl><Input placeholder="/contact" {...field} /></FormControl>
                        <FormMessage />
                      </FormItem>
                    )} />
                    <FormField control={form.control} name={`footerBottomLinks.${index}.isVisible`} render={({ field }) => (
                      <FormItem>
                        <FormLabel>Görünsün</FormLabel>
                        <FormControl><Switch checked={field.value} onCheckedChange={field.onChange} /></FormControl>
                      </FormItem>
                    )} />
                    <Button type="button" variant="ghost" size="icon" onClick={() => footerBottomLinks.remove(index)} aria-label="Alt bağlantıyı sil">
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
