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
      <section className="py-16 lg:py-24 bg-white">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <h2 className="text-3xl lg:text-4xl font-bold text-[#0F172A] mb-4">
              {tv("title")}
            </h2>
            <p className="text-lg text-[#64748B]">
              {tv("subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {featuredVehicles.map((vehicle) => (
              <VehicleCard key={vehicle.id} {...vehicle} />
            ))}
          </div>

          <div className="mt-12 text-center">
            <Link
              href="/vehicles"
              className={cn(
                "inline-flex items-center gap-2 px-8 py-4 rounded-xl",
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
      <section className="py-16 lg:py-24 bg-[#F8FAFC]">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto mb-16">
            <h2 className="text-3xl lg:text-4xl font-bold text-[#0F172A] mb-4">
              {t("whyChooseUs.title")}
            </h2>
            <p className="text-lg text-[#64748B]">
              {t("whyChooseUs.subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-8">
            {whyChooseUsKeys.map((key) => {
              const Icon = whyChooseUsIcons[key];
              return (
                <div
                  key={key}
                  className={cn(
                    "p-6 rounded-2xl bg-white border border-[#E2E8F0]",
                    "transition-all duration-300",
                    "hover:shadow-lg hover:border-[#0369A1]/20"
                  )}
                >
                  <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-[#F0F9FF] mb-5">
                    <Icon className="h-7 w-7 text-[#0369A1]" />
                  </div>
                  <h3 className="text-lg font-bold text-[#0F172A] mb-2">
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
      <section id="faq" className="py-16 lg:py-24 bg-white">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 lg:gap-16">
            <div className="lg:sticky lg:top-32 lg:self-start">
              <h2 className="text-3xl lg:text-4xl font-bold text-[#0F172A] mb-4">
                {t("faq.title")}
              </h2>
              <p className="text-lg text-[#64748B] mb-8">
                {t("faq.subtitle")}
              </p>
              <Link
                href="/contact"
                className={cn(
                  "inline-flex items-center gap-2 px-6 py-3 rounded-xl",
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

            <div className="space-y-4">
              {faqs.map((faq) => (
                <details
                  key={faq.id}
                  className="group rounded-2xl bg-[#F8FAFC] border border-[#E2E8F0] overflow-hidden"
                >
                  <summary className="flex items-center justify-between cursor-pointer p-6 list-none select-none">
                    <h3 className="text-lg font-bold text-[#0F172A] pr-4">
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
                  <div className="px-6 pb-6">
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
      <section className="py-16 lg:py-24 bg-gradient-to-br from-[#0369A1] to-[#0C4A6E]">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h2 className="text-3xl lg:text-4xl font-bold text-white mb-6">
              {t("cta.title")}
            </h2>
            <p className="text-xl text-white/80 mb-8">
              {t("cta.subtitle")}
            </p>
            <div className="flex flex-wrap justify-center gap-4">
              <Link
                href="/vehicles"
                className={cn(
                  "inline-flex items-center gap-2 px-8 py-4 rounded-xl",
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
                  "inline-flex items-center gap-2 px-8 py-4 rounded-xl",
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
