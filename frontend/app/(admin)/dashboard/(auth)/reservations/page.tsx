"use client";

import { useState } from "react";
import Link from "next/link";
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
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Search, Eye, XCircle, ChevronLeft, ChevronRight, PlusCircle } from "lucide-react";
import {
  useAdminReservations,
  useAdminVehicles,
  useAdminOffices,
  mutateCancelReservation,
  mutateCreateManualReservation,
} from "@/hooks/admin";
import type { AdminManualReservationData } from "@/lib/api/admin";
import { toast } from "sonner";

const statusOptions = [
  { value: "ALL", label: "Tümü" },
  { value: "UnpaidRequest", label: "Talep Alındı" },
  { value: "CONFIRMED", label: "Onaylı" },
  { value: "PENDING", label: "Beklemede" },
  { value: "ACTIVE", label: "Aktif" },
  { value: "COMPLETED", label: "Tamamlandı" },
  { value: "CANCELLED", label: "İptal" },
  { value: "EXPIRED", label: "Süresi Doldu" },
];

const statusBadgeVariant = (status: string) => {
  switch (normalizeStatus(status)) {
    case "UNPAID_REQUEST":
      return "secondary";
    case "CONFIRMED":
      return "default";
    case "PENDING":
      return "secondary";
    case "ACTIVE":
      return "default";
    case "COMPLETED":
      return "outline";
    case "CANCELLED":
      return "destructive";
    case "EXPIRED":
      return "destructive";
    default:
      return "outline";
  }
};

const statusLabel = (status: string) => {
  const opt = statusOptions.find((o) => normalizeStatus(o.value) === normalizeStatus(status));
  return opt?.label || status;
};

function normalizeStatus(status: string) {
  return status.replace(/([a-z])([A-Z])/g, "$1_$2").toUpperCase();
}

const emptyManualReservationForm: AdminManualReservationData = {
  vehicleId: "",
  pickupOfficeId: "",
  returnOfficeId: "",
  pickupDateTimeUtc: "",
  returnDateTimeUtc: "",
  customerFirstName: "",
  customerLastName: "",
  customerPhone: "",
  customerEmail: "",
  notes: "",
  totalAmount: undefined,
};

export default function ReservationsPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [page, setPage] = useState(1);
  const [manualDialogOpen, setManualDialogOpen] = useState(false);
  const [manualForm, setManualForm] = useState<AdminManualReservationData>(
    emptyManualReservationForm,
  );
  const [isCreatingManual, setIsCreatingManual] = useState(false);

  const params: Record<string, unknown> = { page, pageSize: 10 };
  if (statusFilter !== "ALL") params.status = statusFilter;
  if (search.trim()) params.search = search.trim();

  const { reservations, pagination, isLoading, isError, mutate } =
    useAdminReservations(params);
  const { vehicles } = useAdminVehicles({ page: 1, pageSize: 100 });
  const { offices } = useAdminOffices();

  const handleCancel = async (id: string) => {
    try {
      await mutateCancelReservation(id, "Admin tarafından iptal");
      toast.success("Rezervasyon iptal edildi");
      mutate();
    } catch {
      toast.error("İptal işlemi başarısız");
    }
  };

  const updateManualForm = (
    field: keyof AdminManualReservationData,
    value: string,
  ) => {
    setManualForm((current) => ({
      ...current,
      [field]: field === "totalAmount" ? (value ? Number(value) : undefined) : value,
    }));
  };

  const handleCreateManualReservation = async () => {
    setIsCreatingManual(true);
    try {
      await mutateCreateManualReservation({
        ...manualForm,
        customerEmail: manualForm.customerEmail || undefined,
        notes: manualForm.notes || undefined,
        totalAmount: manualForm.totalAmount || undefined,
      });
      toast.success("Manuel rezervasyon oluşturuldu");
      setManualDialogOpen(false);
      setManualForm(emptyManualReservationForm);
      mutate();
    } catch {
      toast.error("Manuel rezervasyon oluşturulamadı");
    } finally {
      setIsCreatingManual(false);
    }
  };

  const filtered = reservations.filter((r) => {
    const q = search.toLowerCase();
    if (!q) return true;
    const name = (r.customerName || r.customer?.name || "").toLowerCase();
    const code = (r.reservationCode || "").toLowerCase();
    const vehicle = (r.vehicleName || r.vehicle?.name || "").toLowerCase();
    return name.includes(q) || code.includes(q) || vehicle.includes(q);
  });

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <CardTitle className="text-base">Rezervasyon Listesi</CardTitle>
          <div className="flex items-center gap-2">
            <Button size="sm" onClick={() => setManualDialogOpen(true)}>
              <PlusCircle className="mr-2 h-4 w-4" />
              Manuel Rezervasyon
            </Button>
            <div className="relative">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Ara..."
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
                {statusOptions.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
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
          <>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Kod</TableHead>
                    <TableHead>Müşteri</TableHead>
                    <TableHead>Araç</TableHead>
                    <TableHead>Alış Tarihi</TableHead>
                    <TableHead>İade Tarihi</TableHead>
                    <TableHead>Durum</TableHead>
                    <TableHead className="text-right">Tutar</TableHead>
                    <TableHead className="text-right">İşlemler</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filtered.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell className="font-medium">{r.reservationCode}</TableCell>
                      <TableCell>{r.customerName || r.customer?.name || "—"}</TableCell>
                      <TableCell>{r.vehicleName || r.vehicle?.name || "—"}</TableCell>
                    <TableCell>{r.pickupDate}</TableCell>
                    <TableCell>{r.returnDate}</TableCell>
                      <TableCell>
                        <Badge variant={statusBadgeVariant(r.status)}>
                          {statusLabel(r.status)}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        ₺{r.totalPrice?.toLocaleString("tr-TR")}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-2">
                          <Button size="sm" variant="ghost" asChild>
                            <Link href={`/dashboard/reservations/${r.id}`}>
                              <Eye className="h-4 w-4" />
                            </Link>
                          </Button>
                          {["PENDING", "CONFIRMED", "UNPAID_REQUEST"].includes(normalizeStatus(r.status)) && (
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-destructive"
                              onClick={() => handleCancel(r.id)}
                            >
                              <XCircle className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                  {filtered.length === 0 && (
                    <TableRow>
                      <TableCell
                        colSpan={8}
                        className="text-center text-muted-foreground h-24"
                      >
                        Rezervasyon bulunamadı
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </div>
            {pagination && pagination.totalPages > 1 && (
              <div className="flex items-center justify-end gap-2 mt-4">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page <= 1}
                >
                  <ChevronLeft className="h-4 w-4" />
                </Button>
                <span className="text-sm text-muted-foreground">
                  {page} / {pagination.totalPages}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setPage((p) => Math.min(pagination.totalPages, p + 1))
                  }
                  disabled={page >= pagination.totalPages}
                >
                  <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            )}
          </>
        )}
      </CardContent>
      <Dialog open={manualDialogOpen} onOpenChange={setManualDialogOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Manuel Rezervasyon</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 py-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="manualVehicleId">Fiziksel Araç</Label>
              <select
                id="manualVehicleId"
                className="border-input h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]"
                value={manualForm.vehicleId}
                onChange={(event) => updateManualForm("vehicleId", event.target.value)}
              >
                <option value="">Araç seçin</option>
                {vehicles.map((vehicle) => (
                  <option key={vehicle.id} value={vehicle.id}>
                    {vehicle.plate} - {vehicle.name || `${vehicle.brand ?? ""} ${vehicle.model ?? ""}`.trim()}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualPickupOfficeId">Alış Ofisi</Label>
              <select
                id="manualPickupOfficeId"
                className="border-input h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]"
                value={manualForm.pickupOfficeId}
                onChange={(event) => updateManualForm("pickupOfficeId", event.target.value)}
              >
                <option value="">Alış ofisi seçin</option>
                {offices.map((office) => (
                  <option key={office.id} value={office.id}>
                    {office.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualReturnOfficeId">İade Ofisi</Label>
              <select
                id="manualReturnOfficeId"
                className="border-input h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]"
                value={manualForm.returnOfficeId}
                onChange={(event) => updateManualForm("returnOfficeId", event.target.value)}
              >
                <option value="">İade ofisi seçin</option>
                {offices.map((office) => (
                  <option key={office.id} value={office.id}>
                    {office.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualPickupDateTimeUtc">Alış Tarihi UTC</Label>
              <Input
                id="manualPickupDateTimeUtc"
                type="datetime-local"
                value={manualForm.pickupDateTimeUtc}
                onChange={(event) =>
                  updateManualForm("pickupDateTimeUtc", event.target.value)
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualReturnDateTimeUtc">İade Tarihi UTC</Label>
              <Input
                id="manualReturnDateTimeUtc"
                type="datetime-local"
                value={manualForm.returnDateTimeUtc}
                onChange={(event) =>
                  updateManualForm("returnDateTimeUtc", event.target.value)
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualFirstName">Müşteri Adı</Label>
              <Input
                id="manualFirstName"
                value={manualForm.customerFirstName}
                onChange={(event) =>
                  updateManualForm("customerFirstName", event.target.value)
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualLastName">Müşteri Soyadı</Label>
              <Input
                id="manualLastName"
                value={manualForm.customerLastName}
                onChange={(event) =>
                  updateManualForm("customerLastName", event.target.value)
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualPhone">Telefon</Label>
              <Input
                id="manualPhone"
                value={manualForm.customerPhone}
                onChange={(event) => updateManualForm("customerPhone", event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualEmail">E-posta (opsiyonel)</Label>
              <Input
                id="manualEmail"
                type="email"
                value={manualForm.customerEmail}
                onChange={(event) => updateManualForm("customerEmail", event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="manualTotalAmount">Toplam Tutar (opsiyonel)</Label>
              <Input
                id="manualTotalAmount"
                type="number"
                min="0"
                value={manualForm.totalAmount ?? ""}
                onChange={(event) => updateManualForm("totalAmount", event.target.value)}
              />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="manualNotes">Notlar (opsiyonel)</Label>
              <Textarea
                id="manualNotes"
                value={manualForm.notes}
                onChange={(event) => updateManualForm("notes", event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setManualDialogOpen(false)}
              disabled={isCreatingManual}
            >
              Vazgeç
            </Button>
            <Button onClick={handleCreateManualReservation} disabled={isCreatingManual}>
              {isCreatingManual ? "Oluşturuluyor..." : "Oluştur"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
