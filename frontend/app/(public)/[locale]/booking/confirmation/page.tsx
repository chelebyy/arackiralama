"use client";

import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";
import { Check, Car, Calendar, CreditCard, Hash } from "lucide-react";

export default function BookingConfirmationPage() {
  const t = useTranslations();
  const searchParams = useSearchParams();

  const code = searchParams.get("code");
  const vehicle = searchParams.get("vehicle");
  const pickup = searchParams.get("pickup");
  const returnDate = searchParams.get("return");
  const total = searchParams.get("total");

  const hasDetails = code || vehicle || pickup || returnDate || total;

  return (
    <div className="min-h-[60vh] flex items-center justify-center px-4 sm:px-6 lg:px-8">
      <div className="w-full max-w-2xl">
        <div className="text-center mb-8">
          <div className="mx-auto flex h-20 w-20 items-center justify-center rounded-full bg-blue-100 mb-6">
            <Check className="h-10 w-10 text-blue-600" aria-hidden="true" />
          </div>
          <h1 className="text-3xl font-bold text-slate-900 sm:text-4xl">
            {t("booking.confirmation.title")}
          </h1>
          <p className="mt-4 text-lg text-slate-600">
            {t("booking.confirmation.message")}
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
              {vehicle && (
                <div className="flex items-start justify-between gap-4">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <Car className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.vehicle")}
                  </dt>
                  <dd className="text-sm font-semibold text-slate-900 text-end">{vehicle}</dd>
                </div>
              )}
              {pickup && (
                <div className="flex items-start justify-between gap-4">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <Calendar className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.pickup")}
                  </dt>
                  <dd className="text-sm font-semibold text-slate-900 text-end">{pickup}</dd>
                </div>
              )}
              {returnDate && (
                <div className="flex items-start justify-between gap-4">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <Calendar className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.return")}
                  </dt>
                  <dd className="text-sm font-semibold text-slate-900 text-end">{returnDate}</dd>
                </div>
              )}
              {total && (
                <div className="flex items-start justify-between gap-4 pt-4 border-t border-slate-100">
                  <dt className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <CreditCard className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    {t("trackReservation.details.total")}
                  </dt>
                  <dd className="text-base font-bold text-blue-600 text-end">{total}</dd>
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
