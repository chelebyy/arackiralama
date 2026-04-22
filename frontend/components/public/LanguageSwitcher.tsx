"use client";

import { useState } from "react";
import { usePathname } from "next/navigation";
import NextLink from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { type Locale } from "@/i18n/routing";
import Flag from "react-world-flags";
import { ChevronDown, Globe } from "lucide-react";
import { localeLabels, routing } from "@/i18n/routing";
import { cn } from "@/lib/utils";

export default function LanguageSwitcher() {
  const t = useTranslations("common");
  const locale = useLocale();
  const rawPathname = usePathname();
  const [isOpen, setIsOpen] = useState(false);

  const pathWithoutLocale = rawPathname.replace(/^\/(tr|en|ru|ar|de)/, "") || "/";

  const currentLocale = localeLabels[locale as Locale];

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        onBlur={(e) => {
          if (!e.currentTarget.contains(e.relatedTarget)) {
            setIsOpen(false);
          }
        }}
        className={cn(
          "flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium",
          "text-[#334155] hover:text-[#0F172A] hover:bg-[#F1F5F9]",
          "transition-colors duration-200",
          "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-1",
          "cursor-pointer"
        )}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
      >
        <Globe className="h-4 w-4" />
        <span className="hidden sm:inline">{currentLocale.label}</span>
        <Flag
          code={currentLocale.flag}
          className="h-4 w-6 rounded-sm object-cover"
          alt={`${currentLocale.label} flag`}
        />
        <ChevronDown
          className={cn(
            "h-4 w-4 transition-transform duration-200",
            isOpen && "rotate-180"
          )}
        />
      </button>

      {isOpen && (
        <div
          className={cn(
            "absolute right-0 top-full mt-2 w-48 py-2",
            "bg-white rounded-xl shadow-lg border border-[#E2E8F0]",
            "z-50 animate-in fade-in slide-in-from-top-2 duration-200"
          )}
          role="listbox"
        >
          {routing.locales.map((loc) => {
            const localeData = localeLabels[loc];
            const isActive = loc === locale;

            return (
              <NextLink
                key={loc}
                href={`/${loc}${pathWithoutLocale}`}
                className={cn(
                  "flex items-center gap-3 px-4 py-2.5 text-sm",
                  "transition-colors duration-150",
                  "cursor-pointer",
                  isActive
                    ? "bg-[#F0F9FF] text-[#0369A1] font-medium"
                    : "text-[#334155] hover:bg-[#F8FAFC] hover:text-[#0F172A]"
                )}
                role="option"
                aria-selected={isActive}
                onClick={() => setIsOpen(false)}
              >
                <Flag
                  code={localeData.flag}
                  className="h-4 w-6 rounded-sm object-cover flex-shrink-0"
                  alt={`${localeData.label} flag`}
                />
                <span className="flex-1">{localeData.label}</span>
                {isActive && (
                  <span className="h-2 w-2 rounded-full bg-[#0369A1]" />
                )}
              </NextLink>
            );
          })}
        </div>
      )}
    </div>
  );
}
