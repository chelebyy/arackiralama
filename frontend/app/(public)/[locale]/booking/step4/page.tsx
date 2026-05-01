"use client";

import { useParams, useSearchParams, useRouter } from "next/navigation";
import { useState } from "react";
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
  Wallet,
} from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { PriceBreakdown } from "@/components/public/PriceBreakdown";
import { differenceInCalendarDays } from "date-fns";
import { useBookingState } from "@/hooks/useBooking";
import { useValidateCampaign } from "@/hooks/usePricing";
import { createReservation } from "@/lib/api/reservations";
import type { CreateReservationData } from "@/lib/api/types";

const extraOptions = [
  { id: "child_seat", name: "Child Safety Seat", price: 10, priceType: "per_day" as const },
  { id: "additional_driver", name: "Additional Driver", price: 15, priceType: "per_rental" as const },
  { id: "gps", name: "GPS Navigation", price: 8, priceType: "per_day" as const },
  { id: "wifi", name: "Mobile WiFi", price: 12, priceType: "per_day" as const },
];

const step4Schema = z.object({
  paymentMethod: z.enum(["credit_card", "debit_card", "paypal"]),
  cardNumber: z.string().optional(),
  cardHolder: z.string().optional(),
  expiryDate: z.string().optional(),
  cvv: z.string().optional(),
  campaignCode: z.string().optional(),
  termsAccepted: z.boolean().refine((val) => val === true, {
    message: "You must accept the terms and conditions",
  }),
})
  .refine((data) => {
    if (data.paymentMethod === "paypal") return true;
    if (!data.cardNumber) return false;
    return /^[\d\s]{16,19}$/.test(data.cardNumber.replace(/\s/g, ""));
  }, { message: "Card number is required and must be 16 digits", path: ["cardNumber"] })
  .refine((data) => {
    if (data.paymentMethod === "paypal") return true;
    if (!data.expiryDate) return false;
    return /^(0[1-9]|1[0-2])\/\d{2}$/.test(data.expiryDate);
  }, { message: "Expiry date is required and must be MM/YY", path: ["expiryDate"] })
  .refine((data) => {
    if (data.paymentMethod === "paypal") return true;
    if (!data.cvv) return false;
    return /^\d{3,4}$/.test(data.cvv);
  }, { message: "CVV is required and must be 3-4 digits", path: ["cvv"] });

type Step4FormData = z.infer<typeof step4Schema>;

const paymentMethods = [
  {
    id: "credit_card",
    name: "Credit Card",
    description: "Pay with Visa, Mastercard, or Amex",
    icon: <CreditCard className="h-5 w-5" />,
  },
  {
    id: "debit_card",
    name: "Debit Card",
    description: "Direct bank payment",
    icon: <Banknote className="h-5 w-5" />,
  },
  {
    id: "paypal",
    name: "PayPal",
    description: "Pay with your PayPal account",
    icon: <Wallet className="h-5 w-5" />,
  },
];

export default function BookingStep4Page() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = params.locale as string;
  const booking = useBookingState();
  const { validate: validateCampaignCode, isValidating } = useValidateCampaign();
  const [appliedCampaign, setAppliedCampaign] = useState<{ code: string } | null>(null);
  const [campaignInput, setCampaignInput] = useState("");

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

  const selectedPaymentMethod = watch("paymentMethod");
  const isCreditCard = selectedPaymentMethod === "credit_card" || selectedPaymentMethod === "debit_card";

  const pickupDate = booking.dates?.pickupDate ?? searchParams.get("pickupDate") ?? "";
  const returnDate = booking.dates?.returnDate ?? searchParams.get("returnDate") ?? "";
  const extrasParam = searchParams.get("extras") || "";
  const rentalDays = Math.max(
    1,
    pickupDate && returnDate
      ? differenceInCalendarDays(new Date(returnDate), new Date(pickupDate))
      : 7
  );

  const vehicleParam = searchParams.get("vehicleGroupId") || searchParams.get("vehicle") || "";
  const selectedVehicleGroupId = booking.vehicle?.vehicleGroupId ?? vehicleParam;
  const vehicle = booking.vehicle;

  const applyCampaign = async () => {
    const normalizedCode = campaignInput.trim().toUpperCase();

    if (!normalizedCode) {
      return;
    }

    if (!selectedVehicleGroupId || !pickupDate) {
      toast.error("Missing booking details for campaign validation.");
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
      toast.error("Failed to validate campaign code.");
      return;
    }

    if (!validation.valid) {
      setAppliedCampaign(null);
      setValue("campaignCode", "");
      toast.error("Invalid campaign code.");
      return;
    }

    setAppliedCampaign({ code: normalizedCode });
    setValue("campaignCode", normalizedCode);
  };

  const onSubmit = async (_data: Step4FormData) => {
    if (!booking.customer || !booking.driver) {
      toast.error("Booking details are missing. Please return to the previous step.");
      return;
    }

    const selectedExtraIds = extrasParam ? extrasParam.split(",").filter(Boolean) : [];

    const reservationData: CreateReservationData = {
      vehicleGroupId: booking.vehicle?.vehicleGroupId ?? vehicleParam,
      pickupOfficeId: booking.dates?.pickupOfficeId ?? searchParams.get("pickup") ?? "",
      returnOfficeId: booking.dates?.returnOfficeId ?? searchParams.get("return") ?? "",
      pickupDateTimeUtc: `${booking.dates?.pickupDate ?? pickupDate}T${booking.dates?.pickupTime ?? searchParams.get("pickupTime") ?? "00:00"}:00Z`,
      returnDateTimeUtc: `${booking.dates?.returnDate ?? returnDate}T${booking.dates?.returnTime ?? searchParams.get("returnTime") ?? "00:00"}:00Z`,
      customer: booking.customer,
      extraDriverCount: selectedExtraIds.filter((id) => id.trim() === "additional_driver").length,
      childSeatCount: selectedExtraIds.filter((id) => id.trim() === "child_seat").length,
      campaignCode: appliedCampaign?.code,
    };

    try {
      const reservation = await createReservation(reservationData);
      const code = reservation.publicCode;

      const queryParams = new URLSearchParams(searchParams.toString());
      queryParams.set("code", code);
      router.push(`/${locale}/booking/confirmation?${queryParams.toString()}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to complete booking";
      toast.error(message);
    }
  };

  const selectedExtras = extrasParam
    ? extrasParam.split(",").map((id) => {
        const extra = extraOptions.find((e) => e.id === id.trim());
        if (!extra) return null;
        return {
          name: extra.name,
          price: extra.priceType === "per_day" ? extra.price * rentalDays : extra.price,
        };
      }).filter((e): e is { name: string; price: number } => e !== null)
    : [];

  return (
    <div className="max-w-6xl mx-auto">
      <div className="mb-8">
        <h1
          className="text-3xl font-bold text-slate-900 mb-2"
          style={{ fontFamily: "Lexend, sans-serif" }}
        >
          Payment
        </h1>
        <p className="text-slate-600">Complete your booking by providing payment details.</p>
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
                  Campaign Code
                </h2>
              </div>

              <div className="flex gap-3">
                <input
                  type="text"
                  value={campaignInput}
                  onChange={(e) => setCampaignInput(e.target.value)}
                  placeholder="Enter code"
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
                  {appliedCampaign ? "Applied" : isValidating ? "Validating..." : "Apply"}
                </button>
              </div>

              {appliedCampaign && (
                <div className="mt-4 p-4 bg-green-50 rounded-lg flex items-center gap-3">
                  <Check className="h-5 w-5 text-green-600" />
                  <div>
                    <p className="font-medium text-green-900">Code {appliedCampaign.code} applied!</p>
                    <p className="text-sm text-green-700">Campaign code validated and ready for your reservation.</p>
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
                  Payment Method
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
                        selectedPaymentMethod === method.id ? "bg-sky-600 text-white" : "bg-slate-100 text-slate-600"
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

              {isCreditCard && (
                <div className="mt-6 pt-6 border-t border-slate-200 space-y-4">
                  <div>
                    <label htmlFor="cardNumber" className="block text-sm font-medium text-slate-700 mb-2">
                      Card Number
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
                      Card Holder Name
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
                        Expiry Date
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
                        CVV
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
                      <p className="text-sm font-medium text-sky-900">3D Secure Payment</p>
                      <p className="text-xs text-sky-700 mt-1">
                        Your payment is secured with 3D Secure authentication. You may be redirected to your bank&apos;s verification page.
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
                    I agree to the{" "}
                    <Link href={`/${locale}/terms`} className="text-sky-700 hover:underline">
                      Terms and Conditions
                    </Link>{" "}
                    and{" "}
                    <Link href={`/${locale}/privacy`} className="text-sky-700 hover:underline">
                      Privacy Policy
                    </Link>
                    . I confirm that I have a valid driver&apos;s license and meet the minimum age requirements.
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
                Back
              </Link>

              <button
                type="submit"
                disabled={isSubmitting}
                className="inline-flex items-center gap-2 px-8 py-4 bg-sky-700 text-white font-semibold rounded-lg hover:bg-sky-800 transition-colors"
              >
                {isSubmitting ? "Completing..." : "Complete Booking"}
                <ArrowRight className="h-5 w-5" />
              </button>
            </div>
          </form>
        </div>

        <div className="lg:col-span-1">
          <div className="sticky top-24">
            <PriceBreakdown
              dailyRate={vehicle?.dailyPrice ?? 0}
              days={rentalDays}
              vehicleGroup={vehicle ? `${vehicle.groupName} - ${vehicle.vehicleName}` : "Unknown Vehicle"}
              extras={selectedExtras}
              campaignDiscount={booking.campaignDiscount ?? 0}
              currency="TRY"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
