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
  ChevronRight,
  Building2,
  Plane
} from "lucide-react";
import { cn } from "@/lib/utils";

const stats = [
  { value: "15+", label: "Years Experience", icon: Award },
  { value: "500+", label: "Vehicles in Fleet", icon: Car },
  { value: "50K+", label: "Happy Customers", icon: Users },
  { value: "24/7", label: "Customer Support", icon: Clock }
];

const values = [
  {
    id: "trust",
    icon: Shield,
    title: "Trust & Transparency",
    description: "We believe in honest pricing with no hidden fees. What you see is what you pay."
  },
  {
    id: "quality",
    icon: CheckCircle,
    title: "Quality First",
    description: "Every vehicle in our fleet is meticulously maintained and regularly serviced."
  },
  {
    id: "service",
    icon: Headphones,
    title: "Exceptional Service",
    description: "Our multilingual team is dedicated to making your rental experience seamless."
  },
  {
    id: "innovation",
    icon: Award,
    title: "Continuous Innovation",
    description: "We constantly improve our services to meet the evolving needs of our customers."
  }
];

const fleetCategories = [
  { category: "Economy", count: 120, description: "Fuel-efficient options for budget travelers" },
  { category: "Compact", count: 150, description: "Perfect balance of comfort and efficiency" },
  { category: "SUV", count: 80, description: "Spacious vehicles for families and groups" },
  { category: "Luxury", count: 50, description: "Premium vehicles for special occasions" },
  { category: "Vans", count: 60, description: "Large capacity for group transportation" },
  { category: "Convertible", count: 40, description: "Experience Alanya with the top down" }
];

const coverageAreas = [
  { name: "Alanya City Center", type: "office", address: "Ataturk Boulevard No. 45, Alanya" },
  { name: "Gazipasa Airport (GZP)", type: "airport", distance: "45 km from Alanya center" },
  { name: "Antalya Airport (AYT)", type: "airport", distance: "125 km from Alanya center" },
  { name: "Mahmutlar", type: "office", address: "Barbaros Street No. 12, Mahmutlar" },
  { name: "Kestel", type: "office", address: "Ataturk Street No. 8, Kestel" },
  { name: "Konakli", type: "office", address: "Iskele Street No. 23, Konakli" }
];

export default function AboutPage() {
  const t = useTranslations();

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl lg:text-5xl font-bold text-white mb-6">
              About Alanya Car Rental
            </h1>
            <p className="text-lg lg:text-xl text-white/70">
              Your trusted partner for car rentals in Alanya since 2008
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 lg:gap-16 items-center mb-20">
          <div>
            <h2 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-6">
              Our Story
            </h2>
            <div className="space-y-4 text-[#475569]">
              <p>
                Founded in 2008, Alanya Car Rental began with a simple mission: to provide visitors 
                to the Turkish Riviera with reliable, affordable, and hassle-free transportation. 
                What started as a small family business with just 5 vehicles has grown into one of 
                the most trusted car rental companies in the region.
              </p>
              <p>
                Over the past 15 years, we have served over 50,000 customers from around the world, 
                helping them explore the beautiful Alanya region and beyond. Our commitment to 
                transparency, quality service, and customer satisfaction has earned us a reputation 
                as the go-to car rental service for tourists and locals alike.
              </p>
              <p>
                Today, with a fleet of over 500 vehicles and multiple locations across Alanya, 
                we continue to uphold our founding values while embracing modern technology to 
                make the rental process smoother than ever.
              </p>
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
              Why Choose Us
            </h2>
            <p className="text-lg text-[#64748B]">
              We are committed to providing the best car rental experience in Alanya
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {[
              {
                id: "insurance",
                icon: Shield,
                title: "Comprehensive Insurance",
                description: "All our vehicles come with full insurance coverage including CDW and theft protection."
              },
              {
                id: "delivery",
                icon: MapPin,
                title: "Free Delivery",
                description: "We deliver your car to your hotel, airport, or any location in Alanya at no extra cost."
              },
              {
                id: "support",
                icon: Headphones,
                title: "24/7 Support",
                description: "Our multilingual support team is available round the clock to assist you."
              },
              {
                id: "fees",
                icon: CreditCard,
                title: "No Hidden Fees",
                description: "Transparent pricing with no surprises. The price you see is the price you pay."
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
              Our Values
            </h2>
            <p className="text-lg text-[#64748B]">
              The principles that guide everything we do
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
              Our Fleet
            </h2>
            <p className="text-lg text-[#64748B]">
              A vehicle for every need and budget
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
                  <p className="text-sm text-[#0369A1] font-medium">{category.count} vehicles</p>
                  <p className="text-xs text-[#64748B] mt-1">{category.description}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div>
          <div className="text-center max-w-2xl mx-auto mb-12">
            <h2 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-4">
              Coverage Areas
            </h2>
            <p className="text-lg text-[#64748B]">
              Multiple locations for your convenience
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
                        Free Delivery
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
