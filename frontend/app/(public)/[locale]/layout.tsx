import { NextIntlClientProvider } from "next-intl";
import { getMessages, getTranslations } from "next-intl/server";
import { notFound } from "next/navigation";
import { routing, type Locale, localeLabels } from "@/i18n/routing";
import { Lexend, Source_Sans_3 } from "next/font/google";
import { cn } from "@/lib/utils";
import Header from "@/components/public/Header";
import Footer from "@/components/public/Footer";

const lexend = Lexend({
  subsets: ["latin"],
  display: "swap",
  variable: "--font-lexend",
});

const sourceSans = Source_Sans_3({
  subsets: ["latin"],
  display: "swap",
  variable: "--font-source-sans",
});

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const validLocale = routing.locales.includes(locale as Locale)
    ? (locale as Locale)
    : routing.defaultLocale;
  const t = await getTranslations({ locale: validLocale, namespace: "metadata" });

  return {
    title: t("title"),
    description: t("description"),
  };
}

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

interface LocaleLayoutProps {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}

export default async function LocaleLayout({
  children,
  params,
}: LocaleLayoutProps) {
  const { locale } = await params;

  if (!routing.locales.includes(locale as Locale)) {
    notFound();
  }

  const validLocale = locale as Locale;
  const messages = await getMessages();
  const localeData = localeLabels[validLocale];
  const isRTL = localeData?.dir === "rtl";

  return (
    <NextIntlClientProvider messages={messages}>
      <div
        className={cn(
          lexend.variable,
          sourceSans.variable,
          "scroll-smooth",
          "min-h-screen bg-[#F8FAFC] font-sans antialiased",
          "text-[#020617]"
        )}
        style={{
          fontFamily: "var(--font-source-sans), system-ui, sans-serif",
        }}
      >
        <div className="flex flex-col min-h-screen">
          <Header />
          <main className="flex-1">
            {children}
          </main>
          <Footer />
        </div>
      </div>
    </NextIntlClientProvider>
  );
}
