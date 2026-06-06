import { useMessages, useTranslations } from "next-intl";
import {
  FileText,
  Shield,
  CreditCard,
  Fuel,
  AlertCircle,
  CheckCircle,
  Car,
  User,
  Clock
} from "lucide-react";
import { cn } from "@/lib/utils";

const sectionIcons = {
  agreement: FileText,
  age: User,
  insurance: Shield,
  payment: CreditCard,
  fuel: Fuel,
  cancellation: Clock,
  usage: Car,
  liability: AlertCircle
} as const;

type TermsSection = {
  id: keyof typeof sectionIcons;
  title: string;
  content: string[];
};

export default function TermsPage() {
  const t = useTranslations("legal");
  const terms = useTranslations("legal.terms");
  const messages = useMessages() as { legal: { terms: { sections: TermsSection[] } } };
  const sections = messages.legal.terms.sections;

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl lg:text-5xl font-bold text-white mb-6">
              {terms("title")}
            </h1>
            <p className="text-lg lg:text-xl text-white/70">
              {terms("subtitle")}
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="bg-amber-50 border border-amber-200 rounded-2xl p-6 mb-12">
          <div className="flex items-start gap-4">
            <AlertCircle className="h-6 w-6 text-amber-600 flex-shrink-0 mt-0.5" />
            <div>
              <h2 className="font-semibold text-amber-800 mb-2">{terms("noticeTitle")}</h2>
              <p className="text-amber-700 text-sm leading-relaxed">
                {terms("noticeBody")}
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-8">
          {sections.map((section, index) => {
            const Icon = sectionIcons[section.id];
            return (
              <section
                key={section.id}
                id={section.id}
                className={cn(
                  "p-6 lg:p-8 rounded-2xl bg-white border border-[#E2E8F0]",
                  "transition-all duration-300 hover:shadow-md"
                )}
              >
                <div className="flex items-start gap-4 mb-6">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[#F0F9FF] flex-shrink-0">
                    <Icon className="h-6 w-6 text-[#0369A1]" />
                  </div>
                  <div>
                    <span className="text-sm font-medium text-[#0369A1]">
                      {t("sectionLabel", { number: String(index + 1).padStart(2, "0") })}
                    </span>
                    <h2 className="text-xl font-bold text-[#0F172A]">
                      {section.title}
                    </h2>
                  </div>
                </div>

                <ul className="space-y-3">
                  {section.content.map((item) => (
                    <li
                      key={item.slice(0, 30)}
                      className="flex items-start gap-3 text-[#475569]"
                    >
                      <CheckCircle className="h-5 w-5 text-[#0369A1] flex-shrink-0 mt-0.5" />
                      <span className="leading-relaxed">{item}</span>
                    </li>
                  ))}
                </ul>
              </section>
            );
          })}
        </div>

        <div className="mt-12 p-6 rounded-2xl bg-[#0F172A] text-white">
          <h2 className="text-xl font-bold mb-4">{terms("contactTitle")}</h2>
          <p className="text-white/70 mb-6">{terms("contactBody")}</p>
          <div className="flex flex-wrap gap-4">
            <a
              href="mailto:legal@alanyacarrental.com"
              className={cn(
                "inline-flex items-center gap-2 px-6 py-3 rounded-xl",
                "text-sm font-semibold bg-white text-[#0369A1]",
                "hover:bg-[#F8FAFC] transition-all duration-200"
              )}
            >
              {terms("legalEmail")}
            </a>
            <a
              href="tel:+905555550100"
              className={cn(
                "inline-flex items-center gap-2 px-6 py-3 rounded-xl",
                "text-sm font-semibold bg-white/10 text-white border border-white/20",
                "hover:bg-white/20 transition-all duration-200"
              )}
            >
              {terms("servicePhone")}
            </a>
          </div>
        </div>

        <div className="mt-12 text-center">
          <p className="text-sm text-[#64748B]">{terms("lastUpdated")}</p>
        </div>
      </div>
    </div>
  );
}
