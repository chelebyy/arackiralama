import { useTranslations } from "next-intl";
import {
  Shield,
  Headphones,
  MapPin,
  CreditCard,
  Award,
  Car,
  Users,
  Clock,
  CheckCircle,
  Building2,
  Plane
} from "lucide-react";
import { cn } from "@/lib/utils";

export default function AboutPage() {
  const t = useTranslations("aboutUs");

  const stats = [
    { value: "15+", label: t("stats.years"), icon: Award },
    { value: "500+", label: t("stats.vehicles"), icon: Car },
    { value: "50K+", label: t("stats.customers"), icon: Users },
    { value: "24/7", label: t("stats.support"), icon: Clock }
  ];

  const values = [
    {
      id: "trust",
      icon: Shield,
      title: t("ourValues.trustTitle"),
      description: t("ourValues.trustDesc")
    },
    {
      id: "quality",
      icon: CheckCircle,
      title: t("ourValues.qualityTitle"),
      description: t("ourValues.qualityDesc")
    },
    {
      id: "service",
      icon: Headphones,
      title: t("ourValues.serviceTitle"),
      description: t("ourValues.serviceDesc")
    },
    {
      id: "innovation",
      icon: Award,
      title: t("ourValues.innovationTitle"),
      description: t("ourValues.innovationDesc")
    }
  ];

  const fleetCategories = [
    { category: "Economy", count: 120, description: t("ourFleet.c1") },
    { category: "Compact", count: 150, description: t("ourFleet.c2") },
    { category: "SUV", count: 80, description: t("ourFleet.c3") },
    { category: "Luxury", count: 50, description: t("ourFleet.c4") },
    { category: "Vans", count: 60, description: t("ourFleet.c5") },
    { category: "Convertible", count: 40, description: t("ourFleet.c6") }
  ];

  const coverageAreas = [
    { name: "Alanya City Center", type: "office", address: "Ataturk Boulevard No. 45, Alanya" },
    { name: "Gazipasa Airport (GZP)", type: "airport", distance: t("coverage.distance1") },
    { name: "Antalya Airport (AYT)", type: "airport", distance: t("coverage.distance2") },
    { name: "Mahmutlar", type: "office", address: "Barbaros Street No. 12, Mahmutlar" },
    { name: "Kestel", type: "office", address: "Ataturk Street No. 8, Kestel" },
    { name: "Konakli", type: "office", address: "Iskele Street No. 23, Konakli" }
  ];

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl lg:text-5xl font-bold text-white mb-6">
              {t("title")}
            </h1>
            <p className="text-lg lg:text-xl text-white/70">
              {t("subtitle")}
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 lg:gap-16 items-center mb-20">
          <div>
            <h2 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-6">
              {t("ourStory.title")}
            </h2>
            <div className="space-y-4 text-[#475569]">
              <p>{t("ourStory.p1")}</p>
              <p>{t("ourStory.p2")}</p>
              <p>{t("ourStory.p3")}</p>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            {stats.map((stat) => {
              const Icon = stat.icon;
              return (
                <div
                  key={stat.label}
                  className={cn(
                    "p-6 rounded-2xl bg-white border border-[#E2E8F0]",
                    "text-center transition-all duration-300 hover:shadow-lg"
                  )}
                >
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF] mx-auto mb-4">
                    <Icon className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <p className="text-3xl font-bold text-[#0F172A] mb-1">{stat.value}</p>
                  <p className="text-sm text-[#64748B]">{stat.label}</p>
                </div>
              );
            })}
          </div>
        </div>

        <div className="mb-20">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <h2 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-4">
              {t("whyChooseUs.title")}
            </h2>
            <p className="text-lg text-[#64748B]">
              {t("whyChooseUs.subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {[
              {
                id: "insurance",
                icon: Shield,
                title: t("whyChooseUs.insuranceTitle"),
                description: t("whyChooseUs.insuranceDesc")
              },
              {
                id: "delivery",
                icon: MapPin,
                title: t("whyChooseUs.deliveryTitle"),
                description: t("whyChooseUs.deliveryDesc")
              },
              {
                id: "support",
                icon: Headphones,
                title: t("whyChooseUs.supportTitle"),
                description: t("whyChooseUs.supportDesc")
              },
              {
                id: "fees",
                icon: CreditCard,
                title: t("whyChooseUs.feesTitle"),
                description: t("whyChooseUs.feesDesc")
              }
            ].map((item) => {
              const Icon = item.icon;
              return (
                <div
                  key={item.id}
                  className={cn(
                    "p-6 rounded-2xl bg-white border border-[#E2E8F0]",
                    "transition-all duration-300",
                    "hover:shadow-lg hover:border-[#0369A1]/20"
                  )}
                >
                  <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-[#F0F9FF] mb-5">
                    <Icon className="h-7 w-7 text-[#0369A1]" />
                  </div>
                  <h3 className="text-lg font-bold text-[#0F172A] mb-2">{item.title}</h3>
                  <p className="text-sm text-[#64748B] leading-relaxed">{item.description}</p>
                </div>
              );
            })}
          </div>
        </div>

        <div className="mb-20">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <h2 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-4">
              {t("ourValues.title")}
            </h2>
            <p className="text-lg text-[#64748B]">
              {t("ourValues.subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {values.map((value) => {
              const Icon = value.icon;
              return (
                <div
                  key={value.id}
                  className={cn(
                    "flex gap-5 p-6 rounded-2xl bg-white border border-[#E2E8F0]",
                    "transition-all duration-300 hover:shadow-md"
                  )}
                >
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF] flex-shrink-0">
                    <Icon className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <h3 className="text-lg font-bold text-[#0F172A] mb-2">{value.title}</h3>
                    <p className="text-sm text-[#64748B] leading-relaxed">{value.description}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="mb-20">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <h2 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-4">
              {t("ourFleet.title")}
            </h2>
            <p className="text-lg text-[#64748B]">
              {t("ourFleet.subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {fleetCategories.map((category) => (
              <div
                key={category.category}
                className={cn(
                  "p-6 rounded-2xl bg-white border border-[#E2E8F0]",
                  "flex items-center gap-4 transition-all duration-300",
                  "hover:shadow-lg hover:border-[#0369A1]/20"
                )}
              >
                <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-[#F0F9FF]">
                  <Car className="h-7 w-7 text-[#0369A1]" />
                </div>
                <div className="flex-1">
                  <h3 className="text-lg font-bold text-[#0F172A]">{category.category}</h3>
                  <p className="text-sm text-[#0369A1] font-medium">{category.count} {t("ourFleet.vehicles")}</p>
                  <p className="text-xs text-[#64748B] mt-1">{category.description}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div>
          <div className="text-center max-w-2xl mx-auto mb-12">
            <h2 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-4">
              {t("coverage.title")}
            </h2>
            <p className="text-lg text-[#64748B]">
              {t("coverage.subtitle")}
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {coverageAreas.map((area) => (
              <div
                key={area.name}
                className={cn(
                  "p-6 rounded-2xl bg-white border border-[#E2E8F0]",
                  "transition-all duration-300 hover:shadow-md"
                )}
              >
                <div className="flex items-start gap-4">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF] flex-shrink-0">
                    {area.type === "airport" ? (
                      <Plane className="h-6 w-6 text-[#0369A1]" />
                    ) : (
                      <Building2 className="h-6 w-6 text-[#0369A1]" />
                    )}
                  </div>
                  <div>
                    <h3 className="text-lg font-bold text-[#0F172A] mb-1">{area.name}</h3>
                    <p className="text-sm text-[#64748B]">
                      {area.type === "airport" ? area.distance : area.address}
                    </p>
                    {area.type === "airport" && (
                      <span className="inline-flex items-center gap-1 mt-2 px-2 py-1 rounded-lg bg-[#F0F9FF] text-xs font-medium text-[#0369A1]">
                        {t("coverage.freeDelivery")}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
