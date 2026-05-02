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
import type { DayHours, OpeningHours } from "@/lib/api/types";
import type { AdminOffice, CreateOfficeData } from "@/lib/api/admin/types";
import { createOffice, updateOffice } from "@/lib/api/admin/vehicles";

const defaultDayHours: DayHours = {
  open: "09:00",
  close: "18:00",
  isClosed: false,
};

const defaultOpeningHours: OpeningHours = {
  monday: { ...defaultDayHours },
  tuesday: { ...defaultDayHours },
  wednesday: { ...defaultDayHours },
  thursday: { ...defaultDayHours },
  friday: { ...defaultDayHours },
  saturday: { ...defaultDayHours },
  sunday: { ...defaultDayHours },
};

const dayHoursSchema = z.object({
  open: z.string(),
  close: z.string(),
  isClosed: z.boolean(),
});

const officeSchema = z.object({
  name: z.string().min(1, "Ofis adı gereklidir"),
  code: z.string().min(1, "Ofis kodu gereklidir"),
  type: z.enum(["airport", "hotel", "office"]),
  city: z.string().min(1, "Şehir gereklidir"),
  district: z.string(),
  address: z.string().min(1, "Adres gereklidir"),
  phone: z.string().min(1, "Telefon gereklidir"),
  email: z.string().email("Geçerli bir email adresi giriniz"),
  isActive: z.boolean(),
  isAirport: z.boolean(),
  isHotel: z.boolean(),
  coordinates: z.object({
    latitude: z.coerce.number(),
    longitude: z.coerce.number(),
  }),
  openingHours: z.object({
    monday: dayHoursSchema,
    tuesday: dayHoursSchema,
    wednesday: dayHoursSchema,
    thursday: dayHoursSchema,
    friday: dayHoursSchema,
    saturday: dayHoursSchema,
    sunday: dayHoursSchema,
  }),
  services: z.array(z.string()),
});

type OfficeFormInput = z.input<typeof officeSchema>;
type OfficeFormData = z.output<typeof officeSchema>;

interface OfficeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  office?: AdminOffice;
  onSuccess: () => void;
}

export default function OfficeDialog({
  open,
  onOpenChange,
  office,
  onSuccess,
}: OfficeDialogProps) {
  const isEditing = !!office;

  const form = useForm<OfficeFormInput, unknown, OfficeFormData>({
    resolver: zodResolver(officeSchema),
    defaultValues: {
      name: "",
      code: "",
      type: "office",
      city: "",
      district: "",
      address: "",
      phone: "",
      email: "",
      isActive: true,
      isAirport: false,
      isHotel: false,
      coordinates: { latitude: 0, longitude: 0 },
      openingHours: defaultOpeningHours,
      services: [],
    },
  });

  useEffect(() => {
    if (office) {
      form.reset({
        name: office.name,
        code: office.code,
        type: office.type,
        city: office.city,
        district: office.district || "",
        address: office.address,
        phone: office.phone,
        email: office.email,
        isActive: office.isActive,
        isAirport: office.isAirport,
        isHotel: office.isHotel,
        coordinates: office.coordinates || { latitude: 0, longitude: 0 },
        openingHours: office.openingHours || defaultOpeningHours,
        services: office.services || [],
      });
    } else {
      form.reset({
        name: "",
        code: "",
        type: "office",
        city: "",
        district: "",
        address: "",
        phone: "",
        email: "",
        isActive: true,
        isAirport: false,
        isHotel: false,
        coordinates: { latitude: 0, longitude: 0 },
        openingHours: defaultOpeningHours,
        services: [],
      });
    }
  }, [office, form, open]);

  const buildOfficePayload = (data: OfficeFormData): CreateOfficeData => ({
    name: data.name,
    code: data.code,
    type: data.type,
    city: data.city,
    district: data.district,
    address: data.address,
    phone: data.phone,
    email: data.email,
    isActive: data.isActive,
    isAirport: data.isAirport,
    isHotel: data.isHotel,
    coordinates: data.coordinates,
    openingHours: data.openingHours,
    services: data.services,
  });

  const onSubmit = async (data: OfficeFormData) => {
    try {
      const payload = buildOfficePayload(data);

      if (isEditing && office) {
        await updateOffice(office.id, payload);
        toast.success("Ofis başarıyla güncellendi");
      } else {
        await createOffice(payload);
        toast.success("Ofis başarıyla oluşturuldu");
      }
      onSuccess();
    } catch (error) {
      toast.error(isEditing ? "Ofis güncellenemedi" : "Ofis oluşturulamadı");
      console.error(error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Ofis Düzenle" : "Yeni Ofis Ekle"}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Ofis Adı</FormLabel>
                    <FormControl>
                      <Input placeholder="Alanya Merkez" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="code"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Ofis Kodu</FormLabel>
                    <FormControl>
                      <Input placeholder="ALN-MRK" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="type"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Ofis Tipi</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Tip seçin" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      <SelectItem value="office">Ofis</SelectItem>
                      <SelectItem value="airport">Havalimanı</SelectItem>
                      <SelectItem value="hotel">Otel</SelectItem>
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="city"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Şehir</FormLabel>
                  <FormControl>
                    <Input placeholder="Alanya" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="address"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Adres</FormLabel>
                  <FormControl>
                    <Input placeholder="Saray Mah. Atatürk Cad. No:1" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="phone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Telefon</FormLabel>
                    <FormControl>
                      <Input placeholder="+90 242 123 45 67" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Email</FormLabel>
                    <FormControl>
                      <Input placeholder="alanya@ornek.com" {...field} />
                    </FormControl>
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
