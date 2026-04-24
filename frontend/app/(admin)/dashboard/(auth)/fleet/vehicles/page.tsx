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
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Search, Eye, Pencil, Wrench, Plus } from "lucide-react";
import { useAdminVehicles, useAdminOffices, useAdminVehicleGroups } from "@/hooks/admin";
import type { AdminVehicle } from "@/lib/api/admin/types";
import dynamic from "next/dynamic";

const VehicleDialog = dynamic(() => import("@/components/admin/dialogs/VehicleDialog"), {
  ssr: false,
});

const statusBadgeVariant = (status: string) => {
  switch (status) {
    case "Available":
      return "default";
    case "Maintenance":
      return "secondary";
    case "Retired":
      return "outline";
    default:
      return "outline";
  }
};

const statusLabel = (status: string) => {
  switch (status) {
    case "Available":
      return "Müsait";
    case "Maintenance":
      return "Bakımda";
    case "Retired":
      return "Emekli";
    default:
      return status;
  }
};

export default function VehiclesPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [officeFilter, setOfficeFilter] = useState("ALL");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingVehicle, setEditingVehicle] = useState<AdminVehicle | undefined>();

  const { vehicles, isLoading, isError, mutate } = useAdminVehicles();
  const { offices } = useAdminOffices();
  const { groups } = useAdminVehicleGroups();

  const handleEdit = (vehicle: AdminVehicle) => {
    setEditingVehicle(vehicle);
    setDialogOpen(true);
  };

  const handleCreate = () => {
    setEditingVehicle(undefined);
    setDialogOpen(true);
  };

  const handleSuccess = () => {
    setDialogOpen(false);
    setEditingVehicle(undefined);
    mutate();
  };

  const filtered = vehicles.filter((v) => {
    const q = search.toLowerCase();
    const matchSearch =
      !q ||
      v.plate?.toLowerCase().includes(q) ||
      v.name?.toLowerCase().includes(q);
    const matchStatus = statusFilter === "ALL" || v.status === statusFilter;
    const matchOffice = officeFilter === "ALL" || v.officeId === officeFilter;
    return matchSearch && matchStatus && matchOffice;
  });

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <CardTitle className="text-base">Araç Listesi</CardTitle>
          <Button size="sm" onClick={handleCreate}>
            <Plus className="h-4 w-4 mr-1" /> Yeni Araç
          </Button>
        </div>
        <div className="flex items-center gap-2 flex-wrap mt-4">
            <div className="relative">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Plaka veya araç adı..."
                className="pl-8 w-[200px]"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger className="w-[140px]">
                <SelectValue placeholder="Durum" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ALL">Tümü</SelectItem>
                <SelectItem value="Available">Müsait</SelectItem>
                <SelectItem value="Maintenance">Bakımda</SelectItem>
                <SelectItem value="Retired">Emekli</SelectItem>
              </SelectContent>
            </Select>
            <Select value={officeFilter} onValueChange={setOfficeFilter}>
              <SelectTrigger className="w-[160px]">
                <SelectValue placeholder="Ofis" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ALL">Tüm Ofisler</SelectItem>
                {offices.map((o) => (
                  <SelectItem key={o.id} value={o.id}>
                    {o.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
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
                  <TableHead>Plaka</TableHead>
                  <TableHead>Araç</TableHead>
                  <TableHead>Grup</TableHead>
                  <TableHead>Ofis</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead className="text-right">Kilometre</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((v) => (
                  <TableRow key={v.id}>
                    <TableCell className="font-medium">{v.plate}</TableCell>
                    <TableCell>{v.name}</TableCell>
                    <TableCell>{v.groupName || v.group?.name || "—"}</TableCell>
                    <TableCell>{v.officeName || v.office?.name || "—"}</TableCell>
                    <TableCell>
                      <Badge variant={statusBadgeVariant(v.status)}>
                        {statusLabel(v.status)}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      {v.mileage?.toLocaleString("tr-TR")} km
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="ghost" onClick={() => handleEdit(v)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {filtered.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="text-center text-muted-foreground h-24"
                    >
                      Araç bulunamadı
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
      <VehicleDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        vehicle={editingVehicle}
        onSuccess={handleSuccess}
        offices={offices}
        groups={groups}
      />
    </Card>
  );
}
