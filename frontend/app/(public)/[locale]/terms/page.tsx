import { useTranslations } from "next-intl";
import {
  FileText,
  Shield,
  CreditCard,
  Fuel,
  AlertCircle,
  CheckCircle,
  ChevronRight,
  Car,
  User,
  Clock
} from "lucide-react";
import { cn } from "@/lib/utils";

const sections = [
  {
    id: "agreement",
    icon: FileText,
    title: "Rental Agreement",
    content: [
      "By renting a vehicle from Alanya Car Rental, you agree to these terms and conditions.",
      "The rental agreement is a legally binding contract between the renter and Alanya Car Rental.",
      "All rentals are subject to vehicle availability and confirmation.",
      "We reserve the right to refuse service to anyone for any reason.",
      "The renter is responsible for reading and understanding all terms before signing the agreement."
    ]
  },
  {
    id: "age",
    icon: User,
    title: "Age and License Requirements",
    content: [
      "Minimum age for renters is 25 years old.",
      "Drivers must have held a valid driver's license for at least 2 years.",
      "All drivers must present a valid driver's license at the time of pickup.",
      "International visitors must have an International Driving Permit (IDP) if their license is not in Latin alphabet.",
      "Additional drivers must meet the same age and license requirements and be registered on the rental agreement."
    ]
  },
  {
    id: "insurance",
    icon: Shield,
    title: "Insurance and Coverage",
    content: [
      "All rentals include basic third-party liability insurance as required by Turkish law.",
      "Collision Damage Waiver (CDW) is included with all rentals, reducing the renter's liability for damage.",
      "Theft protection is included, covering the vehicle in case of theft.",
      "A security deposit of 300-500 USD is required depending on vehicle category.",
      "The security deposit is refunded within 7-14 days after vehicle return, pending damage inspection."
    ]
  },
  {
    id: "payment",
    icon: CreditCard,
    title: "Payment Terms",
    content: [
      "Full payment is required at the time of booking confirmation.",
      "We accept major credit cards (Visa, MasterCard, American Express) and cash payments.",
      "A valid credit card in the renter's name is required for the security deposit.",
      "Additional charges may apply for fuel, tolls, parking fines, traffic violations, or vehicle damage.",
      "Late return fees are charged at 50% of the daily rate per hour after the agreed return time."
    ]
  },
  {
    id: "fuel",
    icon: Fuel,
    title: "Fuel Policy",
    content: [
      "Vehicles are provided with a full tank of fuel.",
      "Renters must return the vehicle with a full tank of fuel.",
      "If the vehicle is returned with less fuel, a refueling charge will apply at current market rates plus a service fee.",
      "We offer a pre-purchase fuel option for convenience at competitive rates."
    ]
  },
  {
    id: "cancellation",
    icon: Clock,
    title: "Cancellation and Modification Policy",
    content: [
      "Cancellations made 48 hours or more before pickup: Full refund minus processing fee.",
      "Cancellations made 24-48 hours before pickup: 50% refund.",
      "Cancellations made less than 24 hours before pickup: No refund.",
      "No-shows are charged the full rental amount.",
      "Modifications to reservations are subject to availability and may incur additional charges."
    ]
  },
  {
    id: "usage",
    icon: Car,
    title: "Vehicle Usage",
    content: [
      "Vehicles may only be driven in Turkey unless prior written authorization is obtained.",
      "Off-road driving is prohibited unless the vehicle is specifically authorized for such use.",
      "Smoking is strictly prohibited in all rental vehicles. A cleaning fee will be charged for violations.",
      "Pets are allowed only in pet-friendly vehicles with prior arrangement.",
      "The renter is responsible for all traffic violations, parking fines, and tolls incurred during the rental period."
    ]
  },
  {
    id: "liability",
    icon: AlertCircle,
    title: "Renter Liability",
    content: [
      "The renter is liable for any damage caused by negligence or violation of rental terms.",
      "Damage caused by unauthorized drivers is not covered by insurance.",
      "The renter must report any accidents or damage immediately to our emergency line.",
      "Failure to report damage may result in loss of insurance coverage and full liability.",
      "Personal belongings left in the vehicle are not covered by our insurance."
    ]
  }
];

export default function TermsPage() {
  const t = useTranslations();

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl lg:text-5xl font-bold text-white mb-6">
              Terms and Conditions
            </h1>
            <p className="text-lg lg:text-xl text-white/70">
              Please read these terms carefully before renting a vehicle
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="bg-amber-50 border border-amber-200 rounded-2xl p-6 mb-12">
          <div className="flex items-start gap-4">
            <AlertCircle className="h-6 w-6 text-amber-600 flex-shrink-0 mt-0.5" />
            <div>
              <h2 className="font-semibold text-amber-800 mb-2">Important Notice</h2>
              <p className="text-amber-700 text-sm leading-relaxed">
                By making a reservation or renting a vehicle from Alanya Car Rental, 
                you acknowledge that you have read, understood, and agree to be bound by these 
                terms and conditions. These terms may be updated from time to time, and the 
                current version will always be available on our website.
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-8">
          {sections.map((section, index) => {
            const Icon = section.icon;
            return (
              <section
                key={section.id}
                id={section.id}
                className={cn(
                  "p-6 lg:p-8 rounded-2xl bg-white border border-[#E2E8F0]",
                  "transition-all duration-300 hover:shadow-md"
                )}
              >
                <div className="flex items-start gap-4 mb-6">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF] flex-shrink-0">
                    <Icon className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <span className="text-sm font-medium text-[#0369A1]">
                      Section {String(index + 1).padStart(2, "0")}
                    </span>
                    <h2 className="text-xl font-bold text-[#0F172A]">
                      {section.title}
                    </h2>
                  </div>
                </div>

                <ul className="space-y-3">
                  {section.content.map((item) => (
                    <li
                      key={item.slice(0, 30)}
                      className="flex items-start gap-3 text-[#475569]"
                    >
                      <CheckCircle className="h-5 w-5 text-[#0369A1] flex-shrink-0 mt-0.5" />
                      <span className="leading-relaxed">{item}</span>
                    </li>
                  ))}
                </ul>
              </section>
            );
          })}
        </div>

        <div className="mt-12 p-6 rounded-2xl bg-[#0F172A] text-white">
          <h2 className="text-xl font-bold mb-4">Questions About Our Terms?</h2>
          <p className="text-white/70 mb-6">
            If you have any questions about our terms and conditions, please do not hesitate 
            to contact our customer service team. We are here to help clarify any concerns 
            before you make a reservation.
          </p>
          <div className="flex flex-wrap gap-4">
            <a
              href="mailto:legal@alanyacarrental.com"
              className={cn(
                "inline-flex items-center gap-2 px-6 py-3 rounded-xl",
                "text-sm font-semibold bg-white text-[#0369A1]",
                "hover:bg-[#F8FAFC] transition-all duration-200"
              )}
            >
              Contact Legal Team
            </a>
            <a
              href="tel:+905555550100"
              className={cn(
                "inline-flex items-center gap-2 px-6 py-3 rounded-xl",
                "text-sm font-semibold bg-white/10 text-white border border-white/20",
                "hover:bg-white/20 transition-all duration-200"
              )}
            >
              Call Customer Service
            </a>
          </div>
        </div>

        <div className="mt-12 text-center">
          <p className="text-sm text-[#64748B]">
            Last updated: March 2025
          </p>
        </div>
      </div>
    </div>
  );
}
