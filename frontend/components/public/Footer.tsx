"use client";

import { useTranslations } from "next-intl";
import useSWR from "swr";
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
import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";
import type { PublicSiteLink, PublicSocialLink } from "@/lib/api/admin/types";
import { isPublicSiteLinkVisible } from "@/lib/public-page-visibility";

const defaultQuickLinks = [
  { id: "vehicles", label: "", href: "/vehicles", isVisible: true, sortOrder: 0 },
  { id: "howItWorks", label: "", href: "/about", isVisible: true, sortOrder: 1 },
  { id: "contact", label: "", href: "/contact", isVisible: true, sortOrder: 2 },
  { id: "track", label: "", href: "/track-reservation", isVisible: true, sortOrder: 3 },
  { id: "booking", label: "", href: "/booking", isVisible: true, sortOrder: 4 },
  { id: "terms", label: "", href: "/terms", isVisible: true, sortOrder: 5 },
  { id: "privacy", label: "", href: "/privacy", isVisible: true, sortOrder: 6 },
] satisfies PublicSiteLink[];

const defaultSocialLinks = [
  { id: "instagram", platform: "Instagram", url: "https://instagram.com", isVisible: true, sortOrder: 0 },
  { id: "facebook", platform: "Facebook", url: "https://facebook.com", isVisible: true, sortOrder: 1 },
  { id: "twitter", platform: "Twitter", url: "https://twitter.com", isVisible: true, sortOrder: 2 },
] satisfies PublicSocialLink[];

const defaultFooterBottomLinks = [
  { id: "howItWorks", label: "", href: "/about", isVisible: true, sortOrder: 0 },
  { id: "contact", label: "", href: "/contact", isVisible: true, sortOrder: 1 },
] satisfies PublicSiteLink[];

const defaultCompanyName = "Dvn rent a car";

function getSocialIcon(platform: string) {
  const normalized = platform.toLowerCase();
  if (normalized === "facebook") return Facebook;
  if (normalized === "twitter" || normalized === "x") return Twitter;
  return Instagram;
}

function hasTranslation(
  t: ReturnType<typeof useTranslations>,
  key: string
): boolean {
  return typeof t.has === "function" && t.has(key);
}

export default function Footer() {
  const t = useTranslations("footer");
  const year = new Date().getFullYear();
  const { data: settings } = useSWR("public-site-settings", getPublicSiteSettings, {
    revalidateOnFocus: false,
    shouldRetryOnError: false,
  });

  const quickLinks = (settings?.quickLinks ?? defaultQuickLinks)
    .filter((link) => isPublicSiteLinkVisible(link, settings?.pages))
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const socialLinks = (settings?.socialLinks ?? defaultSocialLinks)
    .filter((link) => link.isVisible)
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const footerBottomLinks = (settings?.footerBottomLinks ?? defaultFooterBottomLinks)
    .filter((link) => isPublicSiteLinkVisible(link, settings?.pages))
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const companyAddress = settings?.companyAddress || t("contact.address");
  const companyPhone = settings?.companyPhone || t("contact.phone");
  const companyEmail = settings?.companyEmail || t("contact.email");
  const workingHours = settings?.workingHours || t("contact.workingHours");
  const companyName = settings?.companyName?.trim() || defaultCompanyName;
  const getLinkLabel = (link: PublicSiteLink) => {
    const translationKey = `quickLinks.links.${link.id}`;
    return hasTranslation(t, translationKey)
      ? t(translationKey)
      : link.label || link.id;
  };

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
                  {companyName}
                </span>
              </div>
            </Link>
            <p className="text-sm text-[#94A3B8] leading-relaxed">
              {t("about.description")}
            </p>
            {/* Social Links */}
            <div className="flex items-center gap-3 pt-2">
              {socialLinks.map((link) => {
                const Icon = getSocialIcon(link.platform);
                return (
                  <a
                    key={link.id}
                    href={link.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className={cn(
                      "flex h-10 w-10 items-center justify-center rounded-lg",
                      "bg-[#1E293B] text-[#94A3B8]",
                      "hover:bg-[#0369A1] hover:text-white",
                      "transition-all duration-200 cursor-pointer",
                      "focus:outline-none focus:ring-2 focus:ring-[#0369A1]"
                    )}
                    aria-label={link.platform}
                  >
                    <Icon className="h-5 w-5" />
                  </a>
                );
              })}
            </div>
          </div>

          {/* Quick Links */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-white tracking-wider">
              {t("quickLinks.title")}
            </h3>
            <ul className="space-y-2">
              {quickLinks.map((link) => (
                <li key={link.id}>
                  <Link
                    href={link.href as never}
                    className={cn(
                      "group flex items-center gap-1 text-sm text-[#94A3B8]",
                      "hover:text-white transition-colors duration-200 cursor-pointer",
                      "focus:outline-none focus:text-white"
                    )}
                  >
                    <ChevronRight className="h-4 w-4 opacity-0 -ml-5 group-hover:opacity-100 group-hover:ml-0 transition-all duration-200" />
                    {getLinkLabel(link)}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          {/* Contact Info */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-white tracking-wider">
              {t("contact.title")}
            </h3>
            <ul className="space-y-3">
              <li className="flex items-start gap-3">
                <MapPin className="h-5 w-5 text-[#0369A1] flex-shrink-0 mt-0.5" />
                <span className="text-sm text-[#94A3B8]">
                  {companyAddress}
                </span>
              </li>
              <li className="flex items-center gap-3">
                <Phone className="h-5 w-5 text-[#0369A1] flex-shrink-0" />
                <a
                  href={`tel:${companyPhone.replace(/\s/g, "")}`}
                  className="text-sm text-[#94A3B8] hover:text-white transition-colors duration-200 cursor-pointer"
                >
                  {companyPhone}
                </a>
              </li>
              <li className="flex items-center gap-3">
                <Mail className="h-5 w-5 text-[#0369A1] flex-shrink-0" />
                <a
                  href={`mailto:${companyEmail}`}
                  className="text-sm text-[#94A3B8] hover:text-white transition-colors duration-200 cursor-pointer"
                >
                  {companyEmail}
                </a>
              </li>
              <li className="flex items-center gap-3">
                <Clock className="h-5 w-5 text-[#0369A1] flex-shrink-0" />
                <span className="text-sm text-[#94A3B8]">
                  {workingHours}
                </span>
              </li>
            </ul>
          </div>

          {/* Newsletter */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-white tracking-wider">
              {t("newsletter.title")}
            </h3>
            <p className="text-sm text-[#94A3B8]">
              {t("newsletter.description")}
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
              {t("copyright", { year, companyName })}
            </p>
            <div className="flex items-center gap-6">
              {footerBottomLinks.map((link) => (
                <Link
                  key={link.id}
                  href={link.href as never}
                  className="text-sm text-[#64748B] hover:text-white transition-colors duration-200 cursor-pointer"
                >
                  {getLinkLabel(link)}
                </Link>
              ))}
            </div>
          </div>
        </div>
      </div>
    </footer>
  );
}
