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
import { Pencil, Plane, Hotel, Plus } from "lucide-react";
import { useAdminOffices } from "@/hooks/admin";
import type { AdminOffice } from "@/lib/api/admin/types";
import dynamic from "next/dynamic";

const OfficeDialog = dynamic(() => import("@/components/admin/dialogs/OfficeDialog"), {
  ssr: false,
});

export default function OfficesPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingOffice, setEditingOffice] = useState<AdminOffice | undefined>();
  
  const { offices, isLoading, isError, mutate } = useAdminOffices();

  const handleEdit = (office: AdminOffice) => {
    setEditingOffice(office);
    setDialogOpen(true);
  };

  const handleCreate = () => {
    setEditingOffice(undefined);
    setDialogOpen(true);
  };

  const handleSuccess = () => {
    setDialogOpen(false);
    setEditingOffice(undefined);
    mutate();
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Ofis Listesi</CardTitle>
          <Button size="sm" onClick={handleCreate}>
            <Plus className="h-4 w-4 mr-1" /> Yeni Ofis
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
                  <TableHead>Kod</TableHead>
                  <TableHead>Ad</TableHead>
                  <TableHead>Şehir</TableHead>
                  <TableHead>Telefon</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Tip</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {offices.map((o) => (
                  <TableRow key={o.id}>
                    <TableCell className="font-medium">{o.code}</TableCell>
                    <TableCell>{o.name}</TableCell>
                    <TableCell>{o.city}</TableCell>
                    <TableCell>{o.phone}</TableCell>
                    <TableCell>{o.email}</TableCell>
                    <TableCell>
                      {o.type === "airport" && (
                        <Badge variant="secondary" className="gap-1">
                          <Plane className="h-3 w-3" /> Havalimanı
                        </Badge>
                      )}
                      {o.type === "hotel" && (
                        <Badge variant="secondary" className="gap-1">
                          <Hotel className="h-3 w-3" /> Otel
                        </Badge>
                      )}
                      {o.type === "office" && <Badge variant="outline">Ofis</Badge>}
                    </TableCell>
                    <TableCell>
                      <Badge variant={o.isActive ? "default" : "destructive"}>
                        {o.isActive ? "Aktif" : "Pasif"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button size="sm" variant="ghost" onClick={() => handleEdit(o)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
                {offices.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={8}
                      className="text-center text-muted-foreground h-24"
                    >
                      Ofis bulunamadı
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
      <OfficeDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        office={editingOffice}
        onSuccess={handleSuccess}
      />
    </Card>
  );
}
