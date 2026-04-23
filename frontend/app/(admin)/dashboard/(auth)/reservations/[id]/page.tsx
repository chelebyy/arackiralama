"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import {
  ArrowLeft,
  Calendar,
  Car,
  CheckCircle,
  CreditCard,
  MapPin,
  NotebookPen,
  Timer,
  User,
  XCircle,
} from "lucide-react";
import {
  useAdminReservation,
  mutateCancelReservation,
  mutateCheckIn,
  mutateCheckOut,
} from "@/hooks/admin";
import { toast } from "sonner";

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
  const map: Record<string, string> = {
    CONFIRMED: "Onaylı",
    PENDING: "Beklemede",
    ACTIVE: "Aktif",
    COMPLETED: "Tamamlandı",
    CANCELLED: "İptal",
    EXPIRED: "Süresi Doldu",
  };
  return map[status] || status;
};

const paymentStatusLabel = (status: string) => {
  const map: Record<string, string> = {
    PENDING: "Beklemede",
    AUTHORIZED: "Yetkilendirildi",
    CAPTURED: "Tahsil Edildi",
    FAILED: "Başarısız",
    REFUNDED: "İade Edildi",
    PARTIALLY_REFUNDED: "Kısmi İade",
  };
  return map[status] || status;
};

const paymentStatusVariant = (status: string) => {
  switch (status) {
    case "CAPTURED":
      return "default";
    case "AUTHORIZED":
      return "secondary";
    case "PENDING":
      return "outline";
    case "FAILED":
      return "destructive";
    case "REFUNDED":
    case "PARTIALLY_REFUNDED":
      return "destructive";
    default:
      return "outline";
  }
};

function currency(value?: number) {
  if (value === undefined || value === null) return "—";
  return `₺${value.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export default function ReservationDetailPage() {
  const params = useParams();
  const id = typeof params.id === "string" ? params.id : null;

  const { reservation, isLoading, isError, mutate } = useAdminReservation(id);

  const handleCancel = async () => {
    if (!id) return;
    try {
      await mutateCancelReservation(id, "Admin tarafından iptal");
      toast.success("Rezervasyon iptal edildi");
      mutate();
    } catch {
      toast.error("İptal işlemi başarısız");
    }
  };

  const handleCheckIn = async () => {
    if (!id) return;
    try {
      await mutateCheckIn(id, { checkedInBy: "Admin" });
      toast.success("Check-in yapıldı");
      mutate();
    } catch {
      toast.error("Check-in işlemi başarısız");
    }
  };

  const handleCheckOut = async () => {
    if (!id) return;
    try {
      await mutateCheckOut(id, { checkedOutBy: "Admin" });
      toast.success("Check-out yapıldı");
      mutate();
    } catch {
      toast.error("Check-out işlemi başarısız");
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !reservation) {
    return (
      <div className="space-y-4">
        <Button variant="outline" size="sm" asChild>
          <Link href="/dashboard/reservations">
            <ArrowLeft className="mr-2 h-4 w-4" />
            Geri
          </Link>
        </Button>
        <div className="text-sm text-destructive">
          Rezervasyon bilgileri yüklenirken hata oluştu veya rezervasyon bulunamadı.
        </div>
      </div>
    );
  }

  const r = reservation;
  const pb = r.priceBreakdown;
  const canCancel = ["PENDING", "CONFIRMED"].includes(r.status);
  const canCheckIn = r.status === "CONFIRMED";
  const canCheckOut = r.status === "ACTIVE";

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <Button variant="outline" size="sm" asChild>
            <Link href="/dashboard/reservations">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Geri
            </Link>
          </Button>
          <h1 className="text-xl font-semibold tracking-tight">
            Rezervasyon Detayı
          </h1>
          <Badge variant={statusBadgeVariant(r.status)}>
            {statusLabel(r.status)}
          </Badge>
        </div>
        <div className="flex items-center gap-2">
          {canCancel && (
            <Button variant="destructive" size="sm" onClick={handleCancel}>
              <XCircle className="mr-2 h-4 w-4" />
              İptal Et
            </Button>
          )}
          {canCheckIn && (
            <Button variant="default" size="sm" onClick={handleCheckIn}>
              <CheckCircle className="mr-2 h-4 w-4" />
              Check-In
            </Button>
          )}
          {canCheckOut && (
            <Button variant="default" size="sm" onClick={handleCheckOut}>
              <CheckCircle className="mr-2 h-4 w-4" />
              Check-Out
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        {/* Rezervasyon Bilgileri */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <NotebookPen className="h-4 w-4 text-muted-foreground" />
              Rezervasyon Bilgileri
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Rezervasyon Kodu</span>
              <span className="font-medium">{r.reservationCode || r.publicCode}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Oluşturulma</span>
              <span>{r.createdAt ? new Date(r.createdAt).toLocaleString("tr-TR") : "—"}</span>
            </div>
            <Separator />
            <div className="flex justify-between">
              <span className="text-muted-foreground">Alış Tarihi</span>
              <span className="font-medium">{r.pickupDate} {r.pickupTime}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Alış Ofisi</span>
              <span>{r.pickupOfficeName}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">İade Tarihi</span>
              <span className="font-medium">{r.returnDate} {r.returnTime}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">İade Ofisi</span>
              <span>{r.returnOfficeName}</span>
            </div>
            <Separator />
            <div className="flex justify-between">
              <span className="text-muted-foreground">Ödeme Durumu</span>
              <Badge variant={paymentStatusVariant(r.paymentStatus)}>
                {paymentStatusLabel(r.paymentStatus)}
              </Badge>
            </div>
            {r.campaignCode && (
              <div className="flex justify-between">
                <span className="text-muted-foreground">Kampanya</span>
                <span className="font-medium">{r.campaignCode}</span>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Müşteri Bilgileri */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <User className="h-4 w-4 text-muted-foreground" />
              Müşteri Bilgileri
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Ad Soyad</span>
              <span className="font-medium">
                {r.customer?.firstName} {r.customer?.lastName}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">E-posta</span>
              <span>{r.customer?.email}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Telefon</span>
              <span>{r.customer?.phone}</span>
            </div>
            {r.customer?.nationality && (
              <div className="flex justify-between">
                <span className="text-muted-foreground">Uyruk</span>
                <span>{r.customer.nationality}</span>
              </div>
            )}
            {r.customer?.passportNumber && (
              <div className="flex justify-between">
                <span className="text-muted-foreground">Pasaport No</span>
                <span>{r.customer.passportNumber}</span>
              </div>
            )}
            <Separator />
            <div className="flex justify-between">
              <span className="text-muted-foreground">Toplam Rezervasyon</span>
              <span>{(r.customer as unknown as { reservationCount?: number })?.reservationCount ?? "—"}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Toplam Harcama</span>
              <span>
                {currency((r.customer as unknown as { totalSpent?: number })?.totalSpent)}
              </span>
            </div>
          </CardContent>
        </Card>

        {/* Sürücü Bilgileri */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <Car className="h-4 w-4 text-muted-foreground" />
              Sürücü Bilgileri
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {r.driver ? (
              <>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Ad Soyad</span>
                  <span className="font-medium">
                    {r.driver.firstName} {r.driver.lastName}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Ehliyet No</span>
                  <span>{r.driver.licenseNumber}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Ehliyet Ülkesi</span>
                  <span>{r.driver.licenseCountry}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Ehliyet Bitiş</span>
                  <span>{r.driver.licenseExpiryDate}</span>
                </div>
              </>
            ) : (
              <div className="text-muted-foreground">Sürücü bilgisi bulunmuyor</div>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {/* Araç Bilgileri */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <Car className="h-4 w-4 text-muted-foreground" />
              Araç Bilgileri
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Araç</span>
              <span className="font-medium">{r.vehicleName || r.vehicle?.name || "—"}</span>
            </div>
            {r.vehiclePlate && (
              <div className="flex justify-between">
                <span className="text-muted-foreground">Plaka</span>
                <span>{r.vehiclePlate}</span>
              </div>
            )}
            {r.assignedVehicleId && (
              <div className="flex justify-between">
                <span className="text-muted-foreground">Atanan Araç ID</span>
                <span className="font-mono text-xs">{r.assignedVehicleId}</span>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Fiyat Özeti */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <CreditCard className="h-4 w-4 text-muted-foreground" />
              Fiyat Özeti
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {pb ? (
              <>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Temel Fiyat</span>
                  <span>{currency(pb.basePrice)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Kiralama Günü</span>
                  <span>{pb.rentalDays} gün</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Ekstralar</span>
                  <span>{currency(pb.extrasTotal)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Sigorta</span>
                  <span>{currency(pb.insuranceTotal)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Ara Toplam</span>
                  <span>{currency(pb.subtotal)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">KDV (%{pb.taxRate})</span>
                  <span>{currency(pb.taxAmount)}</span>
                </div>
                {pb.discountAmount > 0 && (
                  <div className="flex justify-between text-destructive">
                    <span>İndirim</span>
                    <span>-{currency(pb.discountAmount)}</span>
                  </div>
                )}
                <Separator />
                <div className="flex justify-between text-base font-semibold">
                  <span>Toplam</span>
                  <span>{currency(pb.totalAmount)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Depozito</span>
                  <span>{currency(pb.depositAmount)}</span>
                </div>
              </>
            ) : (
              <div className="text-muted-foreground">Fiyat bilgisi bulunmuyor</div>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {/* Admin Notları */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <NotebookPen className="h-4 w-4 text-muted-foreground" />
              Admin Notları
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {r.adminNotes ? (
              <div className="rounded-md bg-muted p-3 text-sm">{r.adminNotes}</div>
            ) : (
              <div className="text-muted-foreground">Admin notu bulunmuyor</div>
            )}
            {r.cancellationReason && (
              <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
                <span className="font-medium">İptal Nedeni:</span> {r.cancellationReason}
              </div>
            )}
            {r.refundAmount !== undefined && r.refundAmount !== null && (
              <div className="rounded-md bg-muted p-3 text-sm">
                <span className="font-medium">İade Tutarı:</span> {currency(r.refundAmount)}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Zaman Çizelgesi */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <Timer className="h-4 w-4 text-muted-foreground" />
              Zaman Çizelgesi
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {r.checkedInAt ? (
              <div className="flex items-start gap-3">
                <CheckCircle className="h-4 w-4 text-green-600 mt-0.5" />
                <div>
                  <div className="font-medium">Check-In Yapıldı</div>
                  <div className="text-muted-foreground">
                    {new Date(r.checkedInAt).toLocaleString("tr-TR")}
                  </div>
                  {r.checkedInBy && (
                    <div className="text-muted-foreground text-xs">by {r.checkedInBy}</div>
                  )}
                </div>
              </div>
            ) : (
              <div className="flex items-center gap-3 text-muted-foreground">
                <CheckCircle className="h-4 w-4" />
                <span>Check-In bekleniyor</span>
              </div>
            )}
            {r.checkedOutAt ? (
              <div className="flex items-start gap-3">
                <CheckCircle className="h-4 w-4 text-green-600 mt-0.5" />
                <div>
                  <div className="font-medium">Check-Out Yapıldı</div>
                  <div className="text-muted-foreground">
                    {new Date(r.checkedOutAt).toLocaleString("tr-TR")}
                  </div>
                  {r.checkedOutBy && (
                    <div className="text-muted-foreground text-xs">by {r.checkedOutBy}</div>
                  )}
                </div>
              </div>
            ) : (
              <div className="flex items-center gap-3 text-muted-foreground">
                <CheckCircle className="h-4 w-4" />
                <span>Check-Out bekleniyor</span>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
