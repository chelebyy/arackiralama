"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Pencil, Trash2, ShieldCheck, Users } from "lucide-react";
import { useAdminVehicleGroups } from "@/hooks/admin";

export default function VehicleGroupsPage() {
  const { groups, isLoading, isError } = useAdminVehicleGroups();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Araç gruplarını ve özelliklerini yönetin.
        </p>
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
                  <CardTitle className="text-base">{g.name}</CardTitle>
                  <div className="flex gap-1">
                    <Button size="sm" variant="ghost">
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button size="sm" variant="ghost" className="text-destructive">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
                <p className="text-sm text-muted-foreground">{g.description}</p>
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
    </div>
  );
}
