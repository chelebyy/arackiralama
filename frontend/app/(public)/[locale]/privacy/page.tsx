import { useMessages, useTranslations } from "next-intl";
import {
  Shield,
  Eye,
  Database,
  UserCheck,
  Cookie,
  Mail,
  Lock,
  FileText,
  AlertCircle,
  CheckCircle
} from "lucide-react";
import { cn } from "@/lib/utils";

const sectionIcons = {
  collection: Eye,
  usage: Database,
  storage: Lock,
  retention: FileText,
  cookies: Cookie,
  rights: UserCheck,
  sharing: Shield
} as const;

type PrivacySection = {
  id: keyof typeof sectionIcons;
  title: string;
  content: string[];
};

export default function PrivacyPage() {
  const t = useTranslations("legal");
  const privacy = useTranslations("legal.privacy");
  const messages = useMessages() as { legal: { privacy: { sections: PrivacySection[] } } };
  const sections = messages.legal.privacy.sections;

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl lg:text-5xl font-bold text-white mb-6">
              {privacy("title")}
            </h1>
            <p className="text-lg lg:text-xl text-white/70">
              {privacy("subtitle")}
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="bg-emerald-50 border border-emerald-200 rounded-2xl p-6 mb-12">
          <div className="flex items-start gap-4">
            <Shield className="h-6 w-6 text-emerald-600 flex-shrink-0 mt-0.5" />
            <div>
              <h2 className="font-semibold text-emerald-800 mb-2">{privacy("noticeTitle")}</h2>
              <p className="text-emerald-700 text-sm leading-relaxed">
                {privacy("noticeBody")}
              </p>
            </div>
          </div>
        </div>

        <div className="prose max-w-none mb-12">
          <p className="text-[#475569] leading-relaxed">{privacy("intro")}</p>
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

        <div className="mt-12 p-6 lg:p-8 rounded-2xl bg-[#0F172A] text-white">
          <div className="flex items-start gap-4">
            <Mail className="h-8 w-8 text-[#0369A1] flex-shrink-0" />
            <div className="flex-1">
              <h2 className="text-xl font-bold mb-4">{privacy("contactTitle")}</h2>
              <p className="text-white/70 mb-6">{privacy("contactBody")}</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="p-4 rounded-xl bg-white/5 border border-white/10">
                  <p className="text-sm text-white/60 mb-1">{privacy("emailLabel")}</p>
                  <a
                    href="mailto:dpo@alanyacarrental.com"
                    className="text-[#0369A1] font-medium hover:underline"
                  >
                    dpo@alanyacarrental.com
                  </a>
                </div>
                <div className="p-4 rounded-xl bg-white/5 border border-white/10">
                  <p className="text-sm text-white/60 mb-1">{privacy("addressLabel")}</p>
                  <p className="text-white/80 text-sm">
                    Alanya Car Rental DPO<br />
                    Ataturk Boulevard No. 45<br />
                    Alanya 07400, Turkey
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="mt-8 p-6 rounded-2xl bg-amber-50 border border-amber-200">
          <div className="flex items-start gap-4">
            <AlertCircle className="h-6 w-6 text-amber-600 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-semibold text-amber-800 mb-2">{privacy("breachTitle")}</h3>
              <p className="text-amber-700 text-sm leading-relaxed">
                {privacy("breachBody")}
              </p>
            </div>
          </div>
        </div>

        <div className="mt-12 text-center">
          <p className="text-sm text-[#64748B]">{privacy("lastUpdated")}</p>
        </div>
      </div>
    </div>
  );
}
