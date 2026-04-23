"use client";

import { useState, useRef } from "react";
import { useLocale } from "next-intl";
import { Link, usePathname, localeLabels, routing, type Locale } from "@/i18n/routing";
import Flag from "react-world-flags";
import { ChevronDown, Globe } from "lucide-react";
import { cn } from "@/lib/utils";

export default function LanguageSwitcher() {
  const locale = useLocale();
  const pathname = usePathname();
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const currentLocale = localeLabels[locale as Locale];

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        id="language-switcher-button"
        onClick={() => setIsOpen(!isOpen)}
        onBlur={(e) => {
          if (!containerRef.current?.contains(e.relatedTarget as Node)) {
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
        aria-expanded={isOpen ? "true" : "false"}
        aria-haspopup="true"
        aria-controls="language-menu"
        aria-label="Select Language"
        title="Select Language"
      >
        <Globe className="h-4 w-4" />
        <span className="hidden sm:inline">{currentLocale.label}</span>
        <Flag code={currentLocale.flag} className="h-4 w-6 rounded-sm object-cover" alt="" />
        <ChevronDown className={cn("h-4 w-4 transition-transform duration-200", isOpen && "rotate-180")} />
      </button>

      {isOpen && (
        <div
          id="language-menu"
          role="menu"
          aria-labelledby="language-switcher-button"
          aria-orientation="vertical"
          className={cn(
            "absolute right-0 top-full mt-2 w-48 py-2",
            "bg-white rounded-xl shadow-lg border border-[#E2E8F0]",
            "z-50 animate-in fade-in slide-in-from-top-2 duration-200"
          )}
        >
          {routing.locales.map((loc) => {
            const localeData = localeLabels[loc];
            const isActive = loc === locale;

            return (
              <Link
                key={loc}
                // @ts-expect-error next-intl Link typing doesn't accept dynamic route pathname strings
                href={pathname}
                locale={loc}
                role="menuitem"
                aria-current={isActive ? "true" : undefined}
                className={cn(
                  "flex items-center gap-3 px-4 py-2.5 text-sm w-full",
                  "transition-colors duration-150",
                  isActive
                    ? "bg-[#F0F9FF] text-[#0369A1] font-medium cursor-default pointer-events-none"
                    : "text-[#334155] hover:bg-[#F8FAFC] hover:text-[#0F172A] cursor-pointer"
                )}
                onClick={() => setIsOpen(false)}
              >
                <Flag code={localeData.flag} className="h-4 w-6 rounded-sm object-cover flex-shrink-0" alt="" />
                <span className="flex-1 text-left">{localeData.label}</span>
                {isActive && <span className="h-2 w-2 rounded-full bg-[#0369A1]" />}
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
