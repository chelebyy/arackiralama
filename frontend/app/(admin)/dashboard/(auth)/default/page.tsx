'use client';

import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from "recharts";
import {
  CalendarCheck,
  Car,
  ArrowRight,
  Wrench,
  Tag,
  TrendingUp,
} from "lucide-react";
import Link from "next/link";
import { useAdminReservations } from "@/hooks/admin";
import { useAdminVehicles } from "@/hooks/admin";

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
  switch (status) {
    case "CONFIRMED":
      return "Onaylı";
    case "PENDING":
      return "Beklemede";
    case "ACTIVE":
      return "Aktif";
    case "COMPLETED":
      return "Tamamlandı";
    case "CANCELLED":
      return "İptal";
    case "EXPIRED":
      return "Süresi Doldu";
    default:
      return status;
  }
};

export default function DashboardPage() {
  const { reservations, isLoading: isRecentReservationsLoading } = useAdminReservations({ pageSize: 5 });
  const { pagination: reservationPagination, isLoading: isReservationStatsLoading } = useAdminReservations({ pageSize: 1 });
  const { pagination: activeReservationPagination, isLoading: isActiveReservationsLoading } = useAdminReservations({ pageSize: 1, status: "ACTIVE" });
  const { vehicles, pagination: vehiclePagination, isLoading: isVehiclesLoading } = useAdminVehicles();

  const availableCount = vehicles.filter((v) => v.status === "Available").length;
  const maintenanceCount = vehicles.filter((v) => v.status === "Maintenance").length;
  const retiredCount = vehicles.filter((v) => v.status === "Retired").length;
  const totalReservations = reservationPagination?.totalCount ?? 0;
  const activeReservations = activeReservationPagination?.totalCount ?? 0;
  const totalVehicles = vehiclePagination?.totalCount ?? vehicles.length;
  const isStatsLoading =
    isReservationStatsLoading || isActiveReservationsLoading || isVehiclesLoading;

  const stats = [
    {
      label: "Toplam Rezervasyon",
      value: totalReservations,
      icon: CalendarCheck,
      color: "text-blue-600",
      bg: "bg-blue-50",
    },
    {
      label: "Aktif Rezervasyon",
      value: activeReservations,
      icon: TrendingUp,
      color: "text-emerald-600",
      bg: "bg-emerald-50",
    },
    {
      label: "Müsait Araç",
      value: availableCount,
      icon: Car,
      color: "text-amber-600",
      bg: "bg-amber-50",
    },
    {
      label: "Toplam Araç",
      value: totalVehicles,
      icon: Wrench,
      color: "text-violet-600",
      bg: "bg-violet-50",
    },
  ];

  const dynamicVehicleData = [
    { name: "Müsait", count: availableCount },
    { name: "Bakımda", count: maintenanceCount },
    { name: "Emekli", count: retiredCount },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Dashboard</h1>
      </div>

      {/* Stats */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((s) => (
          <Card key={s.label}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                {s.label}
              </CardTitle>
              <div className={`rounded-md p-2 ${s.bg}`}>
                <s.icon className={`h-4 w-4 ${s.color}`} />
              </div>
            </CardHeader>
            <CardContent>
              {isStatsLoading ? (
                <div className="h-8 w-16 animate-pulse rounded-md bg-muted" />
              ) : (
                <div className="text-2xl font-bold">{s.value}</div>
              )}
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Quick Actions */}
      <div className="grid gap-4 sm:grid-cols-3">
        <Button asChild className="h-auto py-4 justify-start gap-3">
          <Link href="/dashboard/reservations">
            <CalendarCheck className="h-5 w-5" />
            <span>Yeni Rezervasyon</span>
            <ArrowRight className="ml-auto h-4 w-4" />
          </Link>
        </Button>
        <Button asChild variant="outline" className="h-auto py-4 justify-start gap-3">
          <Link href="/dashboard/fleet/vehicles">
            <Car className="h-5 w-5" />
            <span>Araç Ekle</span>
            <ArrowRight className="ml-auto h-4 w-4" />
          </Link>
        </Button>
        <Button asChild variant="outline" className="h-auto py-4 justify-start gap-3">
          <Link href="/dashboard/pricing/campaigns">
            <Tag className="h-5 w-5" />
            <span>Kampanya Oluştur</span>
            <ArrowRight className="ml-auto h-4 w-4" />
          </Link>
        </Button>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Recent Reservations */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Son Rezervasyonlar</CardTitle>
          </CardHeader>
          <CardContent>
            {isRecentReservationsLoading ? (
              <div className="text-sm text-muted-foreground">Yükleniyor...</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Kod</TableHead>
                    <TableHead>Müşteri</TableHead>
                    <TableHead>Araç</TableHead>
                    <TableHead>Durum</TableHead>
                    <TableHead className="text-right">Tutar</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {reservations.slice(0, 5).map((r) => (
                    <TableRow key={r.id}>
                      <TableCell className="font-medium">{r.reservationCode}</TableCell>
                      <TableCell>{r.customerName || r.customer?.name || "—"}</TableCell>
                      <TableCell>{r.vehicleName || r.vehicle?.name || "—"}</TableCell>
                      <TableCell>
                        <Badge variant={statusBadgeVariant(r.status)}>
                          {statusLabel(r.status)}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        ₺{r.totalPrice?.toLocaleString("tr-TR")}
                      </TableCell>
                    </TableRow>
                  ))}
                  {reservations.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={5} className="text-center text-muted-foreground">
                        Rezervasyon bulunamadı
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        {/* Vehicle Status */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Araç Durumu Özeti</CardTitle>
          </CardHeader>
          <CardContent>
            {isVehiclesLoading ? (
              <div className="h-[240px] animate-pulse rounded-md bg-muted" />
            ) : (
              <div className="h-[240px]">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={dynamicVehicleData}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} />
                    <XAxis dataKey="name" tickLine={false} axisLine={false} />
                    <YAxis tickLine={false} axisLine={false} allowDecimals={false} />
                    <Tooltip
                      formatter={(value: number) => [`${value} araç`, ""]}
                      cursor={{ fill: "rgba(0,0,0,0.04)" }}
                    />
                    <Bar dataKey="count" radius={[4, 4, 0, 0]} fill="#3b82f6" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
            <div className="mt-4 flex items-center justify-around text-sm">
              <div className="flex items-center gap-2">
                <span className="inline-block h-3 w-3 rounded-full bg-emerald-500" />
                <span>Müsait ({availableCount})</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="inline-block h-3 w-3 rounded-full bg-amber-500" />
                <span>Bakımda ({maintenanceCount})</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="inline-block h-3 w-3 rounded-full bg-gray-400" />
                <span>Emekli ({retiredCount})</span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
