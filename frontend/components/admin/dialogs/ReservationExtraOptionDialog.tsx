"use client";

import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { AlertCircle, CheckCircle2 } from "lucide-react";
import { toast } from "sonner";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@/components/ui/dialog";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import {
  mutateCreateReservationExtraOption,
  mutateUpdateReservationExtraOption,
  mutateUpdateReservationExtraOptionStatus
} from "@/hooks/admin";
import { ApiError } from "@/lib/api/client";
import type { AdminVehicleGroup } from "@/lib/api/admin";
import {
  RESERVATION_EXTRA_ICON_KEYS,
  RESERVATION_EXTRA_LOCALES
} from "@/lib/api/admin/reservationExtras";
import type {
  AdminReservationExtraOption,
  CreateReservationExtraOptionData,
  ReservationExtraIconKey,
  ReservationExtraLocale
} from "@/lib/api/admin/reservationExtras";

const localeLabels: Record<ReservationExtraLocale, string> = {
  tr: "Türkçe",
  en: "İngilizce",
  de: "Almanca",
  ru: "Rusça",
  ar: "Arapça"
};

const iconLabels: Record<ReservationExtraIconKey, string> = {
  baby: "Çocuk koltuğu",
  users: "Ek sürücü",
  navigation: "Navigasyon",
  wifi: "Wi-Fi"
};

const translationSchema = z.object({
  name: z.string().max(100, "Ad en fazla 100 karakter olabilir"),
  description: z.string().max(300, "Açıklama en fazla 300 karakter olabilir")
});

const editorSchema = z.object({
  unitPrice: z.coerce.number().min(0, "Fiyat 0 veya daha büyük olmalıdır").max(1_000_000),
  pricingMode: z.enum(["PER_DAY", "PER_RENTAL"]),
  maxQuantity: z.coerce.number().int().min(1).max(20),
  iconKey: z.enum(RESERVATION_EXTRA_ICON_KEYS),
  sortOrder: z.coerce.number().int().min(0).max(9_999),
  vehicleGroupIds: z.array(z.string()),
  translations: z.object({
    tr: translationSchema,
    en: translationSchema,
    de: translationSchema,
    ru: translationSchema,
    ar: translationSchema
  })
});

type EditorInput = z.input<typeof editorSchema>;
type EditorData = z.output<typeof editorSchema>;

interface ReservationExtraOptionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  option?: AdminReservationExtraOption;
  vehicleGroups: AdminVehicleGroup[];
  onSaved: (item: AdminReservationExtraOption) => void;
  onReload: () => void | Promise<void>;
}

function emptyTranslations(): EditorInput["translations"] {
  return {
    tr: { name: "", description: "" },
    en: { name: "", description: "" },
    de: { name: "", description: "" },
    ru: { name: "", description: "" },
    ar: { name: "", description: "" }
  };
}

function buildDefaultValues(option?: AdminReservationExtraOption): EditorInput {
  const translations = emptyTranslations();
  option?.translations.forEach((translation) => {
    if (RESERVATION_EXTRA_LOCALES.includes(translation.locale)) {
      translations[translation.locale] = {
        name: translation.name,
        description: translation.description
      };
    }
  });

  return {
    unitPrice: option?.unitPrice ?? 0,
    pricingMode: option?.pricingMode ?? "PER_RENTAL",
    maxQuantity: option?.maxQuantity ?? 1,
    iconKey: option?.iconKey ?? "baby",
    sortOrder: option?.sortOrder ?? 0,
    vehicleGroupIds: option?.vehicleGroupIds ?? [],
    translations
  };
}

export function getReservationExtraReadiness(values: EditorInput) {
  const missingLocales = RESERVATION_EXTRA_LOCALES.filter((locale) => {
    const translation = values.translations[locale];
    return !translation.name.trim() || !translation.description.trim();
  });
  return {
    missingLocales,
    hasVehicleGroup: values.vehicleGroupIds.length > 0,
    isReady: missingLocales.length === 0 && values.vehicleGroupIds.length > 0
  };
}

function groupLabel(group: AdminVehicleGroup) {
  return group.nameTr ?? group.nameEn ?? group.name ?? group.id;
}

function buildPayload(data: EditorData): CreateReservationExtraOptionData {
  return {
    unitPrice: data.unitPrice,
    pricingMode: data.pricingMode,
    maxQuantity: data.maxQuantity,
    iconKey: data.iconKey,
    sortOrder: data.sortOrder,
    vehicleGroupIds: data.vehicleGroupIds,
    translations: RESERVATION_EXTRA_LOCALES.map((locale) => ({
      locale,
      name: data.translations[locale].name.trim(),
      description: data.translations[locale].description.trim()
    }))
  };
}

export default function ReservationExtraOptionDialog({
  open,
  onOpenChange,
  option,
  vehicleGroups,
  onSaved,
  onReload
}: ReservationExtraOptionDialogProps) {
  const [serverError, setServerError] = useState<string | null>(null);
  const [hasConflict, setHasConflict] = useState(false);
  const form = useForm<EditorInput, unknown, EditorData>({
    resolver: zodResolver(editorSchema),
    defaultValues: buildDefaultValues(option)
  });

  useEffect(() => {
    form.reset(buildDefaultValues(option));
    setServerError(null);
    setHasConflict(false);
  }, [form, open, option]);

  const translations = form.watch("translations");
  const vehicleGroupIds = form.watch("vehicleGroupIds");
  const readiness = getReservationExtraReadiness({
    ...form.getValues(),
    translations,
    vehicleGroupIds
  });

  const save = async (data: EditorData, activate: boolean) => {
    setServerError(null);
    setHasConflict(false);
    if (activate && !getReservationExtraReadiness(data).isReady) {
      setServerError(
        "Aktivasyon için beş dilin adı ve açıklaması ile en az bir araç grubu gereklidir."
      );
      return;
    }

    try {
      const payload = buildPayload(data);
      const saved = option
        ? await mutateUpdateReservationExtraOption(option.id, {
            ...payload,
            version: option.version
          })
        : await mutateCreateReservationExtraOption(payload);

      if (activate && !saved.isActive) {
        try {
          const activated = await mutateUpdateReservationExtraOptionStatus(saved.id, {
            version: saved.version,
            isActive: true
          });
          toast.success("Rezervasyon ekstrası aktifleştirildi");
          onSaved(activated);
          return;
        } catch (error) {
          toast.error(
            error instanceof Error && error.message
              ? `Değişiklikler kaydedildi ancak aktivasyon tamamlanamadı: ${error.message}`
              : "Değişiklikler kaydedildi ancak aktivasyon tamamlanamadı."
          );
          onSaved(saved);
          return;
        }
      }

      toast.success(
        option
          ? "Rezervasyon ekstrası güncellendi"
          : "Rezervasyon ekstrası taslak olarak kaydedildi"
      );
      onSaved(saved);
    } catch (error) {
      if (error instanceof ApiError && error.statusCode === 409) {
        setHasConflict(true);
        setServerError(
          "Bu kayıt başka bir işlemde değiştirildi. Devam etmeden önce güncel veriyi yükleyin."
        );
        return;
      }
      setServerError(
        error instanceof Error && error.message ? error.message : "İşlem tamamlanamadı."
      );
    }
  };

  const isSubmitting = form.formState.isSubmitting;
  const isEditingActive = option?.isActive === true;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-[900px]">
        <DialogHeader>
          <DialogTitle>
            {option ? "Rezervasyon Ekstrasını Düzenle" : "Yeni Rezervasyon Ekstrası"}
          </DialogTitle>
        </DialogHeader>

        <Form {...form}>
          <form className="space-y-5">
            {serverError && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>{hasConflict ? "Sürüm çakışması" : "İşlem tamamlanamadı"}</AlertTitle>
                <AlertDescription className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <span>{serverError}</span>
                  {hasConflict && (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => void onReload()}
                    >
                      Güncel veriyi yükle
                    </Button>
                  )}
                </AlertDescription>
              </Alert>
            )}

            <Tabs defaultValue="tr" className="w-full">
              <div className="max-w-full overflow-x-auto pb-1">
                <TabsList className="min-w-max">
                  {RESERVATION_EXTRA_LOCALES.map((locale) => {
                    const complete = Boolean(
                      translations[locale].name.trim() && translations[locale].description.trim()
                    );
                    return (
                      <TabsTrigger key={locale} value={locale} className="gap-2">
                        {localeLabels[locale]}
                        <Badge
                          variant={complete ? "default" : "secondary"}
                          className="px-1.5 text-[10px]"
                        >
                          {complete ? "Tam" : "Eksik"}
                        </Badge>
                      </TabsTrigger>
                    );
                  })}
                </TabsList>
              </div>
              {RESERVATION_EXTRA_LOCALES.map((locale) => (
                <TabsContent
                  key={locale}
                  value={locale}
                  className="grid gap-4 rounded-md border p-4 md:grid-cols-2"
                >
                  <FormField
                    control={form.control}
                    name={`translations.${locale}.name`}
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{localeLabels[locale]} ad</FormLabel>
                        <FormControl>
                          <Input maxLength={100} {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name={`translations.${locale}.description`}
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{localeLabels[locale]} açıklama</FormLabel>
                        <FormControl>
                          <Textarea maxLength={300} className="min-h-24" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </TabsContent>
              ))}
            </Tabs>

            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
              <FormField
                control={form.control}
                name="unitPrice"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Birim fiyat (TRY)</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={0}
                        max={1_000_000}
                        step="0.01"
                        name={field.name}
                        value={
                          typeof field.value === "string" || typeof field.value === "number"
                            ? field.value
                            : ""
                        }
                        onBlur={field.onBlur}
                        onChange={(event) => field.onChange(event.target.value)}
                        ref={field.ref}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="pricingMode"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Fiyat kuralı</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger aria-label="Fiyat kuralı">
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="PER_DAY">Günlük</SelectItem>
                        <SelectItem value="PER_RENTAL">Kiralama başına</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="maxQuantity"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Maksimum adet</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={1}
                        max={20}
                        name={field.name}
                        value={
                          typeof field.value === "string" || typeof field.value === "number"
                            ? field.value
                            : ""
                        }
                        onBlur={field.onBlur}
                        onChange={(event) => field.onChange(event.target.value)}
                        ref={field.ref}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="iconKey"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>İkon</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger aria-label="İkon">
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {RESERVATION_EXTRA_ICON_KEYS.map((key) => (
                          <SelectItem key={key} value={key}>
                            {iconLabels[key]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="sortOrder"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Sıra</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={0}
                        max={9_999}
                        name={field.name}
                        value={
                          typeof field.value === "string" || typeof field.value === "number"
                            ? field.value
                            : ""
                        }
                        onBlur={field.onBlur}
                        onChange={(event) => field.onChange(event.target.value)}
                        ref={field.ref}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="vehicleGroupIds"
              render={({ field }) => (
                <FormItem>
                  <div className="flex items-center justify-between gap-3">
                    <FormLabel>Araç grupları</FormLabel>
                    <span className="text-muted-foreground text-xs">
                      {field.value.length} grup seçili
                    </span>
                  </div>
                  <div className="grid max-h-40 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2 lg:grid-cols-3">
                    {vehicleGroups.map((group) => (
                      <label
                        key={group.id}
                        className="hover:bg-muted flex cursor-pointer items-center gap-2 rounded-md p-2 transition-colors"
                      >
                        <Checkbox
                          checked={field.value.includes(group.id)}
                          onCheckedChange={(checked) =>
                            field.onChange(
                              checked
                                ? [...field.value, group.id]
                                : field.value.filter((id) => id !== group.id)
                            )
                          }
                        />
                        <span className="text-sm">{groupLabel(group)}</span>
                      </label>
                    ))}
                    {vehicleGroups.length === 0 && (
                      <p className="text-muted-foreground text-sm">
                        Aktarılabilecek araç grubu bulunamadı.
                      </p>
                    )}
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="bg-muted/40 flex items-start gap-3 rounded-md border p-4">
              {readiness.isReady ? (
                <CheckCircle2 className="mt-0.5 h-5 w-5 text-emerald-600" />
              ) : (
                <AlertCircle className="mt-0.5 h-5 w-5 text-amber-600" />
              )}
              <div className="space-y-1">
                <p className="text-sm font-medium">
                  {readiness.isReady ? "Aktivasyona hazır" : "Taslak tamamlanmadı"}
                </p>
                <p className="text-muted-foreground text-xs">
                  {readiness.isReady
                    ? "Beş dil tamamlandı ve en az bir araç grubu seçildi. Son karar sunucu tarafından verilir."
                    : `${readiness.missingLocales.length} dil eksik. ${readiness.hasVehicleGroup ? "Araç grubu seçildi." : "En az bir araç grubu seçin."}`}
                </p>
              </div>
            </div>

            <DialogFooter className="gap-2 sm:justify-between">
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
                disabled={isSubmitting}
              >
                İptal
              </Button>
              <div className="flex flex-col-reverse gap-2 sm:flex-row">
                {!isEditingActive && (
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={isSubmitting}
                    onClick={form.handleSubmit((data) => save(data, false))}
                  >
                    {isSubmitting ? "Kaydediliyor..." : "Taslak Kaydet"}
                  </Button>
                )}
                <Button
                  type="button"
                  disabled={isSubmitting || (!isEditingActive && !readiness.isReady)}
                  onClick={form.handleSubmit((data) => save(data, true))}
                >
                  {isSubmitting
                    ? "Kaydediliyor..."
                    : isEditingActive
                      ? "Değişiklikleri Kaydet"
                      : "Kaydet ve Aktifleştir"}
                </Button>
              </div>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
