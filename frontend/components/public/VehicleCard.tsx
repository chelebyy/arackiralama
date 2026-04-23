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
  readonly id: string;
  readonly name: string;
  readonly category: string;
  readonly image?: string;
  readonly seats: number;
  readonly doors?: number;
  readonly transmission: "manual" | "automatic";
  readonly fuelType: "gasoline" | "diesel" | "hybrid";
  readonly airConditioning?: boolean;
  readonly pricePerDay: number;
  readonly totalPrice?: number;
  readonly days?: number;
  readonly freeKm?: number;
  readonly isAvailable?: boolean;
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

  return (
    <div
      className={cn(
        "@container group relative rounded-2xl bg-white border border-[#E2E8F0]",
        "overflow-hidden transition-all duration-300",
        "hover:shadow-xl hover:border-[#0369A1]/30 flex flex-col h-full",
        !isAvailable && "opacity-70"
      )}
    >
      {/* Image Container */}
      <div className="relative aspect-[16/10] bg-gradient-to-br from-[#F1F5F9] to-[#E2E8F0] overflow-hidden shrink-0">
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

        {/* Top Badges */}
        <div className="absolute top-[var(--space-fluid-sm)] left-0 right-0 px-[var(--space-fluid-sm)] flex justify-between items-start gap-[var(--space-fluid-xs)] overflow-hidden pointer-events-none">
          <span className="px-[var(--space-fluid-xs)] py-1 rounded-lg text-[10px] @sm:text-xs font-semibold bg-white/90 backdrop-blur-sm text-[#0369A1] shadow-sm whitespace-nowrap truncate max-w-[50%]">
            {t(`categories.${category}`)}
          </span>
          <span className="flex items-center gap-1 px-[var(--space-fluid-xs)] py-1 rounded-lg text-[10px] @sm:text-xs font-medium bg-[#10B981] text-white shadow-sm whitespace-nowrap truncate max-w-[50%]">
            <Check className="h-3 w-3 shrink-0" />
            <span className="truncate">{t("freeCancellation")}</span>
          </span>
        </div>
      </div>

      {/* Content */}
      <div className="p-[var(--space-fluid-sm)] flex flex-col flex-1 space-y-[var(--space-fluid-sm)]">
        {/* Name */}
        <h3 className="text-[length:var(--text-fluid-lg)] font-bold text-[#0F172A] truncate">
          {name}
        </h3>

        {/* Features */}
        <div className="flex flex-wrap gap-[var(--space-fluid-xs)]">
          <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
            <Users className="h-3.5 w-3.5 text-[#0369A1]" />
            {seats} {t("features.seats")}
          </div>
          <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
            <Gauge className="h-3.5 w-3.5 text-[#0369A1]" />
            {t(`features.${transmission}`)}
          </div>
          <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
            <Fuel className="h-3.5 w-3.5 text-[#0369A1]" />
            {t(`features.${fuelType}`)}
          </div>
          {airConditioning && (
            <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
              <Snowflake className="h-3.5 w-3.5 text-[#0369A1]" />
              {t("features.airConditioning")}
            </div>
          )}
        </div>

        {/* Free KM */}
        <div className="text-[length:var(--text-fluid-sm)] text-[#64748B]">
          {t("freeKm", { km: freeKm })}
        </div>

        {/* Price & CTA */}
        <div className="pt-[var(--space-fluid-sm)] mt-auto border-t border-[#E2E8F0] flex flex-col gap-[var(--space-fluid-xs)]">
          <div className="flex items-baseline justify-between gap-2">
            <div className="flex items-baseline gap-1">
              <span className="text-[length:var(--text-fluid-xl)] font-bold text-[#0F172A] tracking-tight">
                ₺ {pricePerDay}
              </span>
              <span className="text-[length:var(--text-fluid-sm)] text-[#64748B]">
                /{t("pricePerDay")}
              </span>
            </div>
            {days > 1 && totalPrice && (
              <div className="text-[length:var(--text-fluid-sm)] text-[#64748B] whitespace-nowrap">
                {t("totalPrice")}: ₺ {totalPrice}
              </div>
            )}
          </div>

          {isAvailable ? (
            <Link
              href={{ pathname: "/vehicles/[id]", params: { id } }}
              className={cn(
                "px-[var(--space-fluid-sm)] py-[var(--space-fluid-xs)] rounded-xl text-[length:var(--text-fluid-sm)] font-bold text-center",
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
                "px-[var(--space-fluid-sm)] py-[var(--space-fluid-xs)] rounded-xl text-[length:var(--text-fluid-sm)] font-bold text-center",
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
