"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Pencil, Plus, Power, Trash2 } from "lucide-react";
import {
  mutateDeleteVehicleGroup,
  mutateUpdateVehicleGroup,
  useAdminVehicleGroups,
} from "@/hooks/admin";
import type { AdminVehicleGroup } from "@/lib/api/admin/types";
import dynamic from "next/dynamic";
import { toast } from "sonner";

const VehicleGroupDialog = dynamic(() => import("@/components/admin/dialogs/VehicleGroupDialog"), {
  ssr: false,
});

export default function VehicleGroupsPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingGroup, setEditingGroup] = useState<AdminVehicleGroup | undefined>();

  const { groups, isLoading, isError, mutate } = useAdminVehicleGroups();

  const handleCreate = () => {
    setEditingGroup(undefined);
    setDialogOpen(true);
  };

  const handleEdit = (group: AdminVehicleGroup) => {
    setEditingGroup(group);
    setDialogOpen(true);
  };

  const handleSuccess = () => {
    setDialogOpen(false);
    setEditingGroup(undefined);
    mutate();
  };

  const handleDelete = async (group: AdminVehicleGroup) => {
    const groupLabel = group.nameTr ?? group.nameEn ?? group.name ?? "bu grup";
    if (!window.confirm(`${groupLabel} kaydı silinsin mi? Bağlı araç/fiyat varsa işlem reddedilir.`)) {
      return;
    }

    try {
      await mutateDeleteVehicleGroup(group.id);
      toast.success("Araç grubu silindi");
      mutate();
    } catch (error) {
      toast.error("Araç grubu silinemedi");
      console.error(error);
    }
  };

  const handleToggleActive = async (group: AdminVehicleGroup) => {
    try {
      await mutateUpdateVehicleGroup(group.id, {
        nameTr: group.nameTr ?? group.name ?? "",
        nameEn: group.nameEn ?? group.name ?? "",
        nameRu: group.nameRu ?? group.name ?? "",
        nameAr: group.nameAr ?? group.name ?? "",
        nameDe: group.nameDe ?? group.name ?? "",
        depositAmount: group.depositAmount,
        minAge: group.minAge,
        minLicenseYears: group.minLicenseYears,
        isActive: !group.isActive,
        features: group.features ?? [],
      });
      toast.success(group.isActive ? "Araç grubu pasif edildi" : "Araç grubu aktif edildi");
      mutate();
    } catch (error) {
      toast.error("Araç grubu durumu güncellenemedi");
      console.error(error);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Araç gruplarını ve özelliklerini yönetin.
        </p>
        <Button size="sm" onClick={handleCreate}>
          <Plus className="h-4 w-4 mr-1" /> Yeni Grup
        </Button>
      </div>
      {isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-48 w-full" />
          ))}
        </div>
      ) : isError ? (
        <div className="text-sm text-destructive">Veri yüklenirken hata oluştu</div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {groups.map((g) => (
            <Card key={g.id}>
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">{g.nameTr ?? g.nameEn ?? g.name}</CardTitle>
                  <div className="flex gap-1">
                    <Button size="sm" variant="ghost" onClick={() => handleEdit(g)}>
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => handleToggleActive(g)}
                      aria-label={g.isActive ? "Araç grubunu pasif et" : "Araç grubunu aktif et"}
                      title={g.isActive ? "Pasif et" : "Aktif et"}
                    >
                      <Power className="h-4 w-4" />
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => handleDelete(g)}
                      aria-label="Araç grubunu sil"
                      title="Sil"
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </div>
                <p className="text-sm text-muted-foreground">{g.nameEn}</p>
                <Badge variant={g.isActive ? "default" : "destructive"} className="w-fit">
                  {g.isActive ? "Aktif" : "Pasif"}
                </Badge>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">Depozito</span>
                  <span className="font-medium">₺{g.depositAmount?.toLocaleString("tr-TR")}</span>
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">Min. Yaş</span>
                  <span className="font-medium">{g.minAge}</span>
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">Min. Ehliyet</span>
                  <span className="font-medium">{g.minLicenseYears} yıl</span>
                </div>
                <div className="flex flex-wrap gap-1 pt-2">
                  {g.features?.map((f) => (
                    <Badge key={f} variant="secondary" className="text-xs">
                      {f}
                    </Badge>
                  )) || <span className="text-xs text-muted-foreground">Özellik yok</span>}
                </div>
              </CardContent>
            </Card>
          ))}
          {groups.length === 0 && (
            <div className="text-muted-foreground text-sm">Grup bulunamadı</div>
          )}
        </div>
      )}
      <VehicleGroupDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        group={editingGroup}
        onSuccess={handleSuccess}
      />
    </div>
  );
}
