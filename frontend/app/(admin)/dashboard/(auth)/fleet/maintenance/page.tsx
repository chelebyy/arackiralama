"use client";

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
import { CheckCircle2 } from "lucide-react";
import { useAdminVehicles } from "@/hooks/admin";
import { toast } from "sonner";

export default function MaintenancePage() {
  const { vehicles, isLoading, isError, mutate } = useAdminVehicles();

  const maintenanceVehicles = vehicles.filter((v) => v.nextMaintenanceDate);

  const handleComplete = (id: string) => {
    toast.success("Bakım kaydı tamamlandı (mock)");
    mutate();
  };

  const isOverdue = (dateStr: string) => {
    return new Date(dateStr) < new Date();
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Bakım Takvimi</CardTitle>
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
                  <TableHead>Araç</TableHead>
                  <TableHead>Plaka</TableHead>
                  <TableHead>Son Bakım</TableHead>
                  <TableHead>Sonraki Bakım</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead>Açıklama</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {maintenanceVehicles.map((v) => (
                  <TableRow key={v.id}>
                    <TableCell className="font-medium">{v.name}</TableCell>
                    <TableCell>{v.plate}</TableCell>
                    <TableCell>{v.lastMaintenanceDate || "—"}</TableCell>
                    <TableCell>{v.nextMaintenanceDate}</TableCell>
                    <TableCell>
                      {isOverdue(v.nextMaintenanceDate) ? (
                        <Badge variant="destructive">Gecikmiş</Badge>
                      ) : (
                        <Badge variant="default">Planlandı</Badge>
                      )}
                    </TableCell>
                    <TableCell className="max-w-[200px] truncate">
                      {v.adminNotes || "—"}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button size="sm" variant="ghost" onClick={() => handleComplete(v.id)}>
                        <CheckCircle2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
                {maintenanceVehicles.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="text-center text-muted-foreground h-24"
                    >
                      Yaklaşan bakım bulunmamaktadır
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
