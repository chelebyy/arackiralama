"use client";

import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect, useMemo, useState } from "react";
import { Link } from "@/i18n/routing";
import { Check, Car, Calendar, CreditCard, Hash } from "lucide-react";
import { getReservationByPublicCode } from "@/lib/api/reservations";
import type { Reservation } from "@/lib/api/types";

export default function BookingConfirmationPage() {
  const t = useTranslations();
  const searchParams = useSearchParams();

  const code = searchParams.get("code");
  const isUnpaidRequest = searchParams.get("request") === "unpaid";
  const [reservation, setReservation] = useState<Reservation | null>(null);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);

  useEffect(() => {
    if (!code) return;

    let cancelled = false;
    setIsLoadingDetails(true);

    getReservationByPublicCode(code)
      .then((result) => {
        if (!cancelled) {
          setReservation(result);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setReservation(null);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoadingDetails(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [code]);

  const details = useMemo(() => {
    if (!reservation) return null;

    const raw = reservation as Reservation & {
      vehicleBrand?: string;
      vehicleModel?: string;
      vehicleGroupName?: string;
      pickupDateTime?: string;
      returnDateTime?: string;
      totalAmount?: number;
      depositAmount?: number;
    };

    const pickupDateTime = raw.pickupDateTime ? new Date(raw.pickupDateTime) : null;
    const returnDateTime = raw.returnDateTime ? new Date(raw.returnDateTime) : null;
    const vehicleName =
      reservation.vehicleName ||
      [raw.vehicleBrand, raw.vehicleModel].filter(Boolean).join(" ") ||
      raw.vehicleGroupName ||
      "";

    return {
      vehicle: vehicleName,
      pickup: [reservation.pickupOfficeName, reservation.pickupDate || pickupDateTime?.toLocaleDateString("tr-TR"), reservation.pickupTime || pickupDateTime?.toISOString().slice(11, 16)]
        .filter(Boolean)
        .join(" - "),
      returnDate: [reservation.returnOfficeName, reservation.returnDate || returnDateTime?.toLocaleDateString("tr-TR"), reservation.returnTime || returnDateTime?.toISOString().slice(11, 16)]
        .filter(Boolean)
        .join(" - "),
      total: new Intl.NumberFormat("tr-TR", {
        style: "currency",
        currency: reservation.priceBreakdown?.currency || "TRY",
      }).format(reservation.priceBreakdown?.totalAmount ?? raw.totalAmount ?? 0),
    };
  }, [reservation]);

  const hasDetails = !!code || !!details;

  return (
    <div className="min-h-[60vh] flex items-center justify-center px-4 sm:px-6 lg:px-8">
      <div className="w-full max-w-2xl">
        <div className="text-center mb-8">
          <div className="mx-auto flex h-20 w-20 items-center justify-center rounded-full bg-blue-100 mb-6">
            <Check className="h-10 w-10 text-blue-600" aria-hidden="true" />
          </div>
          <h1 className="text-3xl font-bold text-slate-900 sm:text-4xl">
            {isUnpaidRequest
              ? t("booking.confirmation.unpaidRequestTitle")
              : t("booking.confirmation.title")}
          </h1>
          <p className="mt-4 text-lg text-slate-600">
            {isUnpaidRequest
              ? t("booking.confirmation.unpaidRequestMessage")
              : t("booking.confirmation.message")}
          </p>
        </div>

        {hasDetails && (
          <div className="rounded-2xl bg-white shadow-sm border border-slate-200 p-6 sm:p-8 mb-8">
            <h2 className="text-lg font-semibold text-slate-900 mb-6">
              {t("booking.payment.summary.title")}
            </h2>
            <dl className="space-y-4">
              {code && (
                <div className="flex items-start justify-between gap-4">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <Hash className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("booking.confirmation.reservationNumber")}
                  </dt>
                  <dd className="text-sm font-semibold text-slate-900 text-end">{code}</dd>
                </div>
              )}
              {isLoadingDetails && (
                <div className="text-sm text-slate-500">Rezervasyon detayları yükleniyor...</div>
              )}
              {details?.vehicle && (
                <div className="flex items-start justify-between gap-4">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <Car className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.vehicle")}
                  </dt>
                  <dd className="text-sm font-semibold text-slate-900 text-end">{details.vehicle}</dd>
                </div>
              )}
              {details?.pickup && (
                <div className="flex items-start justify-between gap-4">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <Calendar className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.pickup")}
                  </dt>
                  <dd className="text-sm font-semibold text-slate-900 text-end">{details.pickup}</dd>
                </div>
              )}
              {details?.returnDate && (
                <div className="flex items-start justify-between gap-4">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <Calendar className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.return")}
                  </dt>
                  <dd className="text-sm font-semibold text-slate-900 text-end">{details.returnDate}</dd>
                </div>
              )}
              {details?.total && (
                <div className="flex items-start justify-between gap-4 pt-4 border-t border-slate-100">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <CreditCard className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.total")}
                  </dt>
                  <dd className="text-base font-bold text-blue-600 text-end">{details.total}</dd>
                </div>
              )}
            </dl>
          </div>
        )}

        {!hasDetails && (
          <div className="rounded-2xl bg-blue-50 border border-blue-100 p-6 text-center mb-8">
            <p className="text-blue-800 text-sm font-medium">
              {t("booking.confirmation.instructions")}
            </p>
          </div>
        )}

        <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link
            href="/"
            className="inline-flex items-center justify-center rounded-xl bg-blue-600 px-8 py-3.5 text-base font-semibold text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:ring-offset-2 transition-colors w-full sm:w-auto"
          >
            {t("navigation.home")}
          </Link>
        </div>
      </div>
    </div>
  );
}
