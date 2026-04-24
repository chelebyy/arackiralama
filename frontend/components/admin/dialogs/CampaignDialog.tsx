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
import { Checkbox } from "@/components/ui/checkbox";
import { toast } from "sonner";
import type { Campaign, AdminVehicleGroup } from "@/lib/api/admin/types";
import { createCampaign, updateCampaign } from "@/lib/api/admin/pricing";

const campaignSchema = z.object({
  code: z.string().min(1, "Kampanya kodu gereklidir"),
  name: z.string().min(1, "Kampanya adı gereklidir"),
  description: z.string().min(1, "Açıklama gereklidir"),
  discountType: z.enum(["PERCENTAGE", "FIXED_AMOUNT"]),
  discountValue: z.number().min(0, "İndirim değeri 0 veya daha büyük olmalıdır"),
  minRentalDays: z.number().min(1, "Minimum kiralama günü en az 1 olmalıdır"),
  validFrom: z.string().min(1, "Başlangıç tarihi gereklidir"),
  validUntil: z.string().min(1, "Bitiş tarihi gereklidir"),
  isActive: z.boolean(),
});

type CampaignFormData = z.infer<typeof campaignSchema>;

interface CampaignDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  campaign?: Campaign;
  onSuccess: () => void;
  vehicleGroups?: AdminVehicleGroup[];
}

export default function CampaignDialog({
  open,
  onOpenChange,
  campaign,
  onSuccess,
}: CampaignDialogProps) {
  const isEditing = !!campaign;

  const form = useForm<CampaignFormData>({
    resolver: zodResolver(campaignSchema),
    defaultValues: {
      code: "",
      name: "",
      description: "",
      discountType: "PERCENTAGE",
      discountValue: 0,
      minRentalDays: 1,
      validFrom: "",
      validUntil: "",
      isActive: true,
    },
  });

  useEffect(() => {
    if (campaign) {
      form.reset({
        code: campaign.code,
        name: campaign.name,
        description: campaign.description || "",
        discountType: campaign.discountType === "fixed" ? "FIXED_AMOUNT" : "PERCENTAGE",
        discountValue: campaign.discountValue,
        minRentalDays: campaign.minRentalDays || 1,
        validFrom: campaign.validFrom || "",
        validUntil: campaign.validUntil || "",
        isActive: campaign.isActive,
      });
    } else {
      form.reset({
        code: "",
        name: "",
        description: "",
        discountType: "PERCENTAGE",
        discountValue: 0,
        minRentalDays: 1,
        validFrom: "",
        validUntil: "",
        isActive: true,
      });
    }
  }, [campaign, form, open]);

  const onSubmit = async (data: CampaignFormData) => {
    try {
      const payload = {
        ...data,
        discountType: (data.discountType === "FIXED_AMOUNT" ? "fixed" : "percentage") as "fixed" | "percentage",
      };
      
      if (isEditing && campaign) {
        await updateCampaign(campaign.id, payload as any);
        toast.success("Kampanya başarıyla güncellendi");
      } else {
        await createCampaign(payload as any);
        toast.success("Kampanya başarıyla oluşturuldu");
      }
      onSuccess();
    } catch (error) {
      toast.error(isEditing ? "Kampanya güncellenemedi" : "Kampanya oluşturulamadı");
      console.error(error);
    }
  };

  const discountType = form.watch("discountType");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Kampanya Düzenle" : "Yeni Kampanya Ekle"}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="code"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Kampanya Kodu</FormLabel>
                  <FormControl>
                    <Input placeholder="SUMMER2024" {...field} />
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
                  <FormLabel>Kampanya Adı</FormLabel>
                  <FormControl>
                    <Input placeholder="Yaz İndirimi" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Açıklama</FormLabel>
                  <FormControl>
                    <Input placeholder="Kampanya açıklaması..." {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="discountType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>İndirim Tipi</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Tip seçin" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="PERCENTAGE">Yüzde (%)</SelectItem>
                        <SelectItem value="FIXED_AMOUNT">Sabit Tutar (₺)</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="discountValue"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>
                      İndirim Değeri {discountType === "PERCENTAGE" ? "(%)" : "(₺)"}
                    </FormLabel>
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
              name="minRentalDays"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Minimum Kiralama Günü</FormLabel>
                  <FormControl>
                    <Input type="number" min={1} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="validFrom"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Başlangıç Tarihi</FormLabel>
                    <FormControl>
                      <Input type="date" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="validUntil"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Bitiş Tarihi</FormLabel>
                    <FormControl>
                      <Input type="date" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
                  <FormControl>
                    <Checkbox
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </FormControl>
                  <div className="space-y-1 leading-none">
                    <FormLabel>Aktif</FormLabel>
                  </div>
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
