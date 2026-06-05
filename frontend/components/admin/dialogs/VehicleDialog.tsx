"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import type {
  AdminVehicle,
  AdminVehicleGroup,
  AdminOffice,
  CreateVehicleData,
} from "@/lib/api/admin/types";
import { createVehicle, updateVehicle } from "@/lib/api/admin/vehicles";

const vehicleSchema = z.object({
  plate: z.string().min(1, "Plaka gereklidir"),
  brand: z.string().min(1, "Marka gereklidir"),
  model: z.string().min(1, "Model gereklidir"),
  year: z.coerce.number().min(1990, "Yıl 1990 veya daha yeni olmalıdır"),
  color: z.string().min(1, "Renk gereklidir"),
  groupId: z.string().min(1, "Araç grubu seçilmelidir"),
  officeId: z.string().min(1, "Ofis seçilmelidir"),
  status: z.enum(["Available", "Maintenance", "Retired"]),
});

type VehicleFormInput = z.input<typeof vehicleSchema>;
type VehicleFormData = z.output<typeof vehicleSchema>;

interface VehicleDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  vehicle?: AdminVehicle;
  onSuccess: () => void;
  offices: AdminOffice[];
  groups: AdminVehicleGroup[];
}

export default function VehicleDialog({
  open,
  onOpenChange,
  vehicle,
  onSuccess,
  offices,
  groups,
}: VehicleDialogProps) {
  const isEditing = !!vehicle;

  const form = useForm<VehicleFormInput, unknown, VehicleFormData>({
    resolver: zodResolver(vehicleSchema),
    defaultValues: {
      plate: "",
      brand: "",
      model: "",
      year: new Date().getFullYear(),
      color: "",
      groupId: "",
      officeId: "",
      status: "Available",
    },
  });

  useEffect(() => {
    if (vehicle) {
      form.reset({
        plate: vehicle.plate,
        brand: vehicle.brand ?? "",
        model: vehicle.model ?? "",
        year: vehicle.year ?? new Date().getFullYear(),
        color: vehicle.color ?? "",
        groupId: vehicle.groupId || "",
        officeId: vehicle.officeId || "",
        status: vehicle.status,
      });
    } else {
      form.reset({
        plate: "",
        brand: "",
        model: "",
        year: new Date().getFullYear(),
        color: "",
        groupId: "",
        officeId: "",
        status: "Available",
      });
    }
  }, [vehicle, form, open]);

  const buildVehiclePayload = (data: VehicleFormData): CreateVehicleData => ({
    plate: data.plate,
    brand: data.brand,
    model: data.model,
    year: data.year,
    color: data.color,
    groupId: data.groupId,
    officeId: data.officeId,
    status: data.status,
  });

  const onSubmit = async (data: VehicleFormData) => {
    try {
      const payload = buildVehiclePayload(data);

      if (isEditing && vehicle) {
        await updateVehicle(vehicle.id, payload);
        toast.success("Araç başarıyla güncellendi");
      } else {
        await createVehicle(payload);
        toast.success("Araç başarıyla oluşturuldu");
      }
      onSuccess();
    } catch (error) {
      toast.error(isEditing ? "Araç güncellenemedi" : "Araç oluşturulamadı");
      console.error(error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Araç Düzenle" : "Yeni Araç Ekle"}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="plate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Plaka</FormLabel>
                  <FormControl>
                    <Input placeholder="34 ABC 123" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="brand"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Marka</FormLabel>
                    <FormControl>
                      <Input placeholder="Fiat" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="model"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Model</FormLabel>
                    <FormControl>
                      <Input placeholder="Egea" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="year"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Yıl</FormLabel>
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
                name="color"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Renk</FormLabel>
                    <FormControl>
                      <Input placeholder="Beyaz" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="groupId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Araç Grubu</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Grup seçin" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {groups.map((group) => (
                          <SelectItem key={group.id} value={group.id}>
                            {group.nameTr ?? group.nameEn ?? group.name}
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
                name="officeId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Ofis</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Ofis seçin" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {offices.map((office) => (
                          <SelectItem key={office.id} value={office.id}>
                            {office.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="status"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Durum</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Durum seçin" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="Available">Müsait</SelectItem>
                        <SelectItem value="Maintenance">Bakımda</SelectItem>
                        <SelectItem value="Retired">Emekli</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

            </div>

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
              >
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
