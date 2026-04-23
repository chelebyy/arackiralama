"use client";

import { useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { useRouter, useParams } from "next/navigation";
import {
  MapPin,
  Calendar,
  Clock,
  Search,
  Check,
  ChevronDown
} from "lucide-react";
import { cn } from "@/lib/utils";

interface SearchFormProps {
  readonly className?: string;
  readonly variant?: "hero" | "default";
}

const getToday = () => {
  const d = new Date();
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
};
const getWeekLater = () => {
  const d = new Date();
  d.setDate(d.getDate() + 7);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
};

export default function SearchForm({ className, variant = "default" }: SearchFormProps) {
  const t = useTranslations("searchForm");
  const [sameLocation, setSameLocation] = useState(true);
  const [pickupLocation, setPickupLocation] = useState("");
  const [returnLocation, setReturnLocation] = useState("");

  const [pickupDate, setPickupDate] = useState(getToday);
  const [pickupTime, setPickupTime] = useState("10:00");
  const [returnDate, setReturnDate] = useState(getWeekLater);
  const [returnTime, setReturnTime] = useState("10:00");

  const router = useRouter();
  const params = useParams();
  const locale = (params.locale as string) || "en";

  const handleSearch = useCallback(() => {
    const query = new URLSearchParams();
    query.set("pickup", pickupLocation || locations[0].value);
    query.set("return", returnLocation || pickupLocation || locations[0].value);
    query.set("pickupDate", pickupDate || getToday());
    query.set("pickupTime", pickupTime);
    query.set("returnDate", returnDate || getWeekLater());
    query.set("returnTime", returnTime);
    router.push(`/${locale}/vehicles?${query.toString()}`);
  }, [pickupLocation, returnLocation, pickupDate, pickupTime, returnDate, returnTime, router, locale]);

  const locations = [
    { key: "airport", value: "gzp" },
    { key: "airportAntalya", value: "ayt" },
    { key: "cityCenter", value: "ala" },
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

        <form className="space-y-7" onSubmit={(e) => e.preventDefault()}>
          {/* Locations Grid */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
            {/* Pickup Location */}
            <div className={cn("space-y-4", sameLocation ? "lg:col-span-2" : "")}>
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
                    "w-full h-12 pl-12 pr-10 rounded-xl",
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
                <div className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none">
                  <ChevronDown className="h-4 w-4 text-[#64748B]" aria-hidden="true" />
                </div>
              </div>
            </div>

            {/* Return Location */}
            {!sameLocation && (
              <div className="space-y-4 animate-in fade-in zoom-in duration-200">
                <label htmlFor="returnLocation" className="block text-sm font-semibold text-[#334155]">
                  {t("returnLocation")}
                </label>
                <div className="relative">
                  <MapPin className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
                  <select
                    id="returnLocation"
                    value={returnLocation}
                    onChange={(e) => setReturnLocation(e.target.value)}
                    className={cn(
                      "w-full h-12 pl-12 pr-10 rounded-xl",
                      "bg-[#F8FAFC] border border-[#E2E8F0]",
                      "text-sm text-[#0F172A]",
                      "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                      "transition-all duration-200 cursor-pointer",
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
                  <div className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none">
                    <ChevronDown className="h-4 w-4 text-[#64748B]" aria-hidden="true" />
                  </div>
                </div>
              </div>
            )}
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
                "flex h-5 w-5 items-center justify-center rounded-md border-2",
                "transition-all duration-200",
                sameLocation
                  ? "bg-[#0369A1] border-[#0369A1]"
                  : "bg-white border-[#94A3B8] group-hover:border-[#0369A1]"
              )}
            >
              {sameLocation && <Check className="h-3.5 w-3.5 text-white" />}
            </button>
            <span className="text-sm text-[#475569] group-hover:text-[#334155] transition-colors">
              {t("sameLocation")}
            </span>
          </label>

          {/* Dates Grid */}
          <div className="grid grid-cols-2 xl:grid-cols-4 gap-4">
            {/* Pickup Date */}
            <div className="space-y-4">
              <label htmlFor="pickupDate" className="block text-center text-sm font-semibold text-[#334155]">
                {t("pickupDate")}
              </label>
              <div className="relative">
                <Calendar className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4.5 w-4.5 text-[#94A3B8] pointer-events-none" />
                <input
                  id="pickupDate"
                  type="date"
                  value={pickupDate}
                  onChange={(e) => setPickupDate(e.target.value)}
                  onClick={(e) => {
                    try {
                      if ('showPicker' in HTMLInputElement.prototype) {
                        e.currentTarget.showPicker();
                      }
                    } catch (error) {
                      console.error("Picker error:", error);
                    }
                  }}
                  className={cn(
                    "w-full h-12 pl-8 pr-2 rounded-xl text-center",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-[13px] sm:text-sm tracking-tight text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer",
                    "[&::-webkit-calendar-picker-indicator]:hidden"
                  )}
                />
              </div>
            </div>

            {/* Pickup Time */}
            <div className="space-y-4">
              <label htmlFor="pickupTime" className="block text-center text-sm font-semibold text-[#334155]">
                {t("pickupTime")}
              </label>
              <div className="relative">
                <Clock className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4.5 w-4.5 text-[#94A3B8] pointer-events-none" />
                <input
                  id="pickupTime"
                  type="time"
                  value={pickupTime}
                  onChange={(e) => setPickupTime(e.target.value)}
                  onClick={(e) => {
                    try {
                      if ('showPicker' in HTMLInputElement.prototype) {
                        e.currentTarget.showPicker();
                      }
                    } catch (error) {
                      console.error("Picker error:", error);
                    }
                  }}
                  className={cn(
                    "w-full h-12 pl-8 pr-2 rounded-xl text-center",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-[13px] sm:text-sm tracking-tight text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer",
                    "[&::-webkit-calendar-picker-indicator]:hidden"
                  )}
                />
              </div>
            </div>

            {/* Return Date */}
            <div className="space-y-4">
              <label htmlFor="returnDate" className="block text-center text-sm font-semibold text-[#334155]">
                {t("returnDate")}
              </label>
              <div className="relative">
                <Calendar className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4.5 w-4.5 text-[#94A3B8] pointer-events-none" />
                <input
                  id="returnDate"
                  type="date"
                  value={returnDate}
                  onChange={(e) => setReturnDate(e.target.value)}
                  onClick={(e) => {
                    try {
                      if ('showPicker' in HTMLInputElement.prototype) {
                        e.currentTarget.showPicker();
                      }
                    } catch (error) {
                      console.error("Picker error:", error);
                    }
                  }}
                  className={cn(
                    "w-full h-12 pl-8 pr-2 rounded-xl text-center",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-[13px] sm:text-sm tracking-tight text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer",
                    "[&::-webkit-calendar-picker-indicator]:hidden"
                  )}
                />
              </div>
            </div>

            {/* Return Time */}
            <div className="space-y-4">
              <label htmlFor="returnTime" className="block text-center text-sm font-semibold text-[#334155]">
                {t("returnTime")}
              </label>
              <div className="relative">
                <Clock className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4.5 w-4.5 text-[#94A3B8] pointer-events-none" />
                <input
                  id="returnTime"
                  type="time"
                  value={returnTime}
                  onChange={(e) => setReturnTime(e.target.value)}
                  onClick={(e) => {
                    try {
                      if ('showPicker' in HTMLInputElement.prototype) {
                        e.currentTarget.showPicker();
                      }
                    } catch (error) {
                      console.error("Picker error:", error);
                    }
                  }}
                  className={cn(
                    "w-full h-12 pl-8 pr-2 rounded-xl text-center",
                    "bg-[#F8FAFC] border border-[#E2E8F0]",
                    "text-[13px] sm:text-sm tracking-tight text-[#0F172A]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200 cursor-pointer",
                    "[&::-webkit-calendar-picker-indicator]:hidden"
                  )}
                />
              </div>
            </div>
          </div>

          {/* Search Button */}
          <button
            type="button"
            onClick={handleSearch}
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
          </button>
        </form>
      </div>
    </div>
  );
}
