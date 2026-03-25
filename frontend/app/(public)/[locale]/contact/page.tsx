import { useTranslations } from "next-intl";
import {
  MapPin,
  Phone,
  Mail,
  Clock,
  Car,
  AlertTriangle,
  MessageCircle,
  ChevronRight
} from "lucide-react";
import { cn } from "@/lib/utils";
import ContactForm from "@/components/public/ContactForm";

const offices = [
  {
    name: "Main Office - Alanya City Center",
    address: "Ataturk Boulevard No. 45, Alanya 07400, Turkey",
    phone: "+90 242 555 10 00",
    hours: "08:00 - 20:00",
    type: "main"
  },
  {
    name: "Gazipasa Airport Desk",
    address: "Gazipasa Alanya Airport (GZP), International Arrivals",
    phone: "+90 242 555 10 01",
    hours: "24/7",
    type: "airport"
  },
  {
    name: "Antalya Airport Desk",
    address: "Antalya Airport (AYT), Terminal 2, Ground Floor",
    phone: "+90 242 555 10 02",
    hours: "24/7",
    type: "airport"
  },
  {
    name: "Mahmutlar Office",
    address: "Barbaros Street No. 12, Mahmutlar, Alanya",
    phone: "+90 242 555 10 03",
    hours: "09:00 - 19:00",
    type: "branch"
  }
];

const contactInfo = {
  reservations: "+90 242 555 10 00",
  whatsapp: "+90 555 123 45 67",
  email: "info@alanyacarrental.com",
  support: "support@alanyacarrental.com",
  emergency: "+90 555 999 00 00"
};

const workingHours = [
  { day: "Monday - Friday", hours: "08:00 - 20:00" },
  { day: "Saturday", hours: "09:00 - 18:00" },
  { day: "Sunday", hours: "10:00 - 16:00" },
  { day: "Public Holidays", hours: "10:00 - 16:00" }
];

export default function ContactPage() {
  const t = useTranslations();

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl lg:text-5xl font-bold text-white mb-6">
              Contact Us
            </h1>
            <p className="text-lg lg:text-xl text-white/70">
              We are here to help. Reach out to us for any questions or assistance.
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 lg:gap-16">
          <div>
            <h2 className="text-2xl font-bold text-[#0F172A] mb-6">
              Send Us a Message
            </h2>
            <p className="text-[#64748B] mb-8">
              Fill out the form below and we will get back to you as soon as possible. 
              For urgent matters, please call us directly.
            </p>
            <ContactForm />
          </div>

          <div className="space-y-8">
            <div>
              <h2 className="text-2xl font-bold text-[#0F172A] mb-6">
                Contact Information
              </h2>
              <div className="space-y-4">
                <div className="flex items-start gap-4 p-4 rounded-xl bg-white border border-[#E2E8F0]">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF]">
                    <Phone className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-[#0F172A] mb-1">Reservations</h3>
                    <a
                      href={`tel:${contactInfo.reservations.replace(/\s/g, "")}`}
                      className="text-[#0369A1] hover:underline"
                    >
                      {contactInfo.reservations}
                    </a>
                    <p className="text-sm text-[#64748B] mt-1">For booking inquiries</p>
                  </div>
                </div>

                <div className="flex items-start gap-4 p-4 rounded-xl bg-white border border-[#E2E8F0]">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF]">
                    <MessageCircle className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-[#0F172A] mb-1">WhatsApp</h3>
                    <a
                      href={`https://wa.me/${contactInfo.whatsapp.replace(/\s/g, "").replace(/\+/g, "")}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-[#0369A1] hover:underline"
                    >
                      {contactInfo.whatsapp}
                    </a>
                    <p className="text-sm text-[#64748B] mt-1">Quick chat support</p>
                  </div>
                </div>

                <div className="flex items-start gap-4 p-4 rounded-xl bg-white border border-[#E2E8F0]">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF]">
                    <Mail className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-[#0F172A] mb-1">Email</h3>
                    <a
                      href={`mailto:${contactInfo.email}`}
                      className="text-[#0369A1] hover:underline"
                    >
                      {contactInfo.email}
                    </a>
                    <p className="text-sm text-[#64748B] mt-1">For general inquiries</p>
                  </div>
                </div>

                <div className="flex items-start gap-4 p-4 rounded-xl bg-amber-50 border border-amber-200">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-amber-100">
                    <AlertTriangle className="h-6 w-6 text-amber-600" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-amber-800 mb-1">24/7 Emergency Line</h3>
                    <a
                      href={`tel:${contactInfo.emergency.replace(/\s/g, "")}`}
                      className="text-amber-700 font-bold hover:underline"
                    >
                      {contactInfo.emergency}
                    </a>
                    <p className="text-sm text-amber-600 mt-1">
                      Roadside assistance and emergencies only
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div>
              <h2 className="text-xl font-bold text-[#0F172A] mb-4">
                Working Hours
              </h2>
              <div className="p-4 rounded-xl bg-white border border-[#E2E8F0]">
                <div className="flex items-center gap-2 mb-4">
                  <Clock className="h-5 w-5 text-[#0369A1]" />
                  <span className="font-semibold text-[#0F172A]">Main Office</span>
                </div>
                <ul className="space-y-2">
                  {workingHours.map((item) => (
                    <li
                      key={item.day}
                      className="flex justify-between text-sm py-2 border-b border-[#F1F5F9] last:border-0 last:pb-0"
                    >
                      <span className="text-[#64748B]">{item.day}</span>
                      <span className="font-medium text-[#0F172A]">{item.hours}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
        </div>

        <div className="mt-16">
          <h2 className="text-2xl font-bold text-[#0F172A] mb-8 text-center">
            Our Locations
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {offices.map((office) => (
              <div
                key={office.name}
                className={cn(
                  "p-6 rounded-2xl bg-white border border-[#E2E8F0]",
                  "transition-all duration-300 hover:shadow-lg",
                  office.type === "main" && "border-[#0369A1] ring-1 ring-[#0369A1]/20"
                )}
              >
                {office.type === "main" && (
                  <span className="inline-block px-3 py-1 rounded-lg bg-[#0369A1] text-white text-xs font-semibold mb-4">
                    Main Office
                  </span>
                )}
                {office.type === "airport" && (
                  <span className="inline-block px-3 py-1 rounded-lg bg-[#F0F9FF] text-[#0369A1] text-xs font-semibold mb-4">
                    Airport
                  </span>
                )}
                <h3 className="text-lg font-bold text-[#0F172A] mb-3">{office.name}</h3>
                <div className="space-y-3 text-sm">
                  <div className="flex items-start gap-2">
                    <MapPin className="h-4 w-4 text-[#0369A1] flex-shrink-0 mt-0.5" />
                    <span className="text-[#64748B]">{office.address}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Phone className="h-4 w-4 text-[#0369A1] flex-shrink-0" />
                    <a
                      href={`tel:${office.phone.replace(/\s/g, "")}`}
                      className="text-[#0369A1] hover:underline"
                    >
                      {office.phone}
                    </a>
                  </div>
                  <div className="flex items-center gap-2">
                    <Clock className="h-4 w-4 text-[#0369A1] flex-shrink-0" />
                    <span className="text-[#64748B]">{office.hours}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-16">
          <div className="rounded-2xl overflow-hidden border border-[#E2E8F0] bg-[#E2E8F0] h-96 flex items-center justify-center">
            <div className="text-center">
              <MapPin className="h-12 w-12 text-[#64748B] mx-auto mb-4" />
              <p className="text-lg font-medium text-[#64748B]">Map Placeholder</p>
              <p className="text-sm text-[#94A3B8] mt-1">
                Interactive map showing all office locations
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
