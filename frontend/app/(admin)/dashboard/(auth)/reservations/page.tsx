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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Search, Eye, XCircle, ChevronLeft, ChevronRight } from "lucide-react";
import { useAdminReservations, mutateCancelReservation } from "@/hooks/admin";
import { toast } from "sonner";

const statusOptions = [
  { value: "ALL", label: "Tümü" },
  { value: "CONFIRMED", label: "Onaylı" },
  { value: "PENDING", label: "Beklemede" },
  { value: "ACTIVE", label: "Aktif" },
  { value: "COMPLETED", label: "Tamamlandı" },
  { value: "CANCELLED", label: "İptal" },
  { value: "EXPIRED", label: "Süresi Doldu" },
];

const statusBadgeVariant = (status: string) => {
  switch (status) {
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
  const opt = statusOptions.find((o) => o.value === status);
  return opt?.label || status;
};

export default function ReservationsPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [page, setPage] = useState(1);

  const params: Record<string, unknown> = { page, pageSize: 10 };
  if (statusFilter !== "ALL") params.status = statusFilter;
  if (search.trim()) params.search = search.trim();

  const { reservations, pagination, isLoading, isError, mutate } =
    useAdminReservations(params);

  const handleCancel = async (id: string) => {
    try {
      await mutateCancelReservation(id, "Admin tarafından iptal");
      toast.success("Rezervasyon iptal edildi");
      mutate();
    } catch {
      toast.error("İptal işlemi başarısız");
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
                          {["PENDING", "CONFIRMED"].includes(r.status) && (
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
    </Card>
  );
}
