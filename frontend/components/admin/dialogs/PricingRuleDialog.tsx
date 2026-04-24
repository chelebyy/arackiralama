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
import type { PricingRule, AdminVehicleGroup } from "@/lib/api/admin/types";
import { createPricingRule, updatePricingRule } from "@/lib/api/admin/pricing";

const pricingRuleSchema = z.object({
  vehicleGroupId: z.string().min(1, "Araç grubu seçilmelidir"),
  startDate: z.string().min(1, "Başlangıç tarihi gereklidir"),
  endDate: z.string().min(1, "Bitiş tarihi gereklidir"),
  dailyPrice: z.number().min(0, "Günlük fiyat 0 veya daha büyük olmalıdır"),
  multiplier: z.number().min(0, "Çarpan 0 veya daha büyük olmalıdır"),
  priority: z.number().min(0, "Öncelik 0 veya daha büyük olmalıdır"),
  calculationType: z.enum(["multiplier", "fixed"]),
});

type PricingRuleFormData = z.infer<typeof pricingRuleSchema>;

interface PricingRuleDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  rule?: PricingRule;
  onSuccess: () => void;
  vehicleGroups: AdminVehicleGroup[];
}

export default function PricingRuleDialog({
  open,
  onOpenChange,
  rule,
  onSuccess,
  vehicleGroups,
}: PricingRuleDialogProps) {
  const isEditing = !!rule;

  const form = useForm<PricingRuleFormData>({
    resolver: zodResolver(pricingRuleSchema),
    defaultValues: {
      vehicleGroupId: "",
      startDate: "",
      endDate: "",
      dailyPrice: 0,
      multiplier: 1,
      priority: 0,
      calculationType: "multiplier",
    },
  });

  useEffect(() => {
    if (rule) {
      form.reset({
        vehicleGroupId: rule.vehicleGroupId,
        startDate: rule.startDate,
        endDate: rule.endDate,
        dailyPrice: rule.dailyPrice,
        multiplier: rule.multiplier,
        priority: rule.priority,
        calculationType: rule.calculationType,
      });
    } else {
      form.reset({
        vehicleGroupId: "",
        startDate: "",
        endDate: "",
        dailyPrice: 0,
        multiplier: 1,
        priority: 0,
        calculationType: "multiplier",
      });
    }
  }, [rule, form, open]);

  const onSubmit = async (data: PricingRuleFormData) => {
    try {
      if (isEditing && rule) {
        await updatePricingRule(rule.id, data as any);
        toast.success("Fiyat kuralı başarıyla güncellendi");
      } else {
        await createPricingRule(data as any);
        toast.success("Fiyat kuralı başarıyla oluşturuldu");
      }
      onSuccess();
    } catch (error) {
      toast.error(isEditing ? "Fiyat kuralı güncellenemedi" : "Fiyat kuralı oluşturulamadı");
      console.error(error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Fiyat Kuralı Düzenle" : "Yeni Fiyat Kuralı Ekle"}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="vehicleGroupId"
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
                      {vehicleGroups.map((group) => (
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

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="startDate"
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
                name="endDate"
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

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="dailyPrice"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Günlük Fiyat (₺)</FormLabel>
                    <FormControl>
                      <Input type="number" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="multiplier"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Çarpan</FormLabel>
                    <FormControl>
                      <Input type="number" step="0.1" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="priority"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Öncelik</FormLabel>
                    <FormControl>
                      <Input type="number" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="calculationType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Hesaplama Tipi</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Tip seçin" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="multiplier">Çarpan</SelectItem>
                        <SelectItem value="fixed">Sabit</SelectItem>
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
