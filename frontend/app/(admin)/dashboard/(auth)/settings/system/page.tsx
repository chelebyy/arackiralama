"use client";

import { useEffect, useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Plus, Trash2 } from "lucide-react";
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
import { usePublicSiteSettings, mutateUpdatePublicSiteSettings } from "@/hooks/admin";
import { toast } from "sonner";

const internalHrefSchema = z
  .string()
  .min(1, "Bağlantı zorunludur")
  .regex(/^\/(?!\/)[a-zA-Z0-9/_?=&.#-]*$/, "Site içi bağlantı / ile başlamalıdır");

const siteLinkSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1, "Başlık zorunludur").max(80),
  href: internalHrefSchema,
  isVisible: z.boolean(),
  sortOrder: z.number(),
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
});

const contactWorkingHourSchema = z.object({
  id: z.string().min(1),
  day: z.string().min(1, "Gün zorunludur").max(80),
  hours: z.string().min(1, "Saat zorunludur").max(80),
  isVisible: z.boolean(),
  sortOrder: z.number(),
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

function createId(prefix: string) {
  return `${prefix}-${Date.now().toString(36)}`;
}

function reindexLinks<T extends { sortOrder: number }>(links: T[]) {
  return links.map((link, index) => ({ ...link, sortOrder: index }));
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
        headerLinks: settings.headerLinks ?? [],
        heroLinks: settings.heroLinks ?? [],
        quickLinks: settings.quickLinks ?? [],
        socialLinks: settings.socialLinks ?? [],
        footerBottomLinks: settings.footerBottomLinks ?? [],
        contactPageChannels: settings.contactPageChannels ?? [],
        contactPageOffices: settings.contactPageOffices ?? [],
        contactPageWorkingHours: settings.contactPageWorkingHours ?? [],
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
                    Hakkımızda, kullanım koşulları, gizlilik ve yeni public sayfa içeriklerini buradan yönetin.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => pages.append({
                    id: createId("page"),
                    slug: `new-page-${pages.fields.length + 1}`,
                    locale: "tr",
                    title: "Yeni Sayfa",
                    subtitle: "",
                    seoTitle: "",
                    seoDescription: "",
                    isPublished: false,
                    sortOrder: pages.fields.length,
                    blocks: [
                      {
                        id: createId("block"),
                        heading: "Yeni Bölüm",
                        body: "",
                        isVisible: true,
                        sortOrder: 0,
                      },
                    ],
                  })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Sayfa Ekle
                </Button>
              </div>
              <div className="space-y-4">
                {pages.fields.map((item, pageIndex) => {
                  const pageBlocks = watchedPages?.[pageIndex]?.blocks ?? [];
                  const slug = watchedPages?.[pageIndex]?.slug;
                  const isBuiltInPage = ["about", "terms", "privacy"].includes(slug);

                  return (
                    <div key={item.id} className="space-y-4 rounded-lg border p-4">
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
                            <FormControl><Input placeholder="tr" {...field} /></FormControl>
                            <FormMessage />
                          </FormItem>
                        )} />
                        <FormField control={form.control} name={`pages.${pageIndex}.slug`} render={({ field }) => (
                          <FormItem>
                            <FormLabel>Sayfa Yolu</FormLabel>
                            <FormControl><Input placeholder="about" {...field} /></FormControl>
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
                          aria-label={isBuiltInPage ? "Sayfayı yayından kaldır" : "Sayfayı sil"}
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
                          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">İçerik Blokları</h3>
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
                  onClick={() => headerLinks.append({ id: createId("header"), label: "", href: "/", isVisible: true, sortOrder: headerLinks.fields.length })}
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
                  onClick={() => heroLinks.append({ id: createId("hero"), label: "", href: "/", isVisible: true, sortOrder: heroLinks.fields.length })}
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
                  onClick={() => quickLinks.append({ id: createId("quick"), label: "", href: "/", isVisible: true, sortOrder: quickLinks.fields.length })}
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
                  onClick={() => footerBottomLinks.append({ id: createId("bottom"), label: "", href: "/", isVisible: true, sortOrder: footerBottomLinks.fields.length })}
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
