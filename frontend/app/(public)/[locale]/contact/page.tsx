import { useTranslations } from "next-intl";
import {
  MapPin,
  Phone,
  Mail,
  Clock,
  AlertTriangle,
  MessageCircle
} from "lucide-react";
import { cn } from "@/lib/utils";
import ContactForm from "@/components/public/ContactForm";

const contactInfo = {
  reservations: "+90 242 555 10 00",
  whatsapp: "+90 555 123 45 67",
  email: "info@alanyacarrental.com",
  support: "support@alanyacarrental.com",
  emergency: "+90 555 999 00 00"
};

export default function ContactPage() {
  const t = useTranslations("contactUs");

  const offices = [
    {
      name: t("offices.main.name"),
      address: t("offices.main.address"),
      phone: "+90 242 555 10 00",
      hours: "08:00 - 20:00",
      type: "main"
    },
    {
      name: t("offices.gzp.name"),
      address: t("offices.gzp.address"),
      phone: "+90 242 555 10 01",
      hours: "24/7",
      type: "airport"
    },
    {
      name: t("offices.ayt.name"),
      address: t("offices.ayt.address"),
      phone: "+90 242 555 10 02",
      hours: "24/7",
      type: "airport"
    },
    {
      name: t("offices.mahmutlar.name"),
      address: t("offices.mahmutlar.address"),
      phone: "+90 242 555 10 03",
      hours: "09:00 - 19:00",
      type: "branch"
    }
  ];

  const workingHours = [
    { day: t("days.mondayFriday"), hours: "08:00 - 20:00" },
    { day: t("days.saturday"), hours: "09:00 - 18:00" },
    { day: t("days.sunday"), hours: "10:00 - 16:00" },
    { day: t("days.holidays"), hours: "10:00 - 16:00" }
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
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 lg:gap-16">
          <div>
            <h2 className="text-2xl font-bold text-[#0F172A] mb-6">
              {t("sendMessage")}
            </h2>
            <p className="text-[#64748B] mb-8">
              {t("formDesc")}
            </p>
            <ContactForm />
          </div>

          <div className="space-y-8">
            <div>
              <h2 className="text-2xl font-bold text-[#0F172A] mb-6">
                {t("contactInfo")}
              </h2>
              <div className="space-y-4">
                <div className="flex items-start gap-4 p-4 rounded-xl bg-white border border-[#E2E8F0]">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF]">
                    <Phone className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-[#0F172A] mb-1">{t("reservations")}</h3>
                    <a
                      href={`tel:${contactInfo.reservations.replaceAll(/\s/g, "")}`}
                      className="text-[#0369A1] hover:underline"
                    >
                      {contactInfo.reservations}
                    </a>
                    <p className="text-sm text-[#64748B] mt-1">{t("reservationsDesc")}</p>
                  </div>
                </div>

                <div className="flex items-start gap-4 p-4 rounded-xl bg-white border border-[#E2E8F0]">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF]">
                    <MessageCircle className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-[#0F172A] mb-1">{t("whatsapp")}</h3>
                    <a
                      href={`https://wa.me/${contactInfo.whatsapp.replaceAll(/\s/g, "").replaceAll("+", "")}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-[#0369A1] hover:underline"
                    >
                      {contactInfo.whatsapp}
                    </a>
                    <p className="text-sm text-[#64748B] mt-1">{t("whatsappDesc")}</p>
                  </div>
                </div>

                <div className="flex items-start gap-4 p-4 rounded-xl bg-white border border-[#E2E8F0]">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF]">
                    <Mail className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-[#0F172A] mb-1">{t("email")}</h3>
                    <a
                      href={`mailto:${contactInfo.email}`}
                      className="text-[#0369A1] hover:underline"
                    >
                      {contactInfo.email}
                    </a>
                    <p className="text-sm text-[#64748B] mt-1">{t("emailDesc")}</p>
                  </div>
                </div>

                <div className="flex items-start gap-4 p-4 rounded-xl bg-amber-50 border border-amber-200">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-amber-100">
                    <AlertTriangle className="h-6 w-6 text-amber-600" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-amber-800 mb-1">{t("emergency")}</h3>
                    <a
                      href={`tel:${contactInfo.emergency.replaceAll(/\s/g, "")}`}
                      className="text-amber-700 font-bold hover:underline"
                    >
                      {contactInfo.emergency}
                    </a>
                    <p className="text-sm text-amber-600 mt-1">
                      {t("emergencyDesc")}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div>
              <h2 className="text-xl font-bold text-[#0F172A] mb-4">
                {t("workingHours")}
              </h2>
              <div className="p-4 rounded-xl bg-white border border-[#E2E8F0]">
                <div className="flex items-center gap-2 mb-4">
                  <Clock className="h-5 w-5 text-[#0369A1]" />
                  <span className="font-semibold text-[#0F172A]">{t("mainOffice")}</span>
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
            {t("ourLocations")}
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
                    {t("officeTypes.main")}
                  </span>
                )}
                {office.type === "airport" && (
                  <span className="inline-block px-3 py-1 rounded-lg bg-[#F0F9FF] text-[#0369A1] text-xs font-semibold mb-4">
                    {t("officeTypes.airport")}
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
                      href={`tel:${office.phone.replaceAll(/\s/g, "")}`}
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
          <iframe
            src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d128084.037171682!2d31.95928245!3d36.54115!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x14dca27b8223b0b7%3A0x403b37d0ec0cb80!2sAlanya%2C%20Antalya%2C%20Turkey!5e0!3m2!1sen!2sus!4v1700000000000!5m2!1sen!2sus"
            title="Office Locations Map"
            loading="lazy"
            className="w-full h-96 rounded-2xl border-0"
            allowFullScreen
          />
        </div>
      </div>
    </div>
  );
}
