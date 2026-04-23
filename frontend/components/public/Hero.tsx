import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";
import {
  Car,
  Shield,
  Headphones,
  Tag,
  Star,
  ArrowRight
} from "lucide-react";
import { cn } from "@/lib/utils";
import SearchForm from "./SearchForm";

export default function Hero() {
  const t = useTranslations("hero");

  const trustBadges = [
    { icon: Shield, key: "insurance" },
    { icon: Headphones, key: "support" },
    { icon: Tag, key: "price" },
    { icon: Car, key: "delivery" },
  ];

  return (
    <section className="relative w-full overflow-hidden bg-gradient-to-br from-[#0F172A] via-[#1E293B] to-[#0F172A]">
      {/* Background Pattern */}
      <div className="absolute inset-0 opacity-10">
        <div
          className="absolute inset-0"
          style={{
            backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23FFFFFF' fill-opacity='0.4'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E")`,
          }}
        />
      </div>

      {/* Gradient Overlay */}
      <div className="absolute inset-0 bg-gradient-to-t from-[#0F172A] via-transparent to-transparent" />

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="py-16 lg:py-24">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 lg:gap-16 items-center">
            {/* Left Content */}
            <div className="space-y-8">
              {/* Trust Badge */}
              <div className="inline-flex items-center gap-3 px-4 py-2 rounded-full bg-white/10 backdrop-blur-sm border border-white/20">
                <div className="flex items-center gap-0.5">
                  {[1, 2, 3, 4, 5].map((i) => (
                    <Star key={i} className="h-4 w-4 fill-yellow-400 text-yellow-400" />
                  ))}
                </div>
                <span className="text-sm font-medium text-white/90">
                  {t("trustBadge")}
                </span>
              </div>

              {/* Headline */}
              <h1 className="text-4xl sm:text-5xl lg:text-6xl font-bold text-white leading-tight">
                {t("headline")}
              </h1>

              {/* Subtitle */}
              <p className="text-lg text-white/70 max-w-xl leading-relaxed">
                {t("subtitle")}
              </p>

              {/* CTA Buttons */}
              <div className="flex flex-wrap gap-4">
                <Link
                  href="/vehicles"
                  className={cn(
                    "inline-flex items-center gap-2 px-8 py-4 rounded-xl",
                    "text-base font-bold text-[#0F172A] bg-white",
                    "hover:bg-[#F8FAFC] active:bg-[#E2E8F0]",
                    "transition-all duration-200 cursor-pointer",
                    "focus:outline-none focus:ring-4 focus:ring-white/30",
                    "shadow-lg hover:shadow-xl"
                  )}
                >
                  {t("ctaPrimary")}
                  <ArrowRight className="h-5 w-5" />
                </Link>

                <Link
                  href="/booking"
                  className={cn(
                    "inline-flex items-center gap-2 px-8 py-4 rounded-xl",
                    "text-base font-bold text-white",
                    "border-2 border-white/30 bg-white/5 backdrop-blur-sm",
                    "hover:bg-white/10 hover:border-white/50",
                    "transition-all duration-200 cursor-pointer",
                    "focus:outline-none focus:ring-4 focus:ring-white/20"
                  )}
                >
                  {t("ctaSecondary")}
                </Link>
              </div>

              {/* Trust Badges */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 pt-4">
                {trustBadges.map((badge) => {
                  const Icon = badge.icon;
                  return (
                    <div
                      key={badge.key}
                      className="flex items-center gap-2 text-white/60"
                    >
                      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-white/10">
                        <Icon className="h-5 w-5 text-[#38BDF8]" />
                      </div>
                      <span className="text-xs font-medium leading-tight">
                        {t(`features.${badge.key}`)}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Right Content - Search Form */}
            <div className="lg:justify-self-end w-full max-w-xl">
              <SearchForm variant="hero" />
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
