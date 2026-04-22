import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";
import {
  Users,
  Fuel,
  Gauge,
  Snowflake,
  Check,
  Car
} from "lucide-react";
import { cn } from "@/lib/utils";

interface VehicleCardProps {
  id: string;
  name: string;
  category: string;
  image?: string;
  seats: number;
  doors?: number;
  transmission: "manual" | "automatic";
  fuelType: "gasoline" | "diesel" | "hybrid";
  airConditioning?: boolean;
  pricePerDay: number;
  totalPrice?: number;
  days?: number;
  freeKm?: number;
  isAvailable?: boolean;
}

export default function VehicleCard({
  id,
  name,
  category,
  image,
  seats,
  doors = 4,
  transmission,
  fuelType,
  airConditioning = true,
  pricePerDay,
  totalPrice,
  days = 1,
  freeKm = 200,
  isAvailable = true,
}: VehicleCardProps) {
  const t = useTranslations("vehicles");

  const displayPrice = totalPrice ?? pricePerDay * days;

  return (
    <div
      className={cn(
        "group relative rounded-2xl bg-white border border-[#E2E8F0]",
        "overflow-hidden transition-all duration-300",
        "hover:shadow-xl hover:border-[#0369A1]/30",
        !isAvailable && "opacity-70"
      )}
    >
      {/* Image Container */}
      <div className="relative aspect-[16/10] bg-gradient-to-br from-[#F1F5F9] to-[#E2E8F0] overflow-hidden">
        {image ? (
          <img
            src={image}
            alt={name}
            className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105"
          />
        ) : (
          <div className="flex items-center justify-center h-full">
            <Car className="w-24 h-24 text-[#CBD5E1]" aria-hidden="true" />
          </div>
        )}

        {/* Category Badge */}
        <div className="absolute top-4 left-4">
          <span className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-white/90 backdrop-blur-sm text-[#0369A1] shadow-sm">
            {t(`categories.${category}`)}
          </span>
        </div>

        {/* Free Cancellation Badge */}
        <div className="absolute top-4 right-4">
          <span className="flex items-center gap-1 px-3 py-1.5 rounded-lg text-xs font-medium bg-[#10B981] text-white shadow-sm">
            <Check className="h-3 w-3" />
            {t("freeCancellation")}
          </span>
        </div>
      </div>

      {/* Content */}
      <div className="p-5 space-y-4">
        {/* Name */}
        <h3 className="text-lg font-bold text-[#0F172A] truncate">
          {name}
        </h3>

        {/* Features */}
        <div className="flex flex-wrap gap-2">
          <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-[#F8FAFC] text-xs text-[#475569]">
            <Users className="h-3.5 w-3.5 text-[#0369A1]" />
            {seats} {t("features.seats")}
          </div>
          <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-[#F8FAFC] text-xs text-[#475569]">
            <Gauge className="h-3.5 w-3.5 text-[#0369A1]" />
            {t(`features.${transmission}`)}
          </div>
          <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-[#F8FAFC] text-xs text-[#475569]">
            <Fuel className="h-3.5 w-3.5 text-[#0369A1]" />
            {t(`features.${fuelType}`)}
          </div>
          {airConditioning && (
            <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-[#F8FAFC] text-xs text-[#475569]">
              <Snowflake className="h-3.5 w-3.5 text-[#0369A1]" />
              {t("features.airConditioning")}
            </div>
          )}
        </div>

        {/* Free KM */}
        <div className="text-xs text-[#64748B]">
          {t("freeKm", { km: freeKm })}
        </div>

        {/* Price & CTA */}
        <div className="pt-4 border-t border-[#E2E8F0] flex items-center justify-between">
          <div className="space-y-0.5">
            <div className="flex items-baseline gap-1">
              <span className="text-2xl font-bold text-[#0F172A]">
                €{pricePerDay}
              </span>
              <span className="text-sm text-[#64748B]">
                /{t("pricePerDay")}
              </span>
            </div>
            {days > 1 && totalPrice && (
              <div className="text-xs text-[#64748B]">
                {t("totalPrice")}: €{totalPrice}
              </div>
            )}
          </div>

          {isAvailable ? (
            <Link
              href={{ pathname: "/vehicles/[id]", params: { id } }}
              className={cn(
                "px-4 py-3 rounded-xl text-sm font-bold whitespace-nowrap",
                "transition-all duration-200",
                "focus:outline-none focus:ring-2 focus:ring-offset-2",
                "text-white bg-[#0369A1]",
                "hover:bg-[#0284C7] active:bg-[#075985]",
                "cursor-pointer focus:ring-[#0369A1]",
                "shadow-md hover:shadow-lg"
              )}
            >
              {t("bookNow")}
            </Link>
          ) : (
            <span
              className={cn(
                "px-6 py-3 rounded-xl text-sm font-bold",
                "text-[#94A3B8] bg-[#F1F5F9]",
                "cursor-not-allowed"
              )}
            >
              {t("unavailable")}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
