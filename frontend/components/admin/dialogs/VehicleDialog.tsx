"use client";

import { useEffect, useRef } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { UploadIcon } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter
} from "@/components/ui/dialog";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue
} from "@/components/ui/select";
import { toast } from "sonner";
import type {
  AdminVehicle,
  AdminVehicleGroup,
  AdminOffice,
  CreateVehicleData
} from "@/lib/api/admin/types";
import {
  createVehicle,
  updateVehicle,
  uploadVehiclePhoto,
  updateVehiclePhotos
} from "@/lib/api/admin/vehicles";
import { equipmentCodes, resolveVehicleMedia } from "@/lib/vehicle-catalogue";

const vehicleSchema = z.object({
  plate: z.string().min(1, "Plaka gereklidir"),
  brand: z.string().min(1, "Marka gereklidir"),
  model: z.string().min(1, "Model gereklidir"),
  year: z.coerce.number().min(1990, "Yıl 1990 veya daha yeni olmalıdır"),
  color: z.string().min(1, "Renk gereklidir"),
  groupId: z.string().min(1, "Araç grubu seçilmelidir"),
  officeId: z.string().min(1, "Ofis seçilmelidir"),
  status: z.enum(["Available", "Reserved", "Rented", "Maintenance", "OutOfService", "Retired"]),
  photo: z.array(z.instanceof(File)).optional(),
  transmission: z.string().optional(),
  fuelType: z.string().optional(),
  seatCount: z.string().optional(),
  luggageCapacity: z.string().optional(),
  bodyType: z.string().optional(),
  doorCount: z.string().optional(),
  engine: z.string().optional(),
  powerHp: z.string().optional(),
  equipment: z.array(z.string()).optional(),
  photoUrls: z.array(z.string()).optional()
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
  groups
}: VehicleDialogProps) {
  const isEditing = !!vehicle;
  const savedId = useRef<string | null>(null);
  const normalizeStatus = (
    status: AdminVehicle["status"] | undefined
  ): VehicleFormData["status"] => {
    if (status === 0 || status === "Available") return "Available";
    if (status === 1 || status === "Reserved") return "Reserved";
    if (status === 2 || status === "Rented") return "Rented";
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
      photo: undefined
    }
  });

  useEffect(() => {
    savedId.current = vehicle?.id ?? null;
    if (vehicle) {
      form.reset({
        transmission: vehicle.transmission == null ? "" : String(vehicle.transmission),
        fuelType: vehicle.fuelType == null ? "" : String(vehicle.fuelType),
        seatCount: vehicle.seatCount == null ? "" : String(vehicle.seatCount),
        luggageCapacity: vehicle.luggageCapacity == null ? "" : String(vehicle.luggageCapacity),
        bodyType: vehicle.bodyType == null ? "" : String(vehicle.bodyType),
        doorCount: vehicle.doorCount == null ? "" : String(vehicle.doorCount),
        engine: vehicle.engine == null ? "" : String(vehicle.engine),
        powerHp: vehicle.powerHp == null ? "" : String(vehicle.powerHp),
        equipment: vehicle.equipment ?? [],
        photoUrls: vehicle.photoUrls?.length
          ? vehicle.photoUrls
          : vehicle.photoUrl
            ? [vehicle.photoUrl]
            : [],
        plate: vehicle.plate,
        brand: vehicle.brand ?? "",
        model: vehicle.model ?? "",
        year: vehicle.year ?? new Date().getFullYear(),
        color: vehicle.color ?? "",
        groupId: vehicle.groupId || "",
        officeId: vehicle.officeId || "",
        status: normalizeStatus(vehicle.status),
        photo: undefined
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
        photo: undefined
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
    transmission: (data.transmission || null) as CreateVehicleData["transmission"],
    fuelType: (data.fuelType || null) as CreateVehicleData["fuelType"],
    bodyType: (data.bodyType || null) as CreateVehicleData["bodyType"],
    engine: data.engine?.trim() || null,
    seatCount: data.seatCount ? Number(data.seatCount) : null,
    luggageCapacity: data.luggageCapacity ? Number(data.luggageCapacity) : null,
    doorCount: data.doorCount ? Number(data.doorCount) : null,
    powerHp: data.powerHp ? Number(data.powerHp) : null,
    equipment: data.equipment ?? []
  });

  const onSubmit = async (data: VehicleFormData) => {
    try {
      const payload = buildVehiclePayload(data);
      let savedVehicle: AdminVehicle;

      if (savedId.current) {
        savedVehicle = await updateVehicle(savedId.current, payload);
      } else {
        savedVehicle = await createVehicle(payload);
        savedId.current = savedVehicle.id;
      }

      if (data.photoUrls) {
        savedVehicle = await updateVehiclePhotos(savedVehicle.id, data.photoUrls);
      }
      for (const photo of data.photo ?? []) {
        savedVehicle = await uploadVehiclePhoto(savedVehicle.id, photo);
        form.setValue("photoUrls", savedVehicle.photoUrls ?? []);
        form.setValue(
          "photo",
          (form.getValues("photo") ?? []).filter((item) => item !== photo)
        );
      }

      toast.success(
        isEditing
          ? data.photo
            ? "Araç ve görsel başarıyla güncellendi"
            : "Araç başarıyla güncellendi"
          : data.photo
            ? "Araç ve görsel başarıyla oluşturuldu"
            : "Araç başarıyla oluşturuldu"
      );
      onSuccess();
    } catch (error) {
      const reason = error instanceof Error ? error.message : "İşlem tamamlanamadı.";
      toast.error(savedId.current ? `Araç kaydı korunuyor. ${reason}` : reason);
      console.error(error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
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
                        {field.value === "Reserved" && <SelectItem value="Reserved">Rezerve</SelectItem>}
                        {field.value === "Rented" && <SelectItem value="Rented">Kirada</SelectItem>}
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
                        multiple
                        onChange={(event) => onChange(Array.from(event.target.files ?? []))}
                      />
                      <label
                        htmlFor="vehicle-photo"
                        className="border-input bg-background hover:bg-accent hover:text-accent-foreground flex h-24 cursor-pointer flex-col items-center justify-center gap-2 rounded-md border border-dashed px-3 py-4 text-sm transition-colors"
                      >
                        <UploadIcon className="text-muted-foreground size-5" aria-hidden="true" />
                        <span className="font-medium">
                          {_value?.length
                            ? _value.map((file) => file.name).join(", ")
                            : "Galeriye fotoğraf ekle"}
                        </span>
                        <span className="text-muted-foreground text-xs">
                          JPG, PNG veya WEBP · Her biri en fazla 5 MB · En fazla 12 fotoğraf
                        </span>
                      </label>
                    </div>
                  </FormControl>
                  {vehicle?.photoUrl && (
                    <p className="text-muted-foreground text-xs">
                      Mevcut görsel: {vehicle.photoUrl}
                    </p>
                  )}
                  <FormMessage />
                </FormItem>
              )}
            />

            <fieldset className="space-y-4 rounded-lg border p-4">
              <legend className="px-2 font-medium">Araç kataloğu</legend>
              <p className="text-muted-foreground text-sm">
                Bilinmeyen bilgileri boş bırakın. Yalnızca doğrulanmış donanımı seçin.
              </p>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                {(
                  [
                    [
                      "transmission",
                      "Vites",
                      [
                        ["manual", "Manuel"],
                        ["automatic", "Otomatik"],
                        ["semiAutomatic", "Yarı otomatik"]
                      ]
                    ],
                    [
                      "fuelType",
                      "Yakıt",
                      [
                        ["gasoline", "Benzin"],
                        ["diesel", "Dizel"],
                        ["hybrid", "Hibrit"],
                        ["electric", "Elektrik"],
                        ["lpg", "LPG"]
                      ]
                    ],
                    [
                      "bodyType",
                      "Kasa",
                      [
                        ["sedan", "Sedan"],
                        ["hatchback", "Hatchback"],
                        ["suv", "SUV"],
                        ["estate", "Station wagon"],
                        ["van", "Van"],
                        ["coupe", "Coupe"],
                        ["convertible", "Cabrio"]
                      ]
                    ]
                  ] as const
                ).map(([name, label, options]) => (
                  <label key={name} className="grid gap-2 text-sm">
                    {label}
                    <select
                      {...form.register(name)}
                      className="bg-background rounded-md border p-2"
                    >
                      <option value="">Belirtilmedi</option>
                      {options.map(([value, text]) => (
                        <option key={value} value={value}>
                          {text}
                        </option>
                      ))}
                    </select>
                  </label>
                ))}
                {(
                  [
                    ["seatCount", "Koltuk sayısı", 1, 20],
                    ["luggageCapacity", "Yaklaşık bagaj (bavul)", 0, 20],
                    ["doorCount", "Kapı sayısı", 1, 6],
                    ["powerHp", "Güç (bg)", 1, 2000]
                  ] as const
                ).map(([name, label, min, max]) => (
                  <label key={name} className="grid gap-2 text-sm">
                    {label}
                    <Input
                      {...form.register(name)}
                      type="number"
                      min={min}
                      max={max}
                      step={1}
                      placeholder="Belirtilmedi"
                    />
                  </label>
                ))}
                <label className="grid gap-2 text-sm">
                  Motor
                  <Input {...form.register("engine")} maxLength={80} placeholder="Belirtilmedi" />
                </label>
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                {equipmentCodes.map((code, index) => (
                  <label key={code} className="flex items-center gap-2 text-sm">
                    <input type="checkbox" value={code} {...form.register("equipment")} />
                    {
                      [
                        "Klima",
                        "Bluetooth",
                        "Navigasyon",
                        "Park sensörü",
                        "Geri görüş kamerası",
                        "Hız sabitleyici",
                        "Çocuk koltuğu bağlantıları"
                      ][index]
                    }
                  </label>
                ))}
              </div>
            </fieldset>
            <FormField
              control={form.control}
              name="photoUrls"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Sıralı galeri · İlk fotoğraf kapaktır</FormLabel>
                  <div className="space-y-2">
                    {(field.value ?? []).map((url, index) => (
                      <div key={url} className="flex items-center gap-2 rounded border p-2">
                        <img
                          src={resolveVehicleMedia(url)}
                          alt={`Araç fotoğrafı ${index + 1}`}
                          className="h-14 w-20 rounded object-cover"
                        />
                        <span className="text-sm">{index + 1}</span>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={index === 0}
                          aria-label={`Fotoğraf ${index + 1} öne taşı`}
                          onClick={() => {
                            const next = [...(field.value ?? [])];
                            [next[index - 1], next[index]] = [next[index], next[index - 1]];
                            field.onChange(next);
                          }}
                        >
                          Öne
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          aria-label={`Fotoğraf ${index + 1} kaldır`}
                          onClick={() =>
                            field.onChange(field.value?.filter((item) => item !== url))
                          }
                        >
                          Kaldır
                        </Button>
                      </div>
                    ))}
                  </div>
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
