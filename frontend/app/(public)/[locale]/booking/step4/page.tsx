"use client";

import { useParams, useSearchParams, useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
  CreditCard,
  Shield,
  Lock,
  Tag,
  ArrowRight,
  ArrowLeft,
  Check,
  AlertCircle,
  Banknote,
} from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { PriceBreakdown } from "@/components/public/PriceBreakdown";
import { differenceInCalendarDays } from "date-fns";
import { useBookingActions, useBookingState } from "@/hooks/useBooking";
import { useValidateCampaign } from "@/hooks/usePricing";
import { ApiError } from "@/lib/api/client";
import { createReservationQuote, getPublicReservationExtraOptions } from "@/lib/api/reservationExtras";
import { usePlaceHold } from "@/hooks/useReservations";
import { createReservation, createUnpaidReservationRequest } from "@/lib/api/reservations";
import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";
import type { PublicPaymentMethods } from "@/lib/api/publicSiteSettings";
import { createPaymentIntent } from "@/lib/api/payments";
import type { PaymentIntentResponse } from "@/lib/api/payments";
import type { CreateReservationData, ReservationQuote, SelectedBookingExtra } from "@/lib/api/types";
import { useTranslations } from "next-intl";

type PaymentMethodId = "credit_card" | "debit_card" | "unpaid";

type PaymentMethodOption = {
  id: PaymentMethodId;
  name: string;
  description: string;
  icon: ReactNode;
};

const noPaymentMethods: PublicPaymentMethods = {
  creditCardEnabled: false,
  debitCardEnabled: false,
  unpaidRequestEnabled: false,
  paypalEnabled: false,
  anyEnabled: false,
};


export default function BookingStep4Page() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = params.locale as string;
  const t = useTranslations("booking");
  const booking = useBookingState();
  const { updateExtras } = useBookingActions();
  const { validate: validateCampaignCode, isValidating } = useValidateCampaign();
  const { placeHold } = usePlaceHold();
  const [appliedCampaign, setAppliedCampaign] = useState<{ code: string } | null>(null);
  const [campaignInput, setCampaignInput] = useState("");
  const [paymentMethodsAvailability, setPaymentMethodsAvailability] = useState(noPaymentMethods);
  const [quote, setQuote] = useState<ReservationQuote | null>(null);
  const [isQuoteLoading, setIsQuoteLoading] = useState(false);
  const [quoteError, setQuoteError] = useState<string | null>(null);
  const [requiresQuoteConfirmation, setRequiresQuoteConfirmation] = useState(false);
  const submitModeRef = useRef<PaymentMethodId>("credit_card");
  const sessionIdRef = useRef<string | null>(null);
  const lastAutomaticQuoteKeyRef = useRef<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    getPublicSiteSettings()
      .then((settings) => {
        if (isMounted) {
          setPaymentMethodsAvailability(settings.paymentMethods ?? noPaymentMethods);
        }
      })
      .catch(() => {
        if (isMounted) {
          setPaymentMethodsAvailability(noPaymentMethods);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const step4Schema = z.object({
    paymentMethod: z.enum(["credit_card", "debit_card", "unpaid"]).optional(),
    cardNumber: z.string().optional(),
    cardHolder: z.string().optional(),
    expiryDate: z.string().optional(),
    cvv: z.string().optional(),
    campaignCode: z.string().optional(),
    termsAccepted: z.boolean().refine((val) => val === true, {
      message: t("validation.requiredTerms"),
    }),
  })
    .refine((data) => {
      if (submitModeRef.current === "unpaid" || data.paymentMethod === "unpaid") return true;
      if (!data.cardNumber) return false;
      return /^[\d\s]{16,19}$/.test(data.cardNumber.replace(/\s/g, ""));
    }, { message: t("validation.requiredCardNumber"), path: ["cardNumber"] })
    .refine((data) => {
      if (submitModeRef.current === "unpaid" || data.paymentMethod === "unpaid") return true;
      if (!data.expiryDate) return false;
      return /^(0[1-9]|1[0-2])\/\d{2}$/.test(data.expiryDate);
    }, { message: t("validation.requiredExpiryDate"), path: ["expiryDate"] })
    .refine((data) => {
      if (submitModeRef.current === "unpaid" || data.paymentMethod === "unpaid") return true;
      if (!data.cvv) return false;
      return /^\d{3,4}$/.test(data.cvv);
    }, { message: t("validation.requiredCvv"), path: ["cvv"] });
  type Step4FormData = z.infer<typeof step4Schema>;

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<Step4FormData>({
    resolver: zodResolver(step4Schema),
    defaultValues: {
      paymentMethod: "credit_card",
      termsAccepted: false,
    },
  });

  const paymentMethods = useMemo<PaymentMethodOption[]>(() => {
    const methods: Array<PaymentMethodOption | null> = [
      paymentMethodsAvailability.creditCardEnabled
        ? ({
            id: "credit_card" as const,
            name: t("payment.creditCard"),
            description: t("payment.creditCardDesc"),
            icon: <CreditCard className="h-5 w-5" />,
          })
        : null,
      paymentMethodsAvailability.debitCardEnabled
        ? ({
            id: "debit_card" as const,
            name: t("payment.debitCard"),
            description: t("payment.debitCardDesc"),
            icon: <Banknote className="h-5 w-5" />,
          })
        : null,
      paymentMethodsAvailability.unpaidRequestEnabled
        ? ({
            id: "unpaid" as const,
            name: t("unpaidRequest.title"),
            description: t("unpaidRequest.description"),
            icon: <Check className="h-5 w-5" />,
          })
        : null,
    ];

    return methods.filter((method): method is PaymentMethodOption => method !== null);
  }, [paymentMethodsAvailability, t]);

  useEffect(() => {
    if (paymentMethods.length === 0) return;
    const selected = watch("paymentMethod");
    if (!selected || !paymentMethods.some((method) => method.id === selected)) {
      setValue("paymentMethod", paymentMethods[0].id);
    }
  }, [paymentMethods, setValue, watch]);

  const watchedPaymentMethod = watch("paymentMethod");
  const selectedPaymentMethod = paymentMethods.some((method) => method.id === watchedPaymentMethod)
    ? watchedPaymentMethod
    : paymentMethods[0]?.id;
  const isCreditCard = selectedPaymentMethod === "credit_card" || selectedPaymentMethod === "debit_card";

  const pickupDate = booking.dates?.pickupDate ?? searchParams.get("pickupDate") ?? "";
  const returnDate = booking.dates?.returnDate ?? searchParams.get("returnDate") ?? "";
  const rentalDays = Math.max(
    1,
    pickupDate && returnDate
      ? differenceInCalendarDays(new Date(returnDate), new Date(pickupDate))
      : 7
  );

  const vehicleParam = searchParams.get("vehicleGroupId") || searchParams.get("vehicle") || "";
  const selectedVehicleGroupId = booking.vehicle?.vehicleGroupId ?? vehicleParam;
  const pickupOfficeId = booking.dates?.pickupOfficeId ?? searchParams.get("pickup") ?? "";
  const returnOfficeId = booking.dates?.returnOfficeId ?? searchParams.get("return") ?? "";
  const vehicle = booking.vehicle;
  const vehicleGroupName = vehicle ? `${vehicle.groupName} - ${vehicle.vehicleName}` : searchParams.get("vehicleName") ?? selectedVehicleGroupId;

  const selectedExtras = booking.selectedExtras ?? [];
  const getSessionId = () => {
    if (!sessionIdRef.current) {
      sessionIdRef.current = crypto.randomUUID();
    }
    return sessionIdRef.current;
  };
  const driverAge = booking.driver?.dateOfBirth
    ? Math.max(0, Math.floor((Date.now() - new Date(booking.driver.dateOfBirth).getTime()) / 31_556_952_000))
    : undefined;
  const quoteExpired = !quote || new Date(quote.expiresAtUtc).getTime() <= Date.now();

  const refreshQuote = useCallback(async (
    campaignCode?: string,
    selections: SelectedBookingExtra[] = selectedExtras
  ) => {
    if (!selectedVehicleGroupId || !pickupOfficeId || !returnOfficeId || !pickupDate || !returnDate) {
      setQuote(null);
      setQuoteError(t("missingBookingDetailsStep"));
      return null;
    }

    setIsQuoteLoading(true);
    setQuoteError(null);
    try {
      const nextQuote = await createReservationQuote({
        vehicleGroupId: selectedVehicleGroupId,
        pickupOfficeId,
        returnOfficeId,
        pickupDateTimeUtc: `${pickupDate}T${booking.dates?.pickupTime ?? searchParams.get("pickupTime") ?? "00:00"}:00Z`,
        returnDateTimeUtc: `${returnDate}T${booking.dates?.returnTime ?? searchParams.get("returnTime") ?? "00:00"}:00Z`,
        campaignCode,
        driverAge,
        fullCoverageWaiver: false,
        locale,
        selectedExtras: selections.map(({ optionId, quantity, optionVersion }) => ({ optionId, quantity, optionVersion })),
      }, getSessionId());
      setQuote(nextQuote);
      return nextQuote;
    } catch (error) {
      setQuote(null);
      setQuoteError(error instanceof Error ? error.message : t("failedToRefreshQuote"));
      return null;
    } finally {
      setIsQuoteLoading(false);
    }
  }, [booking.dates?.pickupTime, booking.dates?.returnTime, driverAge, locale, pickupDate, pickupOfficeId, returnDate, returnOfficeId, searchParams, selectedExtras, selectedVehicleGroupId, t]);

  const automaticQuoteKey = [
    selectedVehicleGroupId,
    pickupOfficeId,
    returnOfficeId,
    pickupDate,
    returnDate,
    locale,
    booking.campaignCode ?? "",
    selectedExtras.map((extra) => `${extra.optionId}:${extra.optionVersion}:${extra.quantity}`).join(","),
  ].join("|");

  useEffect(() => {
    if (lastAutomaticQuoteKeyRef.current === automaticQuoteKey) return;
    lastAutomaticQuoteKeyRef.current = automaticQuoteKey;
    void refreshQuote(booking.campaignCode ?? undefined);
  }, [automaticQuoteKey, booking.campaignCode, refreshQuote]);

  const applyCampaign = async () => {
    const normalizedCode = campaignInput.trim().toUpperCase();

    if (!normalizedCode) {
      return;
    }

    if (!selectedVehicleGroupId || !pickupOfficeId || !returnOfficeId || !pickupDate || !returnDate) {
      toast.error(t("missingBookingDetails"));
      return;
    }

    const validation = await validateCampaignCode({
      code: normalizedCode,
      vehicleGroupId: selectedVehicleGroupId,
      rentalDays,
      pickupDate,
    });

    if (!validation) {
      setAppliedCampaign(null);
      setValue("campaignCode", "");
      toast.error(t("failedToValidateCampaign"));
      return;
    }

    if (!validation.valid) {
      setAppliedCampaign(null);
      setValue("campaignCode", "");
      toast.error(t("invalidCampaign"));
      return;
    }

    const nextQuote = await refreshQuote(normalizedCode);
    if (nextQuote) {
      setAppliedCampaign({ code: normalizedCode });
      setValue("campaignCode", normalizedCode);
    }
  };

  const onSubmit = async (data: Step4FormData) => {
    const customer = booking.customer;
    const driver = booking.driver;
    if (!customer || !driver) {
      toast.error(t("missingBookingDetailsStep"));
      return;
    }

    if (requiresQuoteConfirmation) {
      toast.error(t("quoteConfirmationRequired"));
      return;
    }

    if (quoteExpired) {
      await refreshQuote(appliedCampaign?.code);
      toast.error(t("quoteExpired"));
      return;
    }

    const reservationData = (activeQuote: ReservationQuote): CreateReservationData => ({
      vehicleGroupId: booking.vehicle?.vehicleGroupId ?? vehicleParam,
      pickupOfficeId,
      returnOfficeId,
      pickupDateTimeUtc: `${booking.dates?.pickupDate ?? pickupDate}T${booking.dates?.pickupTime ?? searchParams.get("pickupTime") ?? "00:00"}:00Z`,
      returnDateTimeUtc: `${booking.dates?.returnDate ?? returnDate}T${booking.dates?.returnTime ?? searchParams.get("returnTime") ?? "00:00"}:00Z`,
      customer,
      driver,
      campaignCode: activeQuote.appliedCampaignCode ?? undefined,
      driverAge,
      fullCoverageWaiver: false,
      quoteId: activeQuote.quoteId,
      locale,
    });

    try {
      const submittedMethod = data.paymentMethod;
      const selectedMethod = paymentMethods.some((method) => method.id === submittedMethod)
        ? submittedMethod
        : selectedPaymentMethod;
      if (!selectedMethod) {
        toast.error(t("paymentMethodsUnavailable"));
        return;
      }

      const requestOptions = { sessionId: getSessionId(), idempotencyKey: crypto.randomUUID() };
      const createForQuote = (activeQuote: ReservationQuote) => selectedMethod === "unpaid"
        ? createUnpaidReservationRequest(reservationData(activeQuote), requestOptions)
        : createReservation(reservationData(activeQuote), requestOptions);

      let reservation;
      try {
        reservation = await createForQuote(quote!);
      } catch (error) {
        if (!(error instanceof ApiError) || error.statusCode !== 409) {
          throw error;
        }

        const catalog = await getPublicReservationExtraOptions(selectedVehicleGroupId, locale);
        const optionsById = new Map(catalog.map((option) => [option.id, option]));
        const refreshedSelections = selectedExtras.flatMap((selection) => {
          const option = optionsById.get(selection.optionId);
          return option ? [{
            optionId: option.id,
            optionVersion: option.version,
            quantity: Math.min(selection.quantity, option.maxQuantity),
            code: option.code,
            name: option.name,
            description: option.description,
            unitPrice: option.unitPrice,
            pricingMode: option.pricingMode,
          }] : [];
        });
        updateExtras(refreshedSelections);
        const refreshedQuote = await refreshQuote(appliedCampaign?.code, refreshedSelections);
        if (!refreshedQuote) {
          setRequiresQuoteConfirmation(true);
          return;
        }

        try {
          reservation = await createForQuote(refreshedQuote);
        } catch (retryError) {
          if (retryError instanceof ApiError && retryError.statusCode === 409) {
            setRequiresQuoteConfirmation(true);
            setQuoteError(t("quoteConfirmationRequired"));
            return;
          }
          throw retryError;
        }
      }

      if (selectedMethod === "unpaid") {
        const queryParams = new URLSearchParams(searchParams.toString());
        queryParams.delete("extras");
        queryParams.set("code", reservation.publicCode);
        queryParams.set("request", "unpaid");
        router.push(`/${locale}/booking/confirmation?${queryParams.toString()}`);
        return;
      }

      const holdResult = await placeHold(reservation.id, { durationMinutes: 15 }, getSessionId());

      if (!holdResult) {
        toast.error(t("failedToHoldReservation"));
        return;
      }

      if (selectedMethod === "credit_card" || selectedMethod === "debit_card") {
        const [expiryMonth, expiryYear] = data.expiryDate?.split("/") ?? ["", ""];
        const paymentResult: PaymentIntentResponse = await createPaymentIntent({
          reservationId: reservation.id,
          idempotencyKey: crypto.randomUUID(),
          paymentMethod: selectedMethod,
          card: {
            holderName: data.cardHolder ?? "",
            number: data.cardNumber?.replace(/\s/g, "") ?? "",
            expiryMonth,
            expiryYear,
            cvv: data.cvv ?? "",
          },
        });

        sessionStorage.setItem("pendingPaymentIntentId", paymentResult.paymentIntentId);
        sessionStorage.setItem("pendingReservationPublicCode", reservation.publicCode);

        if (paymentResult.redirectUrl) {
          window.location.assign(paymentResult.redirectUrl);
          return;
        }
      }

      const queryParams = new URLSearchParams(searchParams.toString());
      queryParams.delete("extras");
      queryParams.set("code", reservation.publicCode);
      router.push(`/${locale}/booking/confirmation?${queryParams.toString()}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : t("failedToProcessPayment");
      toast.error(message);
    }
  };

  return (
    <div className="max-w-6xl mx-auto">
      <div className="mb-8">
        <h1
          className="text-3xl font-bold text-slate-900 mb-2"
          style={{ fontFamily: "Lexend, sans-serif" }}
        >
          {t("step4.title")}
        </h1>
        <p className="text-slate-600">{t("step4.subtitle")}</p>
        {isQuoteLoading && <p className="mt-3 text-sm text-sky-800" role="status">{t("loadingQuote")}</p>}
        {quote && !quoteExpired && (
          <p className="mt-3 text-sm text-sky-800" role="status">
            {t("quoteExpiresAt", { date: new Date(quote.expiresAtUtc).toLocaleTimeString(locale) })}
          </p>
        )}
        {quoteError && (
          <div className="mt-4 rounded-lg border border-red-200 bg-red-50 p-4" role="alert">
            <p className="text-sm text-red-800">{quoteError}</p>
            <button
              type="button"
              onClick={async () => {
                const nextQuote = await refreshQuote(appliedCampaign?.code);
                if (nextQuote) setRequiresQuoteConfirmation(false);
              }}
              className="mt-3 rounded-md border border-red-300 px-3 py-2 text-sm font-medium text-red-800 transition-colors hover:bg-red-100"
            >
              {t("refreshQuote")}
            </button>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <div className="lg:col-span-2">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
            <div className="bg-white rounded-xl border border-slate-200 p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
                  <Tag className="h-5 w-5 text-sky-600" />
                </div>
                <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
                  {t("campaignCode")}
                </h2>
              </div>

              <div className="flex gap-3">
                <input
                  type="text"
                  value={campaignInput}
                  onChange={(e) => setCampaignInput(e.target.value)}
                  placeholder={t("enterCode")}
                  className="flex-1 px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500 uppercase"
                />
                <button
                  type="button"
                  onClick={applyCampaign}
                  disabled={!campaignInput || appliedCampaign !== null || isValidating}
                  className={cn(
                    "px-6 py-3 font-medium rounded-lg transition-colors",
                    appliedCampaign
                      ? "bg-green-100 text-green-700 cursor-default"
                      : "bg-slate-200 text-slate-700 hover:bg-slate-300 disabled:opacity-50"
                  )}
                >
                  {appliedCampaign ? t("applied") : isValidating ? t("validating") : t("apply")}
                </button>
              </div>

              {appliedCampaign && (
                <div className="mt-4 p-4 bg-green-50 rounded-lg flex items-center gap-3">
                  <Check className="h-5 w-5 text-green-600" />
                  <div>
                    <p className="font-medium text-green-900">{t("applied")}</p>
                    <p className="text-sm text-green-700">{t("campaignCode")} {appliedCampaign.code}</p>
                  </div>
                </div>
              )}
            </div>

            <div className="bg-white rounded-xl border border-slate-200 p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
                  <CreditCard className="h-5 w-5 text-sky-600" />
                </div>
                <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
                  {t("payment.title")}
                </h2>
              </div>

              <div className="space-y-3">
                {paymentMethods.map((method) => (
                  <label
                    key={method.id}
                    className={cn(
                      "flex items-center gap-4 p-4 border-2 rounded-lg cursor-pointer transition-all",
                      selectedPaymentMethod === method.id
                        ? "border-sky-600 bg-sky-50"
                        : "border-slate-200 hover:border-sky-300"
                    )}
                  >
                    <input
                      type="radio"
                      value={method.id}
                      {...register("paymentMethod")}
                      className="w-4 h-4 text-sky-600 border-slate-300 focus:ring-sky-500"
                    />
                    <div
                      className={cn(
                        "w-10 h-10 rounded-lg flex items-center justify-center",
                        selectedPaymentMethod === method.id ? "bg-sky-600 text-white" : "bg-slate-100 text-slate-700"
                      )}
                    >
                      {method.icon}
                    </div>
                    <div>
                      <p className="font-medium text-slate-900">{method.name}</p>
                      <p className="text-sm text-slate-500">{method.description}</p>
                    </div>
                  </label>
                ))}
              </div>

              {paymentMethods.length === 0 && (
                <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
                  {t("paymentMethodsUnavailable")}
                </div>
              )}

              {isCreditCard && (
                <div className="mt-6 pt-6 border-t border-slate-200 space-y-4">
                    <div>
                      <label htmlFor="cardNumber" className="block text-sm font-medium text-slate-700 mb-2">
                        {t("payment.cardNumber")}
                      </label>
                      <div className="relative">
                        <CreditCard className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                        <input
                          type="text"
                          id="cardNumber"
                          {...register("cardNumber")}
                          placeholder="1234 5678 9012 3456"
                          className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                        />
                      </div>
                    </div>

                    <div>
                      <label htmlFor="cardHolder" className="block text-sm font-medium text-slate-700 mb-2">
                        {t("payment.cardHolder")}
                      </label>
                      <input
                        type="text"
                        id="cardHolder"
                        {...register("cardHolder")}
                        placeholder="John Doe"
                        className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                      />
                    </div>

                    <div className="grid grid-cols-2 gap-4">
                      <div>
                        <label htmlFor="expiryDate" className="block text-sm font-medium text-slate-700 mb-2">
                          {t("payment.expiryDate")}
                        </label>
                        <input
                          type="text"
                          id="expiryDate"
                          {...register("expiryDate")}
                          placeholder="MM/YY"
                          className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                        />
                      </div>

                      <div>
                        <label htmlFor="cvv" className="block text-sm font-medium text-slate-700 mb-2">
                          {t("payment.cvv")}
                        </label>
                        <div className="relative">
                          <Lock className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                          <input
                            type="text"
                            id="cvv"
                            {...register("cvv")}
                            placeholder="123"
                            className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                          />
                        </div>
                      </div>
                    </div>

                    <div className="p-4 bg-sky-50 rounded-lg flex items-start gap-3">
                      <Shield className="h-5 w-5 text-sky-600 mt-0.5 flex-shrink-0" />
                      <div>
                        <p className="text-sm font-medium text-sky-900">{t("payment.3dSecure")}</p>
                        <p className="text-xs text-sky-700 mt-1">
                          {t("payment.3dSecureDesc")}
                        </p>
                      </div>
                    </div>
                </div>
              )}
            </div>

            <div className="bg-white rounded-xl border border-slate-200 p-6">
              <label className="flex items-start gap-3 cursor-pointer">
                <input
                  type="checkbox"
                  {...register("termsAccepted")}
                  className="w-5 h-5 text-sky-600 border-slate-300 rounded focus:ring-sky-500 mt-0.5"
                />
                <div>
                  <p className="text-sm text-slate-700">
                    {t("termsAgreement")}{" "}
                    <Link href={`/${locale}/terms`} className="text-sky-700 hover:underline">
                      {t("terms")}
                    </Link>
                    {" "}{t("and")}{" "}
                    <Link href={`/${locale}/privacy`} className="text-sky-700 hover:underline">
                      {t("privacy")}
                    </Link>
                    {t("termsAgreementEnd")}
                  </p>
                  {errors.termsAccepted && (
                    <p className="mt-2 text-sm text-red-600 flex items-center gap-1">
                      <AlertCircle className="h-4 w-4" />
                      {errors.termsAccepted.message}
                    </p>
                  )}
                </div>
              </label>
            </div>

            <div className="flex items-center justify-between">
          <Link
            href={`/${locale}/booking/step3?${searchParams.toString()}`}
            className="inline-flex items-center gap-2 px-6 py-3 text-slate-600 hover:text-slate-900 transition-colors"
          >
            <ArrowLeft className="h-5 w-5" />
            {t("back")}
          </Link>

          <button
            type="submit"
            disabled={isSubmitting || paymentMethods.length === 0}
            onClick={() => {
              submitModeRef.current = selectedPaymentMethod ?? "credit_card";
            }}
            className="inline-flex items-center gap-2 px-8 py-4 bg-sky-700 text-white font-semibold rounded-lg hover:bg-sky-800 transition-colors"
          >
            {isSubmitting
              ? t("completing")
              : selectedPaymentMethod === "unpaid"
                ? t("completeRequest")
                : t("completeBooking")}
            <ArrowRight className="h-5 w-5" />
          </button>
            </div>
          </form>
        </div>

          <div className="lg:col-span-1">
            <div className="sticky top-24">
              {quote ? (
                <PriceBreakdown
                  dailyRate={quote.dailyRate}
                  days={quote.rentalDays}
                  vehicleGroup={vehicleGroupName}
                  extras={quote.extraItems.map((item) => ({ name: item.name, price: item.total }))}
                  campaignDiscountAmount={quote.campaignDiscount}
                  baseAmount={quote.baseTotal}
                  totalAmount={quote.finalTotal}
                  campaignCode={quote.appliedCampaignCode ?? undefined}
                  currency={quote.currency}
                />
              ) : (
                <div className="rounded-xl border border-slate-200 bg-white p-6 text-sm text-slate-700">
                  {isQuoteLoading ? t("loadingQuote") : t("quoteUnavailable")}
                </div>
              )}
            </div>
          </div>
      </div>
    </div>
  );
}
