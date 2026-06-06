"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams, useParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { complete3dsReturn } from "@/lib/api/payments";

export default function ThreeDsReturnPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const params = useParams();
  const locale = params.locale as string;
  const t = useTranslations("booking");
  const [status, setStatus] = useState<"processing" | "error">("processing");

  useEffect(() => {
    if (!locale) return;

    async function handle3dsReturn() {
      const paymentIntentId = sessionStorage.getItem("pendingPaymentIntentId");
      const publicCode = sessionStorage.getItem("pendingReservationPublicCode");

      if (!paymentIntentId || !publicCode) {
        setStatus("error");
        return;
      }

      const bankResponse =
        searchParams.get("bankResponse") ||
        searchParams.get("threeDSResponse") ||
        sessionStorage.getItem("pendingBankResponse") ||
        "completed";

      try {
        await complete3dsReturn(paymentIntentId, { bankResponse });
      } catch {
        // Payment may already be completed by webhook
      }

      sessionStorage.removeItem("pendingPaymentIntentId");
      sessionStorage.removeItem("pendingReservationPublicCode");
      sessionStorage.removeItem("pendingBankResponse");

      router.replace(`/${locale}/booking/confirmation?code=${publicCode}`);
    }

    handle3dsReturn();
  }, [locale, searchParams, router]);

  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="text-center">
        {status === "processing" ? (
          <>
            <div className="w-12 h-12 border-4 border-sky-600 border-t-transparent rounded-full animate-spin mx-auto mb-4" />
            <p className="text-slate-600">{t("processingPayment")}</p>
          </>
        ) : (
          <>
            <p className="text-red-600">{t("failedToProcessPayment")}</p>
            <button
              onClick={() => router.push(`/${locale}/booking/step4`)}
              className="mt-4 px-4 py-2 bg-sky-700 text-white rounded-lg"
            >
              {t("returnToPayment")}
            </button>
          </>
        )}
      </div>
    </div>
  );
}
