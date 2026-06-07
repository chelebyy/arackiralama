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
import { Archive, Search, Pencil, Plus, Power } from "lucide-react";
import {
  mutateDeleteVehicle,
  mutateUpdateVehicleStatus,
  useAdminOffices,
  useAdminVehicleGroups,
  useAdminVehicles,
} from "@/hooks/admin";
import type { AdminVehicle } from "@/lib/api/admin/types";
import dynamic from "next/dynamic";
import { toast } from "sonner";

const VehicleDialog = dynamic(() => import("@/components/admin/dialogs/VehicleDialog"), {
  ssr: false,
});

const normalizeStatus = (status: AdminVehicle["status"]) => {
  if (status === 0 || status === "Available") return "Available";
  if (status === 1 || status === "Reserved") return "Reserved";
  if (status === 2 || status === "Rented") return "Rented";
  if (status === 3 || status === "Maintenance") return "Maintenance";
  if (status === 4 || status === "OutOfService") return "OutOfService";
  if (status === 5 || status === "Retired") return "Retired";
  return String(status);
};

const statusBadgeVariant = (status: string) => {
  switch (status) {
    case "Available":
      return "default";
    case "Maintenance":
      return "secondary";
    case "OutOfService":
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
    case "Reserved":
      return "Rezerve";
    case "Rented":
      return "Kirada";
    case "OutOfService":
      return "Servis Dışı";
    case "Retired":
      return "Arşivli";
    default:
      return status;
  }
};

export default function VehiclesPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ACTIVE");
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

  const handleArchive = async (vehicle: AdminVehicle) => {
    const vehicleLabel = vehicle.plate ?? vehicle.name ?? "bu araç";
    if (!window.confirm(`${vehicleLabel} arşivlensin mi? Rezervasyon geçmişi yoksa kayıt tamamen silinir; geçmişi varsa arşivlenir.`)) {
      return;
    }

    try {
      const result = await mutateDeleteVehicle(vehicle.id);
      toast.success(result?.outcome === "Archived" ? "Araç arşivlendi" : "Araç silindi");
      mutate();
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : "Araç arşivlenemedi");
      console.error(error);
    }
  };

  const handleToggleAvailability = async (vehicle: AdminVehicle) => {
    const normalizedStatus = normalizeStatus(vehicle.status);
    if (normalizedStatus === "Retired") {
      toast.error("Arşivli araç tekrar aktif edilemez. Yeni araç kaydı oluşturun.");
      return;
    }
    const nextStatus = normalizedStatus === "OutOfService" ? "Available" : "OutOfService";

    try {
      await mutateUpdateVehicleStatus(vehicle.id, nextStatus);
      toast.success(nextStatus === "Available" ? "Araç aktif edildi" : "Araç pasif edildi");
      mutate();
    } catch (error) {
      toast.error("Araç durumu güncellenemedi");
      console.error(error);
    }
  };

  const filtered = vehicles.filter((v) => {
    const q = search.toLowerCase();
    const vehicleName = `${v.brand ?? ""} ${v.model ?? ""} ${v.name ?? ""}`.trim();
    const matchSearch =
      !q ||
      v.plate?.toLowerCase().includes(q) ||
      vehicleName.toLowerCase().includes(q);
    const normalizedStatus = normalizeStatus(v.status);
    const matchStatus =
      statusFilter === "ACTIVE"
        ? normalizedStatus !== "Retired"
        : statusFilter === "ALL" || normalizedStatus === statusFilter;
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
                <SelectItem value="ACTIVE">Aktif Filo</SelectItem>
                <SelectItem value="ALL">Tüm Kayıtlar</SelectItem>
                <SelectItem value="Available">Müsait</SelectItem>
                <SelectItem value="Maintenance">Bakımda</SelectItem>
                <SelectItem value="OutOfService">Servis Dışı</SelectItem>
                <SelectItem value="Retired">Arşivli</SelectItem>
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
                  <TableHead className="text-right">Fotoğraf</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((v) => {
                  const group = groups.find((item) => item.id === v.groupId);
                  const groupName =
                    group?.nameTr ??
                    group?.nameEn ??
                    group?.name ??
                    v.groupName ??
                    v.group?.name ??
                    "—";
                  const officeName =
                    offices.find((office) => office.id === v.officeId)?.name ??
                    v.officeName ??
                    v.office?.name ??
                    "—";

                  return (
                  <TableRow key={v.id}>
                    <TableCell className="font-medium">{v.plate}</TableCell>
                    <TableCell>
                      {v.brand && v.model
                        ? `${v.brand} ${v.model}${v.year ? ` (${v.year})` : ""}${v.color ? ` · ${v.color}` : ""}`
                        : v.name ?? "—"}
                    </TableCell>
                    <TableCell>{groupName}</TableCell>
                    <TableCell>{officeName}</TableCell>
                    <TableCell>
                      <Badge variant={statusBadgeVariant(normalizeStatus(v.status))}>
                        {statusLabel(normalizeStatus(v.status))}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">{v.photoUrl ? "Var" : "Yok"}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="ghost" onClick={() => handleEdit(v)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => handleToggleAvailability(v)}
                          aria-label={normalizeStatus(v.status) === "OutOfService" ? "Aracı aktif et" : "Aracı pasif et"}
                          title={normalizeStatus(v.status) === "OutOfService" ? "Aktif et" : "Pasif et"}
                        >
                          <Power className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => handleArchive(v)}
                          aria-label="Aracı arşivle"
                          title="Arşivle"
                        >
                          <Archive className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                  );
                })}
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
