"use client";

import { useState } from "react";
import { Link, usePathname, type Locale } from "@/i18n/routing";
import { useLocale, useTranslations } from "next-intl";
import NextLink from "next/link";
import { localeLabels } from "@/i18n/routing";
import {
  Menu,
  X,
  Car,
  Home,
  Info,
  Phone,
  Search,
  User
} from "lucide-react";
import { cn } from "@/lib/utils";
import LanguageSwitcher from "./LanguageSwitcher";

export default function Header() {
  const t = useTranslations("navigation");
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const pathname = usePathname();
  const locale = useLocale();

  const isRTL = localeLabels[locale as Locale]?.dir === "rtl";

  const navLinks = [
    { href: "/" as const, label: t("home"), icon: Home },
    { href: "/vehicles" as const, label: t("vehicles"), icon: Car },
    { href: "/about" as const, label: t("about"), icon: Info },
    { href: "/contact" as const, label: t("contact"), icon: Phone },
    { href: "/track-reservation" as const, label: t("trackReservation"), icon: Search },
  ];

  return (
    <header className="sticky top-0 z-50 w-full bg-white/95 backdrop-blur-sm border-b border-[#E2E8F0]">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex h-16 items-center justify-between">
          {/* Logo */}
          <Link
            href="/"
            className="flex items-center gap-2 cursor-pointer group"
          >
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[#0369A1]">
              <Car className="h-6 w-6 text-white" />
            </div>
            <div className={cn("hidden sm:block", isRTL && "text-right")}>
              <span className="text-lg font-bold text-[#0F172A] block leading-tight">
                Alanya
              </span>
              <span className="text-xs font-medium text-[#64748B] block leading-tight">
                Car Rental
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
                  href={link.href}
                  className={cn(
                    "px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200 cursor-pointer",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-1",
                    isActive
                      ? "bg-[#F0F9FF] text-[#0369A1]"
                      : "text-[#334155] hover:text-[#0F172A] hover:bg-[#F8FAFC]"
                  )}
                >
                  {link.label}
                </Link>
              );
            })}
          </nav>

          {/* Right Section */}
          <div className="flex items-center gap-2">
            <LanguageSwitcher />

            {/* Login Button - Desktop */}
            <NextLink
              href="/dashboard/login/v2"
              className={cn(
                "hidden sm:flex items-center gap-2 px-4 py-2 rounded-lg",
                "text-sm font-medium text-[#334155] hover:text-[#0F172A] hover:bg-[#F8FAFC]",
                "transition-colors duration-200 cursor-pointer",
                "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-1"
              )}
            >
              <User className="h-4 w-4" />
              <span>{t("login")}</span>
            </NextLink>

            {/* CTA Button - Desktop */}
            <Link
              href="/vehicles"
              className={cn(
                "hidden md:flex items-center gap-2 px-5 py-2.5 rounded-lg",
                "text-sm font-semibold text-white bg-[#0369A1]",
                "hover:bg-[#0284C7] active:bg-[#075985]",
                "transition-all duration-200 cursor-pointer",
                "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-2",
                "shadow-sm hover:shadow"
              )}
            >
              {t("trackReservation")}
            </Link>

            {/* Mobile Menu Button */}
            <button
              type="button"
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              className={cn(
                "lg:hidden p-2 rounded-lg",
                "text-[#334155] hover:text-[#0F172A] hover:bg-[#F8FAFC]",
                "transition-colors duration-200 cursor-pointer",
                "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-1"
              )}
              aria-expanded={isMobileMenuOpen}
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
            "bg-white border-b border-[#E2E8F0] shadow-lg",
            "animate-in slide-in-from-top-2 duration-200"
          )}
        >
          <nav className="mx-auto max-w-7xl px-4 py-4 space-y-1">
            {navLinks.map((link) => {
              const isActive = pathname === link.href;
              const Icon = link.icon;
              return (
                <Link
                  key={link.href}
                  href={link.href}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className={cn(
                    "flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium",
                    "transition-colors duration-200 cursor-pointer",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1]",
                    isActive
                      ? "bg-[#F0F9FF] text-[#0369A1]"
                      : "text-[#334155] hover:text-[#0F172A] hover:bg-[#F8FAFC]"
                  )}
                >
                  <Icon className="h-5 w-5 flex-shrink-0" />
                  {link.label}
                </Link>
              );
            })}
            <div className="pt-4 border-t border-[#E2E8F0] space-y-2">
              <NextLink
                href="/dashboard/login/v2"
                onClick={() => setIsMobileMenuOpen(false)}
                className={cn(
                  "flex items-center gap-3 px-4 py-3 rounded-lg",
                  "text-sm font-medium text-[#334155] hover:text-[#0F172A] hover:bg-[#F8FAFC]",
                  "transition-colors duration-200 cursor-pointer"
                )}
              >
                <User className="h-5 w-5 flex-shrink-0" />
                {t("login")}
              </NextLink>
              <Link
                href="/vehicles"
                onClick={() => setIsMobileMenuOpen(false)}
                className={cn(
                  "flex items-center justify-center gap-2 px-4 py-3 rounded-lg",
                  "text-sm font-semibold text-white bg-[#0369A1]",
                  "hover:bg-[#0284C7] active:bg-[#075985]",
                  "transition-colors duration-200 cursor-pointer"
                )}
              >
                <Car className="h-5 w-5" />
                {t("vehicles")}
              </Link>
            </div>
          </nav>
        </div>
      )}
    </header>
  );
}
