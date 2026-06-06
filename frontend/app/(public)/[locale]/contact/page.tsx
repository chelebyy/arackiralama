"use client";

import useSWR from "swr";
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
import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";
import type { PublicContactChannel, PublicContactOffice, PublicContactWorkingHour } from "@/lib/api/admin/types";

function getChannelIcon(type: string) {
  if (type === "whatsapp") return MessageCircle;
  if (type === "email") return Mail;
  if (type === "emergency") return AlertTriangle;
  return Phone;
}

function getChannelTone(type: string) {
  return type === "emergency"
    ? {
        wrapper: "bg-amber-50 border-amber-200",
        icon: "bg-amber-100",
        iconColor: "text-amber-600",
        title: "text-amber-800",
        link: "text-amber-700 font-bold",
        desc: "text-amber-600",
      }
    : {
        wrapper: "bg-white border-[#E2E8F0]",
        icon: "bg-[#F0F9FF]",
        iconColor: "text-[#0369A1]",
        title: "text-[#0F172A]",
        link: "text-[#0369A1]",
        desc: "text-[#64748B]",
      };
}

function sortedVisible<T extends { isVisible: boolean; sortOrder: number }>(items: T[]) {
  return items.filter((item) => item.isVisible).sort((a, b) => a.sortOrder - b.sortOrder);
}

const defaultMapEmbedUrl =
  "https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d128084.037171682!2d31.95928245!3d36.54115!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x14dca27b8223b0b7%3A0x403b37d0ec0cb80!2sAlanya%2C%20Antalya%2C%20Turkey!5e0!3m2!1sen!2sus!4v1700000000000!5m2!1sen!2sus";

export default function ContactPage() {
  const t = useTranslations("contactUs");
  const { data: settings } = useSWR("public-site-settings", getPublicSiteSettings, {
    revalidateOnFocus: false,
  });

  const defaultChannels: PublicContactChannel[] = [
    {
      id: "reservations",
      type: "phone",
      label: t("reservations"),
      value: "+90 242 555 10 00",
      href: "tel:+902425551000",
      description: t("reservationsDesc"),
      isVisible: true,
      sortOrder: 0,
    },
    {
      id: "whatsapp",
      type: "whatsapp",
      label: t("whatsapp"),
      value: "+90 555 123 45 67",
      href: "https://wa.me/905551234567",
      description: t("whatsappDesc"),
      isVisible: true,
      sortOrder: 1,
    },
    {
      id: "email",
      type: "email",
      label: t("email"),
      value: "info@alanyacarrental.com",
      href: "mailto:info@alanyacarrental.com",
      description: t("emailDesc"),
      isVisible: true,
      sortOrder: 2,
    },
    {
      id: "emergency",
      type: "emergency",
      label: t("emergency"),
      value: "+90 555 999 00 00",
      href: "tel:+905559990000",
      description: t("emergencyDesc"),
      isVisible: true,
      sortOrder: 3,
    },
  ];

  const defaultOffices: PublicContactOffice[] = [
    {
      id: "main",
      name: t("offices.main.name"),
      address: t("offices.main.address"),
      phone: "+90 242 555 10 00",
      hours: "08:00 - 20:00",
      type: "main",
      isVisible: true,
      sortOrder: 0,
    },
    {
      id: "gzp",
      name: t("offices.gzp.name"),
      address: t("offices.gzp.address"),
      phone: "+90 242 555 10 01",
      hours: "24/7",
      type: "airport",
      isVisible: true,
      sortOrder: 1,
    },
    {
      id: "ayt",
      name: t("offices.ayt.name"),
      address: t("offices.ayt.address"),
      phone: "+90 242 555 10 02",
      hours: "24/7",
      type: "airport",
      isVisible: true,
      sortOrder: 2,
    },
    {
      id: "mahmutlar",
      name: t("offices.mahmutlar.name"),
      address: t("offices.mahmutlar.address"),
      phone: "+90 242 555 10 03",
      hours: "09:00 - 19:00",
      type: "branch",
      isVisible: true,
      sortOrder: 3,
    },
  ];

  const defaultWorkingHours: PublicContactWorkingHour[] = [
    { id: "mondayFriday", day: t("days.mondayFriday"), hours: "08:00 - 20:00", isVisible: true, sortOrder: 0 },
    { id: "saturday", day: t("days.saturday"), hours: "09:00 - 18:00", isVisible: true, sortOrder: 1 },
    { id: "sunday", day: t("days.sunday"), hours: "10:00 - 16:00", isVisible: true, sortOrder: 2 },
    { id: "holidays", day: t("days.holidays"), hours: "10:00 - 16:00", isVisible: true, sortOrder: 3 },
  ];

  const channels = sortedVisible(settings?.contactPageChannels ?? defaultChannels);
  const offices = sortedVisible(settings?.contactPageOffices ?? defaultOffices);
  const workingHours = sortedVisible(settings?.contactPageWorkingHours ?? defaultWorkingHours);
  const mapTitle = settings?.contactPageMapTitle ?? "Office Locations Map";
  const mapEmbedUrl = settings?.contactPageMapEmbedUrl ?? defaultMapEmbedUrl;
  const isMapVisible = settings?.contactPageMapIsVisible ?? true;

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-[var(--space-fluid-3xl)]">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-[length:var(--text-fluid-5xl)] font-bold text-white mb-6">
              {t("title")}
            </h1>
            <p className="text-[length:var(--text-fluid-xl)] text-white/70">
              {t("subtitle")}
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-[var(--space-fluid-3xl)]">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-[var(--space-fluid-2xl)]">
          <div>
            <h2 className="text-[length:var(--text-fluid-3xl)] font-bold text-[#0F172A] mb-6">
              {t("sendMessage")}
            </h2>
            <p className="text-[length:var(--text-fluid-base)] text-[#64748B] mb-8">
              {t("formDesc")}
            </p>
            <ContactForm />
          </div>

          <div className="space-y-[var(--space-fluid-xl)]">
            <div>
              <h2 className="text-[length:var(--text-fluid-3xl)] font-bold text-[#0F172A] mb-6">
                {t("contactInfo")}
              </h2>
              <div className="space-y-[var(--space-fluid-md)]">
                {channels.map((channel) => {
                  const Icon = getChannelIcon(channel.type);
                  const tone = getChannelTone(channel.type);
                  const isExternal = channel.href.startsWith("http");

                  return (
                    <div key={channel.id} className={cn("flex items-start gap-4 p-4 rounded-xl border", tone.wrapper)}>
                      <div className={cn("flex h-12 w-12 items-center justify-center rounded-xl", tone.icon)}>
                        <Icon className={cn("h-6 w-6", tone.iconColor)} />
                      </div>
                      <div>
                        <h3 className={cn("text-[length:var(--text-fluid-lg)] font-semibold mb-1", tone.title)}>{channel.label}</h3>
                        <a
                          href={channel.href}
                          target={isExternal ? "_blank" : undefined}
                          rel={isExternal ? "noopener noreferrer" : undefined}
                          className={cn("hover:underline break-all", tone.link)}
                        >
                          {channel.value}
                        </a>
                        {channel.description && (
                          <p className={cn("text-sm mt-1", tone.desc)}>{channel.description}</p>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            <div>
              <h2 className="text-[length:var(--text-fluid-2xl)] font-bold text-[#0F172A] mb-4">
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
          <h2 className="text-[length:var(--text-fluid-3xl)] font-bold text-[#0F172A] mb-8 text-center">
            {t("ourLocations")}
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-[var(--space-fluid-lg)]">
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
                <h3 className="text-[length:var(--text-fluid-lg)] font-bold text-[#0F172A] mb-3">{office.name}</h3>
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

        {isMapVisible && (
          <div className="mt-16">
            <iframe
              src={mapEmbedUrl}
              title={mapTitle}
              loading="lazy"
              className="w-full h-96 rounded-2xl border-0"
              allowFullScreen
            />
          </div>
        )}
      </div>
    </div>
  );
}
