"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { complete3dsReturn } from "@/lib/api/payments";

export default function ThreeDsReturnPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [status, setStatus] = useState<"processing" | "error">("processing");

  useEffect(() => {
    async function handle3dsReturn() {
      const paymentIntentId = sessionStorage.getItem("pendingPaymentIntentId");
      const publicCode = sessionStorage.getItem("pendingReservationPublicCode");

      if (!paymentIntentId || !publicCode) {
        setStatus("error");
        return;
    }

      // Bank response may come as query param (GET redirect) or stored param
      const bankResponse =
        searchParams.get("bankResponse") ||
        searchParams.get("threeDSResponse") ||
        sessionStorage.getItem("pendingBankResponse") ||
        "completed";

      try {
        await complete3dsReturn(paymentIntentId, { bankResponse });
      } catch {
        // Payment may already be completed by webhook — proceed to confirmation
      }

      sessionStorage.removeItem("pendingPaymentIntentId");
      sessionStorage.removeItem("pendingReservationPublicCode");
      sessionStorage.removeItem("pendingBankResponse");

      router.replace(`/tr/booking/confirmation?code=${publicCode}`);
    }

    handle3dsReturn();
  }, [searchParams, router]);

  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="text-center">
        {status === "processing" ? (
          <>
            <div className="w-12 h-12 border-4 border-sky-600 border-t-transparent rounded-full animate-spin mx-auto mb-4" />
            <p className="text-slate-600">Processing payment...</p>
          </>
        ) : (
          <>
            <p className="text-red-600">Failed to process payment. Please try again.</p>
            <button
              onClick={() => router.push("/tr/booking/step4")}
              className="mt-4 px-4 py-2 bg-sky-700 text-white rounded-lg"
            >
              Return to Payment
            </button>
          </>
        )}
      </div>
    </div>
  );
}
