"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
import { toast } from "sonner";
import { createVehicleGroup, updateVehicleGroup } from "@/lib/api/admin/vehicles";
import type {
  AdminVehicleGroup,
  CreateVehicleGroupData,
} from "@/lib/api/admin/types";

const vehicleGroupSchema = z.object({
  nameTr: z.string().min(1, "Türkçe ad gereklidir"),
  nameEn: z.string().min(1, "İngilizce ad gereklidir"),
  nameRu: z.string().min(1, "Rusça ad gereklidir"),
  nameAr: z.string().min(1, "Arapça ad gereklidir"),
  nameDe: z.string().min(1, "Almanca ad gereklidir"),
  depositAmount: z.coerce.number().min(0, "Depozito 0 veya daha büyük olmalıdır"),
  minAge: z.coerce.number().min(18, "Minimum yaş 18 veya daha büyük olmalıdır"),
  minLicenseYears: z.coerce.number().min(0, "Ehliyet yılı 0 veya daha büyük olmalıdır"),
  featuresText: z.string().optional(),
});

type VehicleGroupFormInput = z.input<typeof vehicleGroupSchema>;
type VehicleGroupFormData = z.output<typeof vehicleGroupSchema>;

interface VehicleGroupDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  group?: AdminVehicleGroup;
  onSuccess: () => void;
}

function buildFeatures(value?: string): string[] {
  return (value ?? "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

export default function VehicleGroupDialog({
  open,
  onOpenChange,
  group,
  onSuccess,
}: VehicleGroupDialogProps) {
  const isEditing = !!group;

  const form = useForm<VehicleGroupFormInput, unknown, VehicleGroupFormData>({
    resolver: zodResolver(vehicleGroupSchema),
    defaultValues: {
      nameTr: "",
      nameEn: "",
      nameRu: "",
      nameAr: "",
      nameDe: "",
      depositAmount: 0,
      minAge: 21,
      minLicenseYears: 1,
      featuresText: "",
    },
  });

  useEffect(() => {
    form.reset({
      nameTr: group?.nameTr ?? "",
      nameEn: group?.nameEn ?? "",
      nameRu: group?.nameRu ?? "",
      nameAr: group?.nameAr ?? "",
      nameDe: group?.nameDe ?? "",
      depositAmount: group?.depositAmount ?? 0,
      minAge: group?.minAge ?? 21,
      minLicenseYears: group?.minLicenseYears ?? 1,
      featuresText: group?.features?.join(", ") ?? "",
    });
  }, [form, group, open]);

  const buildPayload = (data: VehicleGroupFormData): CreateVehicleGroupData => ({
    nameTr: data.nameTr,
    nameEn: data.nameEn,
    nameRu: data.nameRu,
    nameAr: data.nameAr,
    nameDe: data.nameDe,
    depositAmount: data.depositAmount,
    minAge: data.minAge,
    minLicenseYears: data.minLicenseYears,
    features: buildFeatures(data.featuresText),
  });

  const onSubmit = async (data: VehicleGroupFormData) => {
    try {
      const payload = buildPayload(data);

      if (isEditing && group) {
        await updateVehicleGroup(group.id, payload);
        toast.success("Araç grubu başarıyla güncellendi");
      } else {
        await createVehicleGroup(payload);
        toast.success("Araç grubu başarıyla oluşturuldu");
      }

      onSuccess();
    } catch (error) {
      toast.error(isEditing ? "Araç grubu güncellenemedi" : "Araç grubu oluşturulamadı");
      console.error(error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Araç Grubu Düzenle" : "Yeni Araç Grubu"}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              {(["nameTr", "nameEn", "nameRu", "nameAr", "nameDe"] as const).map((name) => (
                <FormField
                  key={name}
                  control={form.control}
                  name={name}
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>
                        {name === "nameTr"
                          ? "Ad (TR)"
                          : name === "nameEn"
                          ? "Ad (EN)"
                          : name === "nameRu"
                          ? "Ad (RU)"
                          : name === "nameAr"
                          ? "Ad (AR)"
                          : "Ad (DE)"}
                      </FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ))}
            </div>

            <div className="grid grid-cols-3 gap-4">
              <FormField
                control={form.control}
                name="depositAmount"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Depozito (TL)</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        name={field.name}
                        value={typeof field.value === "number" ? field.value : ""}
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
                name="minAge"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Min. Yaş</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        name={field.name}
                        value={typeof field.value === "number" ? field.value : ""}
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
                name="minLicenseYears"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Ehliyet Yılı</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        name={field.name}
                        value={typeof field.value === "number" ? field.value : ""}
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
              name="featuresText"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Özellikler</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder="Otomatik vites, Klima, Bluetooth"
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                İptal
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting
                  ? "Kaydediliyor..."
                  : isEditing
                  ? "Güncelle"
                  : "Oluştur"}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
