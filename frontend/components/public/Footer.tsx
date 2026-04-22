"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";
import {
  MapPin,
  Phone,
  Mail,
  Clock,
  Car,
  ChevronRight,
  Instagram,
  Facebook,
  Twitter
} from "lucide-react";
import { cn } from "@/lib/utils";

export default function Footer() {
  const t = useTranslations("footer");
  const year = new Date().getFullYear();

  const quickLinks = [
    { key: "vehicles", href: "/vehicles" as const },
    { key: "howItWorks", href: "/about" as const },
    { key: "contact", href: "/contact" as const },
    { key: "track", href: "/track-reservation" as const },
    { key: "booking", href: "/booking" as const },
  ];

  return (
    <footer className="w-full bg-[#0F172A] text-white">
      {/* Main Footer */}
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-12 lg:py-16">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8 lg:gap-12">
          {/* Brand Column */}
          <div className="space-y-4">
            <Link
              href="/"
              className="flex items-center gap-2 cursor-pointer group"
            >
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[#0369A1]">
                <Car className="h-6 w-6 text-white" />
              </div>
              <div>
                <span className="text-lg font-bold text-white block leading-tight">
                  Alanya
                </span>
                <span className="text-xs font-medium text-[#94A3B8] block leading-tight">
                  Car Rental
                </span>
              </div>
            </Link>
            <p className="text-sm text-[#94A3B8] leading-relaxed">
              {t("about.description")}
            </p>
            {/* Social Links */}
            <div className="flex items-center gap-3 pt-2">
              <a
                href="https://instagram.com"
                target="_blank"
                rel="noopener noreferrer"
                className={cn(
                  "flex h-10 w-10 items-center justify-center rounded-lg",
                  "bg-[#1E293B] text-[#94A3B8]",
                  "hover:bg-[#0369A1] hover:text-white",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1]"
                )}
                aria-label="Instagram"
              >
                <Instagram className="h-5 w-5" />
              </a>
              <a
                href="https://facebook.com"
                target="_blank"
                rel="noopener noreferrer"
                className={cn(
                  "flex h-10 w-10 items-center justify-center rounded-lg",
                  "bg-[#1E293B] text-[#94A3B8]",
                  "hover:bg-[#0369A1] hover:text-white",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1]"
                )}
                aria-label="Facebook"
              >
                <Facebook className="h-5 w-5" />
              </a>
              <a
                href="https://twitter.com"
                target="_blank"
                rel="noopener noreferrer"
                className={cn(
                  "flex h-10 w-10 items-center justify-center rounded-lg",
                  "bg-[#1E293B] text-[#94A3B8]",
                  "hover:bg-[#0369A1] hover:text-white",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1]"
                )}
                aria-label="Twitter"
              >
                <Twitter className="h-5 w-5" />
              </a>
            </div>
          </div>

          {/* Quick Links */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-white uppercase tracking-wider">
              {t("quickLinks.title")}
            </h3>
            <ul className="space-y-2">
              {quickLinks.map((link) => (
                <li key={link.key}>
                  <Link
                    href={link.href}
                    className={cn(
                      "group flex items-center gap-1 text-sm text-[#94A3B8]",
                      "hover:text-white transition-colors duration-200 cursor-pointer",
                      "focus:outline-none focus:text-white"
                    )}
                  >
                    <ChevronRight className="h-4 w-4 opacity-0 -ml-5 group-hover:opacity-100 group-hover:ml-0 transition-all duration-200" />
                    {t(`quickLinks.links.${link.key}`)}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          {/* Contact Info */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-white uppercase tracking-wider">
              {t("contact.title")}
            </h3>
            <ul className="space-y-3">
              <li className="flex items-start gap-3">
                <MapPin className="h-5 w-5 text-[#0369A1] flex-shrink-0 mt-0.5" />
                <span className="text-sm text-[#94A3B8]">
                  {t("contact.address")}
                </span>
              </li>
              <li className="flex items-center gap-3">
                <Phone className="h-5 w-5 text-[#0369A1] flex-shrink-0" />
                <a
                  href={`tel:${t("contact.phone").replace(/\s/g, "")}`}
                  className="text-sm text-[#94A3B8] hover:text-white transition-colors duration-200 cursor-pointer"
                >
                  {t("contact.phone")}
                </a>
              </li>
              <li className="flex items-center gap-3">
                <Mail className="h-5 w-5 text-[#0369A1] flex-shrink-0" />
                <a
                  href={`mailto:${t("contact.email")}`}
                  className="text-sm text-[#94A3B8] hover:text-white transition-colors duration-200 cursor-pointer"
                >
                  {t("contact.email")}
                </a>
              </li>
              <li className="flex items-center gap-3">
                <Clock className="h-5 w-5 text-[#0369A1] flex-shrink-0" />
                <span className="text-sm text-[#94A3B8]">
                  {t("contact.workingHours")}
                </span>
              </li>
            </ul>
          </div>

          {/* Newsletter */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-white uppercase tracking-wider">
              {t("newsletter.title")}
            </h3>
            <p className="text-sm text-[#94A3B8]">
              Subscribe for exclusive deals and updates.
            </p>
            <form className="space-y-2" onSubmit={(e) => e.preventDefault()}>
              <input
                id="newsletter-email"
                name="email"
                type="email"
                placeholder={t("newsletter.placeholder")}
                className={cn(
                  "w-full px-4 py-3 rounded-lg bg-[#1E293B] border border-[#334155]",
                  "text-sm text-white placeholder-[#64748B]",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                  "transition-all duration-200"
                )}
              />
              <button
                type="submit"
                className={cn(
                  "w-full px-4 py-3 rounded-lg",
                  "text-sm font-semibold text-white bg-[#0369A1]",
                  "hover:bg-[#0284C7] active:bg-[#075985]",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-2",
                  "focus:ring-offset-[#0F172A]"
                )}
              >
                {t("newsletter.button")}
              </button>
            </form>
          </div>
        </div>
      </div>

      {/* Bottom Bar */}
      <div className="border-t border-[#1E293B]">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-6">
          <div className="flex flex-col sm:flex-row items-center justify-between gap-4">
            <p className="text-sm text-[#64748B]">
              {t("copyright", { year })}
            </p>
            <div className="flex items-center gap-6">
              <Link
                href="/about"
                className="text-sm text-[#64748B] hover:text-white transition-colors duration-200 cursor-pointer"
              >
                {t("quickLinks.links.howItWorks")}
              </Link>
              <Link
                href="/contact"
                className="text-sm text-[#64748B] hover:text-white transition-colors duration-200 cursor-pointer"
              >
                {t("quickLinks.links.contact")}
              </Link>
            </div>
          </div>
        </div>
      </div>
    </footer>
  );
}
