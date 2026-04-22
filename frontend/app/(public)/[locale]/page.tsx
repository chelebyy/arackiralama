import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";
import {
  Shield,
  Headphones,
  MapPin,
  CreditCard,
  ChevronRight,
  MessageCircle
} from "lucide-react";
import { cn } from "@/lib/utils";
import Hero from "@/components/public/Hero";
import VehicleCard from "@/components/public/VehicleCard";

const featuredVehicles = [
  {
    id: "1",
    name: "Renault Clio",
    category: "economy",
    seats: 5,
    transmission: "manual" as const,
    fuelType: "gasoline" as const,
    pricePerDay: 45,
    freeKm: 200,
  },
  {
    id: "2",
    name: "Volkswagen Golf",
    category: "compact",
    seats: 5,
    transmission: "automatic" as const,
    fuelType: "diesel" as const,
    pricePerDay: 65,
    freeKm: 250,
  },
  {
    id: "3",
    name: "BMW X5",
    category: "suv",
    seats: 7,
    transmission: "automatic" as const,
    fuelType: "diesel" as const,
    pricePerDay: 120,
    freeKm: 300,
  },
  {
    id: "4",
    name: "Mercedes S-Class",
    category: "luxury",
    seats: 5,
    transmission: "automatic" as const,
    fuelType: "hybrid" as const,
    pricePerDay: 200,
    freeKm: 300,
  },
];

const whyChooseUsIcons: Record<string, React.ComponentType<{ className?: string }>> = {
  insurance: Shield,
  delivery: MapPin,
  support: Headphones,
  payment: CreditCard,
};

const whyChooseUsKeys = ["insurance", "delivery", "support", "payment"];

export default function HomePage() {
  const t = useTranslations("home");
  const tv = useTranslations("vehicles");

  const faqs = t.raw("faq.questions") as Array<{
    id: string;
    question: string;
    answer: string;
  }>;

  return (
    <>
      {/* Hero Section */}
      <Hero />

      {/* Featured Vehicles */}
      <section className="py-[var(--space-fluid-3xl)] bg-white">
        <div className="mx-auto max-w-7xl px-[var(--space-fluid-md)] lg:px-[var(--space-fluid-lg)]">
          <div className="text-center max-w-2xl mx-auto mb-[var(--space-fluid-xl)]">
            <h2 className="text-[length:var(--text-fluid-4xl)] font-bold text-[#0F172A] mb-[var(--space-fluid-sm)]">
              {tv("title")}
            </h2>
            <p className="text-[length:var(--text-fluid-lg)] text-[#64748B]">
              {tv("subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-fluid-lg)]">
            {featuredVehicles.map((vehicle) => (
              <VehicleCard key={vehicle.id} {...vehicle} />
            ))}
          </div>

          <div className="mt-[var(--space-fluid-xl)] text-center">
            <Link
              href="/vehicles"
              className={cn(
                "inline-flex items-center gap-[var(--space-fluid-xs)] px-[var(--space-fluid-xl)] py-[var(--space-fluid-md)] rounded-xl",
                "text-base font-semibold text-[#0369A1]",
                "border-2 border-[#0369A1]",
                "hover:bg-[#0369A1] hover:text-white",
                "transition-all duration-200 cursor-pointer",
                "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-2"
              )}
            >
              {t("viewAllVehicles")}
              <ChevronRight className="h-5 w-5" />
            </Link>
          </div>
        </div>
      </section>

      {/* Why Choose Us */}
      <section className="py-[var(--space-fluid-3xl)] bg-[#F8FAFC]">
        <div className="mx-auto max-w-7xl px-[var(--space-fluid-md)] lg:px-[var(--space-fluid-lg)]">
          <div className="text-center max-w-2xl mx-auto mb-[var(--space-fluid-2xl)]">
            <h2 className="text-[length:var(--text-fluid-4xl)] font-bold text-[#0F172A] mb-[var(--space-fluid-sm)]">
              {t("whyChooseUs.title")}
            </h2>
            <p className="text-[length:var(--text-fluid-lg)] text-[#64748B]">
              {t("whyChooseUs.subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-fluid-xl)]">
            {whyChooseUsKeys.map((key) => {
              const Icon = whyChooseUsIcons[key];
              return (
                <div
                  key={key}
                  className={cn(
                    "p-[var(--space-fluid-lg)] rounded-2xl bg-white border border-[#E2E8F0]",
                    "transition-all duration-300",
                    "hover:shadow-lg hover:border-[#0369A1]/20"
                  )}
                >
                  <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-[#F0F9FF] mb-[var(--space-fluid-sm)]">
                    <Icon className="h-7 w-7 text-[#0369A1]" />
                  </div>
                  <h3 className="text-[length:var(--text-fluid-lg)] font-bold text-[#0F172A] mb-[var(--space-fluid-xs)]">
                    {t(`whyChooseUs.${key}.title`)}
                  </h3>
                  <p className="text-sm text-[#64748B] leading-relaxed">
                    {t(`whyChooseUs.${key}.description`)}
                  </p>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* FAQ Section */}
      <section id="faq" className="py-[var(--space-fluid-3xl)] bg-white">
        <div className="mx-auto max-w-7xl px-[var(--space-fluid-md)] lg:px-[var(--space-fluid-lg)]">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-[var(--space-fluid-2xl)]">
            <div className="lg:sticky lg:top-[var(--space-fluid-3xl)] lg:self-start">
              <h2 className="text-[length:var(--text-fluid-4xl)] font-bold text-[#0F172A] mb-[var(--space-fluid-sm)]">
                {t("faq.title")}
              </h2>
              <p className="text-[length:var(--text-fluid-lg)] text-[#64748B] mb-[var(--space-fluid-lg)]">
                {t("faq.subtitle")}
              </p>
              <Link
                href="/contact"
                className={cn(
                  "inline-flex items-center gap-[var(--space-fluid-xs)] px-[var(--space-fluid-lg)] py-[var(--space-fluid-sm)] rounded-xl",
                  "text-sm font-semibold text-white bg-[#0369A1]",
                  "hover:bg-[#0284C7] active:bg-[#075985]",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-2"
                )}
              >
                <MessageCircle className="h-5 w-5" />
                {t("faq.contactSupport")}
              </Link>
            </div>

            <div className="space-y-[var(--space-fluid-sm)]">
              {faqs.map((faq) => (
                <details
                  key={faq.id}
                  className="group rounded-2xl bg-[#F8FAFC] border border-[#E2E8F0] overflow-hidden"
                >
                  <summary className="flex items-center justify-between cursor-pointer p-[var(--space-fluid-lg)] list-none select-none">
                    <h3 className="text-[length:var(--text-fluid-lg)] font-bold text-[#0F172A] pr-4">
                      {faq.question}
                    </h3>
                    <span className="flex-shrink-0 flex items-center justify-center h-8 w-8 rounded-full bg-white border border-[#E2E8F0] group-open:bg-[#0369A1] group-open:border-[#0369A1] transition-colors">
                      <svg
                        className="h-4 w-4 text-[#0369A1] group-open:text-white transition-transform group-open:rotate-180"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                        strokeWidth={2}
                      >
                        <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
                      </svg>
                    </span>
                  </summary>
                  <div className="px-[var(--space-fluid-lg)] pb-[var(--space-fluid-lg)]">
                    <p className="text-sm text-[#64748B] leading-relaxed">
                      {faq.answer}
                    </p>
                  </div>
                </details>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* Contact CTA */}
      <section className="py-[var(--space-fluid-3xl)] bg-gradient-to-br from-[#0369A1] to-[#0C4A6E]">
        <div className="mx-auto max-w-7xl px-[var(--space-fluid-md)] lg:px-[var(--space-fluid-lg)]">
          <div className="text-center max-w-3xl mx-auto">
            <h2 className="text-[length:var(--text-fluid-4xl)] font-bold text-white mb-[var(--space-fluid-md)]">
              {t("cta.title")}
            </h2>
            <p className="text-[length:var(--text-fluid-xl)] text-white/80 mb-[var(--space-fluid-lg)]">
              {t("cta.subtitle")}
            </p>
            <div className="flex flex-wrap justify-center gap-[var(--space-fluid-sm)]">
              <Link
                href="/vehicles"
                className={cn(
                  "inline-flex items-center gap-[var(--space-fluid-xs)] px-[var(--space-fluid-xl)] py-[var(--space-fluid-md)] rounded-xl",
                  "text-base font-bold text-[#0369A1] bg-white",
                  "hover:bg-[#F8FAFC] active:bg-[#E2E8F0]",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-4 focus:ring-white/30",
                  "shadow-lg"
                )}
              >
                {t("cta.browseVehicles")}
                <ChevronRight className="h-5 w-5" />
              </Link>

              <Link
                href="/contact"
                className={cn(
                  "inline-flex items-center gap-[var(--space-fluid-xs)] px-[var(--space-fluid-xl)] py-[var(--space-fluid-md)] rounded-xl",
                  "text-base font-bold text-white",
                  "border-2 border-white/30 bg-white/5",
                  "hover:bg-white/10 hover:border-white/50",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-4 focus:ring-white/20"
                )}
              >
                {t("cta.contactUs")}
              </Link>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
