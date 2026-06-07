"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { UploadIcon } from "lucide-react";
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
import { createVehicle, updateVehicle, uploadVehiclePhoto } from "@/lib/api/admin/vehicles";

const vehicleSchema = z.object({
  plate: z.string().min(1, "Plaka gereklidir"),
  brand: z.string().min(1, "Marka gereklidir"),
  model: z.string().min(1, "Model gereklidir"),
  year: z.coerce.number().min(1990, "Yıl 1990 veya daha yeni olmalıdır"),
  color: z.string().min(1, "Renk gereklidir"),
  groupId: z.string().min(1, "Araç grubu seçilmelidir"),
  officeId: z.string().min(1, "Ofis seçilmelidir"),
  status: z.enum(["Available", "Maintenance", "OutOfService", "Retired"]),
  photo: z.instanceof(File).optional(),
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
  const normalizeStatus = (status: AdminVehicle["status"] | undefined): VehicleFormData["status"] => {
    if (status === 0 || status === "Available") return "Available";
    if (status === 3 || status === "Maintenance") return "Maintenance";
    if (status === 4 || status === "OutOfService") return "OutOfService";
    if (status === 5 || status === "Retired") return "Retired";
    return "Available";
  };

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
      photo: undefined,
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
        status: normalizeStatus(vehicle.status),
        photo: undefined,
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
        photo: undefined,
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
      let savedVehicle: AdminVehicle;

      if (isEditing && vehicle) {
        savedVehicle = await updateVehicle(vehicle.id, payload);
      } else {
        savedVehicle = await createVehicle(payload);
      }

      if (data.photo) {
        await uploadVehiclePhoto(savedVehicle.id, data.photo);
      }

      toast.success(
        isEditing
          ? data.photo
            ? "Araç ve görsel başarıyla güncellendi"
            : "Araç başarıyla güncellendi"
          : data.photo
          ? "Araç ve görsel başarıyla oluşturuldu"
          : "Araç başarıyla oluşturuldu",
      );
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
                        value={field.value == null ? "" : String(field.value)}
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
                        <SelectItem value="OutOfService">Servis Dışı</SelectItem>
                        <SelectItem value="Retired">Arşivli</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

            </div>

            <FormField
              control={form.control}
              name="photo"
              render={({ field: { value: _value, onChange, ...field } }) => (
                <FormItem>
                  <FormLabel>Araç Görseli</FormLabel>
                  <FormControl>
                    <div>
                      <Input
                        {...field}
                        id="vehicle-photo"
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        className="sr-only"
                        onChange={(event) => onChange(event.target.files?.[0])}
                      />
                      <label
                        htmlFor="vehicle-photo"
                        className="border-input bg-background hover:bg-accent hover:text-accent-foreground flex h-24 cursor-pointer flex-col items-center justify-center gap-2 rounded-md border border-dashed px-3 py-4 text-sm transition-colors"
                      >
                        <UploadIcon className="size-5 text-muted-foreground" aria-hidden="true" />
                        <span className="font-medium">
                          {_value instanceof File ? _value.name : "Dosya görseli seç"}
                        </span>
                        <span className="text-xs text-muted-foreground">
                          JPG, PNG veya WEBP
                        </span>
                      </label>
                    </div>
                  </FormControl>
                  {vehicle?.photoUrl && (
                    <p className="text-xs text-muted-foreground">
                      Mevcut görsel: {vehicle.photoUrl}
                    </p>
                  )}
                  <FormMessage />
                </FormItem>
              )}
            />

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
