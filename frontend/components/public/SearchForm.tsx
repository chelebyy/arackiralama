"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";
import {
  MapPin,
  Calendar,
  Clock,
  Search,
  Check
} from "lucide-react";
import { cn } from "@/lib/utils";

interface SearchFormProps {
  className?: string;
  variant?: "hero" | "default";
}

export default function SearchForm({ className, variant = "default" }: SearchFormProps) {
  const t = useTranslations("searchForm");
  const [sameLocation, setSameLocation] = useState(true);
  const [pickupLocation, setPickupLocation] = useState("");
  const [returnLocation, setReturnLocation] = useState("");

  const locations = [
    { key: "airport", value: "gazipasa-airport" },
    { key: "airportAntalya", value: "antalya-airport" },
    { key: "cityCenter", value: "alanya-center" },
    { key: "mahmutlar", value: "mahmutlar" },
    { key: "kargicak", value: "kargicak" },
    { key: "konakli", value: "konakli" },
    { key: "avsallar", value: "avsallar" },
  ];

  const isHero = variant === "hero";

  return (
    <div
      className={cn(
        "w-full rounded-2xl bg-white",
        isHero ? "shadow-xl border border-[#E2E8F0]" : "shadow-lg border border-[#E2E8F0]",
        className
      )}
    >
      <div className={cn("p-6 lg:p-8", isHero && "lg:p-10")}>
        <h3 className={cn(
          "font-bold text-[#0F172A] mb-6",
          isHero ? "text-2xl" : "text-xl"
        )}>
          {t("title")}
        </h3>

        <form className="space-y-5" onSubmit={(e) => e.preventDefault()}>
          {/* Locations Grid */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
            {/* Pickup Location */}
            <div className="space-y-2">
              <label htmlFor="pickupLocation" className="block text-sm font-semibold text-[#334155]">
                {t("pickupLocation")}
              </label>
              <div className="relative">
                <MapPin className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
                <select
                  id="pickupLocation"
                  value={pickupLocation}
                  onChange={(e) => {
                    setPickupLocation(e.target.value);
                    if (sameLocation) {
                      setReturnLocation(e.target.value);
                    }
                  }}
                  className={cn(
                    "w-full pl-12 pr-4 py-3.5 rounded-xl",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-sm text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer",
                    "appearance-none"
                  )}
                >
                  <option value="">{t("pickupLocationPlaceholder")}</option>
                  {locations.map((loc) => (
                    <option key={loc.value} value={loc.value}>
                      {t(`locationOptions.${loc.key}`)}
                    </option>
                  ))}
                </select>
                <div className="absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none">
                  <svg className="h-4 w-4 text-[#64748B]" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                  </svg>
                </div>
              </div>
            </div>

            {/* Return Location */}
            <div className={cn("space-y-2", sameLocation && "opacity-60")}>
              <label htmlFor="returnLocation" className="block text-sm font-semibold text-[#334155]">
                {t("returnLocation")}
              </label>
              <div className="relative">
                <MapPin className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
                <select
                  id="returnLocation"
                  value={returnLocation}
                  onChange={(e) => setReturnLocation(e.target.value)}
                  disabled={sameLocation}
                  className={cn(
                    "w-full pl-12 pr-4 py-3.5 rounded-xl",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-sm text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer disabled:cursor-not-allowed",
                    "appearance-none"
                  )}
                >
                  <option value="">{t("returnLocationPlaceholder")}</option>
                  {locations.map((loc) => (
                    <option key={loc.value} value={loc.value}>
                      {t(`locationOptions.${loc.key}`)}
                    </option>
                  ))}
                </select>
                {!sameLocation && (
                  <div className="absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none">
                    <svg className="h-4 w-4 text-[#64748B]" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                    </svg>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Same Location Checkbox */}
          <label className="flex items-center gap-3 cursor-pointer group">
            <button
              type="button"
              onClick={() => {
                setSameLocation(!sameLocation);
                if (!sameLocation) {
                  setReturnLocation(pickupLocation);
                }
              }}
              className={cn(
                "flex h-5 w-5 items-center justify-center rounded border-2",
                "transition-all duration-200",
                sameLocation
                  ? "bg-[#0369A1] border-[#0369A1]"
                  : "bg-white border-[#CBD5E1] group-hover:border-[#0369A1]"
              )}
            >
              {sameLocation && <Check className="h-3.5 w-3.5 text-white" />}
            </button>
            <span className="text-sm text-[#475569] group-hover:text-[#334155] transition-colors">
              {t("sameLocation")}
            </span>
          </label>

          {/* Dates Grid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
            {/* Pickup Date */}
            <div className="space-y-2">
              <label htmlFor="pickupDate" className="block text-sm font-semibold text-[#334155]">
                {t("pickupDate")}
              </label>
              <div className="relative">
                <Calendar className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
                <input
                  id="pickupDate"
                  type="date"
                  className={cn(
                    "w-full pl-12 pr-4 py-3.5 rounded-xl",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-sm text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer"
                  )}
                />
              </div>
            </div>

            {/* Pickup Time */}
            <div className="space-y-2">
              <label htmlFor="pickupTime" className="block text-sm font-semibold text-[#334155]">
                {t("pickupTime")}
              </label>
              <div className="relative">
                <Clock className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
                <input
                  id="pickupTime"
                  type="time"
                  defaultValue="10:00"
                  className={cn(
                    "w-full pl-12 pr-4 py-3.5 rounded-xl",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-sm text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer"
                  )}
                />
              </div>
            </div>

            {/* Return Date */}
            <div className="space-y-2">
              <label htmlFor="returnDate" className="block text-sm font-semibold text-[#334155]">
                {t("returnDate")}
              </label>
              <div className="relative">
                <Calendar className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
                <input
                  id="returnDate"
                  type="date"
                  className={cn(
                    "w-full pl-12 pr-4 py-3.5 rounded-xl",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-sm text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer"
                  )}
                />
              </div>
            </div>

            {/* Return Time */}
            <div className="space-y-2">
              <label htmlFor="returnTime" className="block text-sm font-semibold text-[#334155]">
                {t("returnTime")}
              </label>
              <div className="relative">
                <Clock className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
                <input
                  id="returnTime"
                  type="time"
                  defaultValue="10:00"
                  className={cn(
                    "w-full pl-12 pr-4 py-3.5 rounded-xl",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-sm text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer"
                  )}
                />
              </div>
            </div>
          </div>

          {/* Search Button */}
          <Link
            href="/vehicles"
            className={cn(
              "flex items-center justify-center gap-2 w-full py-4 rounded-xl",
              "text-base font-bold text-white bg-[#0369A1]",
              "hover:bg-[#0284C7] active:bg-[#075985]",
              "transition-all duration-200 cursor-pointer",
              "focus:outline-none focus:ring-4 focus:ring-[#0369A1]/30",
              "shadow-lg hover:shadow-xl"
            )}
          >
            <Search className="h-5 w-5" />
            {t("searchButton")}
          </Link>
        </form>
      </div>
    </div>
  );
}
