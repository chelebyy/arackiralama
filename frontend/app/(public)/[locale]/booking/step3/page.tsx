"use client";

import { useParams, useSearchParams, useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState, type MouseEvent } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
  User,
  Mail,
  Phone,
  CreditCard,
  Baby,
  Users,
  Shield,
  ArrowRight,
  ArrowLeft,
  Info,
  Check,
  Minus,
  Navigation,
  Plus,
  RefreshCw,
  Wifi,
} from "lucide-react";
import Link from "next/link";
import useSWR from "swr";
import { cn } from "@/lib/utils";
import { useBookingActions, useBookingState } from "@/hooks/useBooking";
import { useOffices } from "@/hooks/useVehicles";
import { getPublicReservationExtraOptions } from "@/lib/api/reservationExtras";
import type { PublicReservationExtraOption, SelectedBookingExtra } from "@/lib/api/types";
import { useTranslations } from "next-intl";
import { differenceInCalendarDays } from "date-fns";

function openDatePicker(event: MouseEvent<HTMLInputElement>) {
  event.currentTarget.showPicker?.();
}

const officeSlugPatterns: Record<string, string> = {
  ala: "alanya",
  gzp: "gazipasa",
  ayt: "antalya",
  mahmutlar: "mahmutlar",
  kargicak: "kargicak",
  konakli: "konakli",
  avsallar: "avsallar",
};

function normalizeOfficeText(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
}

function isGuid(value: string): boolean {
  return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(value);
}

function resolveOffice(offices: { id: string; name: string }[], slugOrGuid: string): { id: string; name: string } {
  if (isGuid(slugOrGuid)) {
    const matchedGuid = offices.find((office) => office.id === slugOrGuid);
    return { id: slugOrGuid, name: matchedGuid?.name ?? slugOrGuid };
  }

  const input = normalizeOfficeText(slugOrGuid);
  const directMatch = offices.find((office) => normalizeOfficeText(office.name) === input);
  if (directMatch) return directMatch;

  const pattern = officeSlugPatterns[input] ?? Object.values(officeSlugPatterns).find((value) => input.includes(value));
  const matched = pattern
    ? offices.find((office) => normalizeOfficeText(office.name).includes(pattern))
    : undefined;

  return matched ?? { id: slugOrGuid, name: slugOrGuid };
}

function ExtraIcon({ iconKey }: { iconKey: string }) {
  const Icon = iconKey === "baby"
    ? Baby
    : iconKey === "users"
      ? Users
      : iconKey === "navigation"
        ? Navigation
        : iconKey === "wifi"
          ? Wifi
          : Shield;
  return <Icon className="h-5 w-5" />;
}

function toSelection(option: PublicReservationExtraOption, quantity: number): SelectedBookingExtra {
  return {
    optionId: option.id,
    quantity,
    optionVersion: option.version,
    code: option.code,
    name: option.name,
    description: option.description,
    unitPrice: option.unitPrice,
    pricingMode: option.pricingMode,
  };
}

export default function BookingStep3Page() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = params.locale as string;
  const t = useTranslations("booking");
  const booking = useBookingState();
  const { updateCustomerDetails, updateExtras, setDates } = useBookingActions();
  const { offices, isLoading: officesLoading, isError: officesError } = useOffices();
  const [legacyWarning, setLegacyWarning] = useState(false);
  const legacyExtrasApplied = useRef(false);
  const pickupOffice = searchParams.get("pickup") || "ala";
  const returnOffice = searchParams.get("return") || pickupOffice;
  const pickupOfficeMatch = resolveOffice(offices, pickupOffice);
  const returnOfficeMatch = resolveOffice(offices, returnOffice);
  const officesReady =
    !officesLoading &&
    !officesError &&
    isGuid(pickupOfficeMatch.id) &&
    isGuid(returnOfficeMatch.id);
  const vehicleGroupId = booking.vehicle?.vehicleGroupId ?? searchParams.get("vehicleGroupId") ?? searchParams.get("vehicle") ?? "";
  const {
    data: extraOptions = [],
    error: extraOptionsError,
    isLoading: extraOptionsLoading,
    mutate: retryExtraOptions,
  } = useSWR<PublicReservationExtraOption[], Error>(
    vehicleGroupId ? ["reservation-extra-options", vehicleGroupId, locale] : null,
    () => getPublicReservationExtraOptions(vehicleGroupId, locale),
    { revalidateOnFocus: false }
  );
  const pickupDate = searchParams.get("pickupDate") || "";
  const returnDate = searchParams.get("returnDate") || "";
  const rentalDays = pickupDate && returnDate
    ? Math.max(1, differenceInCalendarDays(new Date(returnDate), new Date(pickupDate)))
    : 1;
  const selectionsByOptionId = useMemo(
    () => new Map(booking.selectedExtras.map((selection) => [selection.optionId, selection])),
    [booking.selectedExtras]
  );

  const step3Schema = z.object({
    firstName: z.string().min(2, t("validation.requiredFirstName")),
    lastName: z.string().min(2, t("validation.requiredLastName")),
    email: z.string().email(t("validation.invalidEmail")),
    phone: z.string().min(10, t("validation.requiredPhone")),
    driverLicense: z.string().min(5, t("validation.requiredLicense")),
    driverLicenseCountry: z.string().min(1, t("validation.requiredLicenseCountry")),
    birthDate: z.string().min(1, t("validation.requiredBirthDate")),
    specialRequests: z.string().optional(),
  });
  type Step3FormData = z.infer<typeof step3Schema>;

  useEffect(() => {
    if (legacyExtrasApplied.current || booking.selectedExtras.length > 0 || extraOptions.length === 0) return;

    const legacyCodes = (searchParams.get("extras") || "").split(",").map((code) => code.trim()).filter(Boolean);
    if (legacyCodes.length === 0) return;

    legacyExtrasApplied.current = true;
    const supportedCodes = new Set(["child_seat", "additional_driver", "gps", "wifi"]);
    const optionsByCode = new Map(extraOptions.map((option) => [option.code, option]));
    const restoredSelections = legacyCodes.flatMap((code) => {
      const option = supportedCodes.has(code) ? optionsByCode.get(code) : undefined;
      return option ? [toSelection(option, 1)] : [];
    });

    if (restoredSelections.length > 0) {
      updateExtras(restoredSelections);
    }
    setLegacyWarning(restoredSelections.length !== legacyCodes.length);
  }, [booking.selectedExtras.length, extraOptions, searchParams, updateExtras]);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Step3FormData>({
    resolver: zodResolver(step3Schema),
  });

  const updateQuantity = (option: PublicReservationExtraOption, quantity: number) => {
    const currentSelections = booking.selectedExtras.filter((selection) => selection.optionId !== option.id);
    if (quantity > 0) {
      currentSelections.push(toSelection(option, quantity));
    }
    updateExtras(currentSelections);
  };

  const onSubmit = (data: Step3FormData) => {
    if (!officesReady) return;

    setDates({
      pickupOfficeId: pickupOfficeMatch.id,
      pickupOfficeName: pickupOfficeMatch.name,
      pickupDate: searchParams.get("pickupDate") || "",
      pickupTime: searchParams.get("pickupTime") || "10:00",
      returnOfficeId: returnOfficeMatch.id,
      returnOfficeName: returnOfficeMatch.name,
      returnDate: searchParams.get("returnDate") || "",
      returnTime: searchParams.get("returnTime") || "09:00",
    });

    updateCustomerDetails(
      {
        firstName: data.firstName,
        lastName: data.lastName,
        email: data.email,
        phone: data.phone,
        dateOfBirth: data.birthDate,
      },
      {
        firstName: data.firstName,
        lastName: data.lastName,
        dateOfBirth: data.birthDate,
        licenseNumber: data.driverLicense,
        licenseCountry: data.driverLicenseCountry,
        licenseIssueDate: "2020-01-01",
        licenseExpiryDate: "2030-01-01",
        isPrimaryDriver: true,
      }
    );

    const queryParams = new URLSearchParams(searchParams.toString());
    queryParams.delete("extras");
    router.push(`/${locale}/booking/step4?${queryParams.toString()}`);
  };

  return (
    <div className="max-w-4xl mx-auto">
      <div className="mb-8">
        <h1
          className="text-3xl font-bold text-slate-900 mb-2"
          style={{ fontFamily: "Lexend, sans-serif" }}
        >
          {t("step3.title")}
        </h1>
        <p className="text-slate-600">{t("step3.subtitle")}</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
              <User className="h-5 w-5 text-sky-600" />
            </div>
            <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
              {t("primaryDriver")}
            </h2>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label htmlFor="firstName" className="block text-sm font-medium text-slate-700 mb-2">
                {t("driverInfo.firstName")}
              </label>
              <input
                type="text"
                id="firstName"
                {...register("firstName")}
                className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                placeholder="John"
              />
              {errors.firstName && (
                <p className="mt-1 text-sm text-red-600">{errors.firstName.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="lastName" className="block text-sm font-medium text-slate-700 mb-2">
                {t("driverInfo.lastName")}
              </label>
              <input
                type="text"
                id="lastName"
                {...register("lastName")}
                className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                placeholder="Doe"
              />
              {errors.lastName && (
                <p className="mt-1 text-sm text-red-600">{errors.lastName.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="email" className="block text-sm font-medium text-slate-700 mb-2">
                {t("driverInfo.email")}
              </label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="email"
                  id="email"
                  {...register("email")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="john.doe@example.com"
                />
              </div>
              {errors.email && (
                <p className="mt-1 text-sm text-red-600">{errors.email.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="phone" className="block text-sm font-medium text-slate-700 mb-2">
                {t("driverInfo.phone")}
              </label>
              <div className="relative">
                <Phone className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="tel"
                  id="phone"
                  {...register("phone")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="+90 555 123 4567"
                />
              </div>
              {errors.phone && (
                <p className="mt-1 text-sm text-red-600">{errors.phone.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="birthDate" className="block text-sm font-medium text-slate-700 mb-2">
                {t("driverInfo.birthDate")}
              </label>
              <input
                type="date"
                id="birthDate"
                {...register("birthDate")}
                onClick={openDatePicker}
                className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
              />
              {errors.birthDate && (
                <p className="mt-1 text-sm text-red-600">{errors.birthDate.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="driverLicense" className="block text-sm font-medium text-slate-700 mb-2">
                {t("driverInfo.licenseNumber")}
              </label>
              <div className="relative">
                <CreditCard className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="text"
                  id="driverLicense"
                  {...register("driverLicense")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="License number"
                />
              </div>
              {errors.driverLicense && (
                <p className="mt-1 text-sm text-red-600">{errors.driverLicense.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="driverLicenseCountry" className="block text-sm font-medium text-slate-700 mb-2">
                {t("driverInfo.licenseCountry")}
              </label>
              <div className="relative">
                <CreditCard className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="text"
                  id="driverLicenseCountry"
                  {...register("driverLicenseCountry")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="e.g. Turkey, Germany, United Kingdom"
                />
              </div>
              {errors.driverLicenseCountry && (
                <p className="mt-1 text-sm text-red-600">{errors.driverLicenseCountry.message}</p>
              )}
            </div>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
              <Shield className="h-5 w-5 text-sky-600" />
            </div>
            <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
              {t("additionalOptions")}
            </h2>
          </div>

          {extraOptionsLoading ? (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4" aria-label={t("loadingExtraOptions")}>
              {[0, 1].map((index) => <div key={index} className="h-32 animate-pulse rounded-lg bg-slate-100" />)}
            </div>
          ) : extraOptionsError ? (
            <div className="rounded-lg border border-red-200 bg-red-50 p-4" role="alert">
              <p className="text-sm text-red-700">{t("failedToLoadExtraOptions")}</p>
              <button
                type="button"
                onClick={() => retryExtraOptions()}
                className="mt-3 inline-flex items-center gap-2 rounded-md border border-red-300 px-3 py-2 text-sm font-medium text-red-800 transition-colors hover:bg-red-100"
              >
                <RefreshCw className="h-4 w-4" />
                {t("retry")}
              </button>
            </div>
          ) : extraOptions.length === 0 ? (
            <p className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">{t("noExtraOptions")}</p>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {extraOptions.map((option) => {
                const quantity = selectionsByOptionId.get(option.id)?.quantity ?? 0;
                const isSelected = quantity > 0;
                const displayTotal = option.pricingMode === "PER_DAY"
                  ? option.unitPrice * rentalDays * quantity
                  : option.unitPrice * quantity;

                return (
                  <div
                    key={option.id}
                    className={cn(
                      "relative rounded-lg border-2 p-4 transition-colors duration-200",
                      isSelected ? "border-sky-600 bg-sky-50" : "border-slate-200 hover:border-sky-300"
                    )}
                  >
                    <div className="flex items-start gap-3">
                      <div className={cn("flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg", isSelected ? "bg-sky-600 text-white" : "bg-slate-100 text-slate-700")}>
                        {isSelected ? <Check className="h-5 w-5" /> : <ExtraIcon iconKey={option.iconKey} />}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-start justify-between gap-3">
                          <h3 className="font-medium text-slate-900">{option.name}</h3>
                          <span className="whitespace-nowrap font-semibold text-sky-700">
                            ₺{option.unitPrice}
                            <span className="text-xs font-normal text-slate-600">/{option.pricingMode === "PER_DAY" ? t("day") : t("rental")}</span>
                          </span>
                        </div>
                        <p className="mt-1 text-sm text-slate-600">{option.description}</p>
                        <div className="mt-4 flex items-center justify-between gap-3">
                          <div className="inline-flex items-center rounded-md border border-slate-300 bg-white">
                            <button
                              type="button"
                              aria-label={`${t("decreaseQuantity")} ${option.name}`}
                              disabled={quantity === 0}
                              onClick={() => updateQuantity(option, quantity - 1)}
                              className="p-2 text-slate-700 transition-colors hover:bg-slate-100 disabled:cursor-not-allowed disabled:text-slate-300"
                            >
                              <Minus className="h-4 w-4" />
                            </button>
                            <span className="min-w-9 border-x border-slate-300 px-3 py-2 text-center text-sm font-semibold text-slate-900">{quantity}</span>
                            <button
                              type="button"
                              aria-label={`${t("increaseQuantity")} ${option.name}`}
                              disabled={quantity === option.maxQuantity}
                              onClick={() => updateQuantity(option, quantity + 1)}
                              className="p-2 text-slate-700 transition-colors hover:bg-slate-100 disabled:cursor-not-allowed disabled:text-slate-300"
                            >
                              <Plus className="h-4 w-4" />
                            </button>
                          </div>
                          {isSelected && <span className="text-sm font-semibold text-slate-800">₺{displayTotal.toFixed(2)}</span>}
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
          <p className="mt-4 text-sm text-sky-800">{t("quoteAuthoritative")}</p>
          {legacyWarning && <p className="mt-2 text-sm text-amber-800" role="status">{t("legacyExtrasUpdated")}</p>}
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <label htmlFor="specialRequests" className="block text-sm font-medium text-slate-700 mb-2">
            {t("specialRequests")}
          </label>
          <textarea
            id="specialRequests"
            {...register("specialRequests")}
            rows={3}
            className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
            placeholder={t("specialRequestsPlaceholder")}
          />
        </div>

        <div className="flex items-center justify-between">
          <Link
            href={`/${locale}/booking/step2?${searchParams.toString()}`}
            className="inline-flex items-center gap-2 px-6 py-3 text-slate-600 hover:text-slate-900 transition-colors"
          >
            <ArrowLeft className="h-5 w-5" />
            {t("back")}
          </Link>

          <button
            type="submit"
            disabled={!officesReady}
            className={cn(
              "inline-flex items-center gap-2 px-8 py-4 font-semibold rounded-lg transition-colors",
              officesReady
                ? "bg-sky-700 text-white hover:bg-sky-800"
                : "bg-slate-200 text-slate-400 cursor-not-allowed"
            )}
          >
            {t("continueToPayment")}
            <ArrowRight className="h-5 w-5" />
          </button>
        </div>

        {!officesReady && (
          <div className="flex items-start gap-2 text-sm text-slate-600">
            <Info className="h-4 w-4 mt-0.5 text-sky-600" />
            <span>{t("loadingOffices")}</span>
          </div>
        )}
      </form>
    </div>
  );
}
