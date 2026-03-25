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

const whyChooseUs = [
  {
    id: "insurance",
    icon: Shield,
    title: "Comprehensive Insurance",
    description: "All our vehicles come with full insurance coverage for your peace of mind.",
  },
  {
    id: "delivery",
    icon: MapPin,
    title: "Free Delivery",
    description: "We deliver your car to your hotel, airport, or any location in Alanya.",
  },
  {
    id: "support",
    icon: Headphones,
    title: "24/7 Support",
    description: "Our multilingual support team is available round the clock to assist you.",
  },
  {
    id: "payment",
    icon: CreditCard,
    title: "Secure Payment",
    description: "Pay securely online with credit card or choose cash on delivery.",
  },
];

const faqs = [
  {
    id: "documents",
    question: "What documents do I need to rent a car?",
    answer: "You need a valid driver's license, passport or ID card, and a credit card for the security deposit. International visitors should have an International Driving Permit if their license is not in Latin alphabet.",
  },
  {
    id: "age",
    question: "Is there a minimum age requirement?",
    answer: "Yes, drivers must be at least 25 years old with 2 years of driving experience. Additional fees may apply for drivers under 25.",
  },
  {
    id: "airport",
    question: "Can I pick up the car at the airport?",
    answer: "Absolutely! We offer free delivery to Gazipasa Airport (GZP) and Antalya Airport (AYT). Our representative will meet you at the arrivals with your car.",
  },
  {
    id: "included",
    question: "What is included in the rental price?",
    answer: "The price includes comprehensive insurance, free daily mileage (200-300 km depending on vehicle), 24/7 roadside assistance, and free delivery within Alanya.",
  },
  {
    id: "cancel",
    question: "Can I modify or cancel my reservation?",
    answer: "Yes, you can modify or cancel your reservation free of charge up to 48 hours before pickup. Cancellations within 48 hours may incur a fee.",
  },
];

export default function HomePage() {
  const t = useTranslations();

  return (
    <>
      {/* Hero Section */}
      <Hero />

      {/* Featured Vehicles */}
      <section className="py-16 lg:py-24 bg-white">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <h2 className="text-3xl lg:text-4xl font-bold text-[#0F172A] mb-4">
              {t("vehicles.title")}
            </h2>
            <p className="text-lg text-[#64748B]">
              {t("vehicles.subtitle")}
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
              View All Vehicles
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
              Why Choose Us
            </h2>
            <p className="text-lg text-[#64748B]">
              We are committed to providing the best car rental experience in Alanya
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-8">
            {whyChooseUs.map((item) => {
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
                  <h3 className="text-lg font-bold text-[#0F172A] mb-2">
                    {item.title}
                  </h3>
                  <p className="text-sm text-[#64748B] leading-relaxed">
                    {item.description}
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
                Frequently Asked Questions
              </h2>
              <p className="text-lg text-[#64748B] mb-8">
                Find answers to common questions about our car rental services. Can&apos;t find what you&apos;re looking for? Contact our support team.
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
                Contact Support
              </Link>
            </div>

            <div className="space-y-4">
              {faqs.map((faq) => (
                <div
                  key={faq.id}
                  className="p-6 rounded-2xl bg-[#F8FAFC] border border-[#E2E8F0]"
                >
                  <h3 className="text-lg font-bold text-[#0F172A] mb-3">
                    {faq.question}
                  </h3>
                  <p className="text-sm text-[#64748B] leading-relaxed">
                    {faq.answer}
                  </p>
                </div>
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
              Ready to Explore Alanya?
            </h2>
            <p className="text-xl text-white/80 mb-8">
              Book your car now and start your adventure. Our team is ready to help you find the perfect vehicle.
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
                Browse Vehicles
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
                Contact Us
              </Link>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
