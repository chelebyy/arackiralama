"use client";

import { useState } from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Plus, Pencil, Power } from "lucide-react";
import { useAdminUsers, mutateUpdateAdminUserRole, mutateUpdateAdminUserStatus } from "@/hooks/admin";
import type { AdminUserRole } from "@/lib/api/admin";
import { toast } from "sonner";
import dynamic from "next/dynamic";

const AdminUserDialog = dynamic(() => import("@/components/admin/dialogs/AdminUserDialog"), {
  ssr: false,
});

export default function AdminUsersPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const { users, isLoading, isError, mutate } = useAdminUsers();

  const handleCreate = () => {
    setDialogOpen(true);
  };

  const handleSuccess = () => {
    setDialogOpen(false);
    mutate();
  };

  const toggleRole = async (id: string, currentRole: string) => {
    const newRole: AdminUserRole = currentRole === "SuperAdmin" ? "Admin" : "SuperAdmin";
    try {
      await mutateUpdateAdminUserRole(id, newRole);
      toast.success("Rol güncellendi");
      mutate();
    } catch {
      toast.error("Rol güncellenemedi");
    }
  };

  const toggleStatus = async (id: string, current: boolean) => {
    try {
      await mutateUpdateAdminUserStatus(id, !current);
      toast.success("Durum güncellendi");
      mutate();
    } catch {
      toast.error("Durum güncellenemedi");
    }
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Admin Kullanıcılar</CardTitle>
          <Button size="sm" onClick={handleCreate}>
            <Plus className="h-4 w-4 mr-1" /> Yeni Admin
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : isError ? (
          <div className="text-sm text-destructive">Veri yüklenirken hata oluştu</div>
        ) : (
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ad</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Rol</TableHead>
                  <TableHead>Son Giriş</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {users.map((u) => (
                  <TableRow key={u.id}>
                    <TableCell className="font-medium">{u.fullName}</TableCell>
                    <TableCell>{u.email}</TableCell>
                    <TableCell>
                      <Badge
                        variant={u.role === "SuperAdmin" ? "default" : "secondary"}
                      >
                        {u.role === "SuperAdmin" ? "Süper Admin" : "Admin"}
                      </Badge>
                    </TableCell>
                    <TableCell>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString("tr-TR") : "—"}</TableCell>
                    <TableCell>
                      <Badge variant={u.isActive ? "default" : "destructive"}>
                        {u.isActive ? "Aktif" : "Pasif"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => toggleRole(u.id, u.role)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => toggleStatus(u.id, u.isActive)}
                        >
                          <Power className="h-4 w-4" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {users.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="text-center text-muted-foreground h-24"
                    >
                      Admin kullanıcı bulunamadı
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
      <AdminUserDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onSuccess={handleSuccess}
      />
    </Card>
  );
}
