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
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import type { AdminVehicle, AdminVehicleGroup, AdminOffice } from "@/lib/api/admin/types";
import { createVehicle, updateVehicle } from "@/lib/api/admin/vehicles";

const vehicleSchema = z.object({
  plate: z.string().min(1, "Plaka gereklidir"),
  name: z.string().min(1, "Araç adı gereklidir"),
  groupId: z.string().min(1, "Araç grubu seçilmelidir"),
  officeId: z.string().min(1, "Ofis seçilmelidir"),
  status: z.enum(["Available", "Maintenance", "Retired"]),
  mileage: z.number().min(0, "Kilometre 0 veya daha büyük olmalıdır"),
  adminNotes: z.string().optional(),
});

type VehicleFormData = z.infer<typeof vehicleSchema>;

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

  const form = useForm<VehicleFormData>({
    resolver: zodResolver(vehicleSchema),
    defaultValues: {
      plate: "",
      name: "",
      groupId: "",
      officeId: "",
      status: "Available",
      mileage: 0,
      adminNotes: "",
    },
  });

  useEffect(() => {
    if (vehicle) {
      form.reset({
        plate: vehicle.plate,
        name: vehicle.name,
        groupId: vehicle.groupId || "",
        officeId: vehicle.officeId || "",
        status: vehicle.status,
        mileage: vehicle.mileage || 0,
        adminNotes: vehicle.adminNotes || "",
      });
    } else {
      form.reset({
        plate: "",
        name: "",
        groupId: "",
        officeId: "",
        status: "Available",
        mileage: 0,
        adminNotes: "",
      });
    }
  }, [vehicle, form, open]);

  const onSubmit = async (data: VehicleFormData) => {
    try {
      if (isEditing && vehicle) {
        await updateVehicle(vehicle.id, data as any);
        toast.success("Araç başarıyla güncellendi");
      } else {
        await createVehicle(data as any);
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

            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Araç Adı</FormLabel>
                  <FormControl>
                    <Input placeholder="Fiat Egea" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

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
                            {group.name}
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

              <FormField
                control={form.control}
                name="mileage"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Kilometre</FormLabel>
                    <FormControl>
                      <Input type="number" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="adminNotes"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Admin Notları</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder="Araç hakkında notlar..."
                      className="resize-none"
                      {...field}
                    />
                  </FormControl>
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
