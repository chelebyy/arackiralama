import { defineRouting } from "next-intl/routing";
import { createNavigation } from "next-intl/navigation";

export const routing = defineRouting({
  locales: ["tr", "en", "ru", "ar", "de"],
  defaultLocale: "tr",
  localePrefix: {
    mode: "always",
    prefixes: {
      tr: "/tr",
      en: "/en",
      ru: "/ru",
      ar: "/ar",
      de: "/de"
    }
  },
  pathnames: {
    "/": {
      tr: "/",
      en: "/",
      ru: "/",
      ar: "/",
      de: "/"
    },
    "/vehicles": {
      tr: "/araclar",
      en: "/vehicles",
      ru: "/avtomobili",
      ar: "/al-sayarat",
      de: "/fahrzeuge"
    },
    "/about": {
      tr: "/hakkimizda",
      en: "/about",
      ru: "/o-nas",
      ar: "/amna",
      de: "/ueber-uns"
    },
    "/contact": {
      tr: "/iletisim",
      en: "/contact",
      ru: "/kontakty",
      ar: "/at-tawasul",
      de: "/kontakt"
    },
    "/track-reservation": {
      tr: "/rezervasyon-takip",
      en: "/track-reservation",
      ru: "/otsledit-bronirovanie",
      ar: "/tatabu-al-hajz",
      de: "/reservierung-verfolgen"
    },
    "/booking": {
      tr: "/rezervasyon",
      en: "/booking",
      ru: "/bronirovanie",
      ar: "/al-hajz",
      de: "/buchung"
    },
    "/vehicles/[id]": {
      tr: "/araclar/[id]",
      en: "/vehicles/[id]",
      ru: "/avtomobili/[id]",
      ar: "/al-sayarat/[id]",
      de: "/fahrzeuge/[id]"
    }
  }
});

export type Locale = (typeof routing.locales)[number];

export const localeLabels: Record<Locale, { label: string; flag: string; dir?: "ltr" | "rtl" }> = {
  tr: { label: "Türkçe", flag: "TR" },
  en: { label: "English", flag: "GB" },
  ru: { label: "Русский", flag: "RU" },
  ar: { label: "العربية", flag: "SA", dir: "rtl" },
  de: { label: "Deutsch", flag: "DE" }
};

export const { Link, redirect, usePathname, useRouter, getPathname } = createNavigation(routing);
