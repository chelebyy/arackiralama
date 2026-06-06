"use client";

import { useEffect, useMemo } from "react";
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
import { createAdminUser, updateAdminUser } from "@/lib/api/admin/users";
import type { AdminUser } from "@/lib/api/admin/types";

const adminUserBaseSchema = {
  fullName: z.string().min(1, "Ad soyad gereklidir"),
  email: z.string().email("Geçerli bir email adresi giriniz"),
  role: z.enum(["Admin", "SuperAdmin"]),
};

const createAdminUserSchema = z.object({
  ...adminUserBaseSchema,
  password: z.string().min(6, "Şifre en az 6 karakter olmalıdır"),
});

const updateAdminUserSchema = z.object({
  ...adminUserBaseSchema,
  password: z.string().optional(),
});

type AdminUserFormData = z.infer<typeof updateAdminUserSchema>;

interface AdminUserDialogProps {
  adminUser?: AdminUser | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
}

export default function AdminUserDialog({
  adminUser,
  open,
  onOpenChange,
  onSuccess,
}: AdminUserDialogProps) {
  const isEditing = Boolean(adminUser);
  const schema = useMemo(
    () => (isEditing ? updateAdminUserSchema : createAdminUserSchema),
    [isEditing]
  );

  const form = useForm<AdminUserFormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      fullName: "",
      email: "",
      role: "Admin",
      password: "",
    },
  });

  useEffect(() => {
    if (!open) {
      return;
    }

    form.reset({
      fullName: adminUser?.fullName ?? "",
      email: adminUser?.email ?? "",
      role: adminUser?.role ?? "Admin",
      password: "",
    });
  }, [adminUser, form, open]);

  const onSubmit = async (data: AdminUserFormData) => {
    try {
      if (adminUser) {
        await updateAdminUser(adminUser.id, {
          email: data.email,
          fullName: data.fullName,
          role: data.role,
        });
        toast.success("Admin kullanıcı başarıyla güncellendi");
      } else {
        await createAdminUser({
          email: data.email,
          fullName: data.fullName,
          role: data.role,
          password: data.password ?? "",
        });
        toast.success("Admin kullanıcı başarıyla oluşturuldu");
      }

      form.reset();
      onSuccess();
    } catch (error) {
      toast.error(isEditing ? "Admin kullanıcı güncellenemedi" : "Admin kullanıcı oluşturulamadı");
      console.error(error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>
            {isEditing ? "Admin Kullanıcıyı Düzenle" : "Yeni Admin Kullanıcı Ekle"}
          </DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="fullName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Ad Soyad</FormLabel>
                  <FormControl>
                    <Input placeholder="Ahmet Yılmaz" {...field} />
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
                    <Input type="email" placeholder="ahmet@ornek.com" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="role"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Rol</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Rol seçin" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      <SelectItem value="Admin">Admin</SelectItem>
                      <SelectItem value="SuperAdmin">Süper Admin</SelectItem>
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            {!isEditing && (
              <FormField
                control={form.control}
                name="password"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Şifre</FormLabel>
                    <FormControl>
                      <Input type="password" placeholder="******" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

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
                  ? isEditing
                    ? "Güncelleniyor..."
                    : "Oluşturuluyor..."
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
