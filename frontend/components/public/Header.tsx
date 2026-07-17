"use client";

import { useState } from "react";
import useSWR from "swr";
import { Link, usePathname, type Locale } from "@/i18n/routing";
import { useLocale, useTranslations } from "next-intl";
import { localeLabels } from "@/i18n/routing";
import {
  Menu,
  X,
  Car,
  Home,
  Info,
  Phone,
  Search
} from "lucide-react";
import { cn } from "@/lib/utils";
import LanguageSwitcher from "./LanguageSwitcher";
import { getPublicSiteSettings } from "@/lib/api/publicSiteSettings";
import type { PublicSiteLink } from "@/lib/api/admin/types";
import { isPublicSiteLinkVisible } from "@/lib/public-page-visibility";
import { getLocalizedPublicSettingText } from "@/lib/public-settings-localization";

const defaultHeaderLinks = [
  { id: "home", label: "", href: "/", isVisible: true, sortOrder: 0 },
  { id: "vehicles", label: "", href: "/vehicles", isVisible: true, sortOrder: 1 },
  { id: "about", label: "", href: "/about", isVisible: true, sortOrder: 2 },
  { id: "contact", label: "", href: "/contact", isVisible: true, sortOrder: 3 },
  { id: "trackReservation", label: "", href: "/track-reservation", isVisible: true, sortOrder: 5 },
] satisfies PublicSiteLink[];

const defaultCompanyName = "Dvn rent a car";

function getHeaderIcon(id: string) {
  if (id === "vehicles") return Car;
  if (id === "about") return Info;
  if (id === "contact") return Phone;
  if (id === "trackReservation") return Search;
  return Home;
}

function hasTranslation(
  t: ReturnType<typeof useTranslations>,
  key: string
): boolean {
  return typeof t.has === "function" && t.has(key);
}

export default function Header() {
  const t = useTranslations("navigation");
  const tc = useTranslations("common");
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const pathname = usePathname();
  const locale = useLocale();
  const { data: settings } = useSWR("public-site-settings", getPublicSiteSettings, {
    revalidateOnFocus: false,
    shouldRetryOnError: false,
  });

  const isRTL = localeLabels[locale as Locale]?.dir === "rtl";

  const headerLinks = (settings?.headerLinks ?? defaultHeaderLinks)
    .filter((link) => isPublicSiteLinkVisible(link, settings?.pages))
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const navLinks = headerLinks.filter((link) => link.id !== "login" && link.id !== "trackReservation");
  const trackingLink = headerLinks.find((link) => link.id === "trackReservation");
  const getLabel = (link: PublicSiteLink) => {
    const fallback = hasTranslation(t, link.id) ? t(link.id) : link.label || link.id;
    return getLocalizedPublicSettingText(link.translations, locale, "label", fallback);
  };
  const companyName = settings?.companyName?.trim() || defaultCompanyName;

  return (
    <header className="sticky top-0 z-50 w-full bg-[#0F172A]/95 backdrop-blur-sm border-b border-white/10 lg:bg-white/95 lg:border-[#E2E8F0]">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex h-16 items-center justify-between">
          {/* Logo */}
          <Link
            href="/"
            className="flex items-center gap-2 cursor-pointer group"
          >
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/10 ring-1 ring-white/15 lg:bg-[#0369A1] lg:ring-0">
              <Car className="h-6 w-6 text-white" />
            </div>
            <div className={cn("min-w-0 max-w-[150px] sm:max-w-none", isRTL && "text-right")}>
              <span className="block truncate text-sm font-bold leading-tight text-white sm:text-lg lg:text-[#0F172A]">
                {companyName}
              </span>
            </div>
          </Link>

          {/* Desktop Navigation */}
          <nav className="hidden lg:flex items-center gap-1">
            {navLinks.map((link) => {
              const isActive = pathname === link.href;
              return (
                <Link
                  key={link.href}
                  href={link.href as never}
                  className={cn(
                    "px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200 cursor-pointer",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-1",
                    isActive
                      ? "bg-[#F0F9FF] text-[#0369A1]"
                      : "text-[#334155] hover:text-[#0F172A] hover:bg-[#F8FAFC]"
                  )}
                >
                  {getLabel(link)}
                </Link>
              );
            })}
          </nav>

          {/* Right Section */}
          <div className="flex items-center gap-2">
            <LanguageSwitcher />

            {/* CTA Button - Desktop */}
            {trackingLink && (
              <Link
                href={trackingLink.href as never}
                className={cn(
                  "hidden md:flex items-center gap-2 px-5 py-2.5 rounded-lg",
                  "text-sm font-semibold text-white bg-[#0369A1]",
                  "hover:bg-[#0284C7] active:bg-[#075985]",
                  "transition-all duration-200 cursor-pointer",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-2",
                  "shadow-sm hover:shadow"
                )}
              >
                <Search className="h-4 w-4" />
                {getLabel(trackingLink)}
              </Link>
            )}

            {/* Mobile Menu Button */}
            <button
              type="button"
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              className={cn(
                "lg:hidden p-2 rounded-lg",
                "text-white/90 hover:text-white hover:bg-white/10",
                "transition-colors duration-200 cursor-pointer",
                "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-1"
              )}
              aria-label="Toggle menu"
            >
              {isMobileMenuOpen ? (
                <X className="h-6 w-6" />
              ) : (
                <Menu className="h-6 w-6" />
              )}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Menu */}
      {isMobileMenuOpen && (
        <div
          className={cn(
            "lg:hidden absolute top-full left-0 right-0",
            "bg-[#0F172A]/98 border-b border-white/10 shadow-2xl shadow-[#0F172A]/30 backdrop-blur-md",
            "animate-in slide-in-from-top-2 duration-200"
          )}
        >
          <nav className="mx-auto max-w-7xl px-4 py-4 space-y-1">
            {navLinks.map((link) => {
              const isActive = pathname === link.href;
              const Icon = getHeaderIcon(link.id);
              return (
                <Link
                  key={link.href}
                  href={link.href as never}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className={cn(
                    "flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium",
                    "transition-colors duration-200 cursor-pointer",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1]",
                    isActive
                      ? "bg-white/12 text-white"
                      : "text-white/78 hover:text-white hover:bg-white/10"
                  )}
                >
                  <Icon className="h-5 w-5 flex-shrink-0" />
                  {getLabel(link)}
                </Link>
              );
            })}
            <div className="pt-4 border-t border-white/10 space-y-2">
              {trackingLink && (
                <Link
                  href={trackingLink.href as never}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className={cn(
                    "flex items-center justify-center gap-2 px-4 py-3 rounded-lg",
                    "text-sm font-semibold text-white bg-[#0369A1]",
                    "hover:bg-[#0284C7] active:bg-[#075985]",
                    "transition-colors duration-200 cursor-pointer"
                  )}
                >
                  <Search className="h-5 w-5" />
                  {getLabel(trackingLink)}
                </Link>
              )}
            </div>
          </nav>
        </div>
      )}
    </header>
  );
}
