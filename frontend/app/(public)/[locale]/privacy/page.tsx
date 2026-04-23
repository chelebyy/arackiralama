import { useTranslations } from "next-intl";
import {
  Shield,
  Eye,
  Database,
  UserCheck,
  Cookie,
  Mail,
  Lock,
  FileText,
  ChevronRight,
  AlertCircle,
  CheckCircle
} from "lucide-react";
import { cn } from "@/lib/utils";

const sections = [
  {
    id: "collection",
    icon: Eye,
    title: "Data We Collect",
    content: [
      "Personal identification information: name, date of birth, nationality",
      "Contact information: email address, phone number, residential address",
      "Driver's license details and driving history",
      "Payment information: credit card details, billing address",
      "Reservation details: pickup/dropoff locations, dates, vehicle preferences",
      "Website usage data: IP address, browser type, pages visited",
      "Communication records: emails, chat logs, phone call records"
    ]
  },
  {
    id: "usage",
    icon: Database,
    title: "How We Use Your Data",
    content: [
      "Processing and confirming your vehicle reservations",
      "Verifying your identity and driver's license validity",
      "Processing payments and managing security deposits",
      "Communicating with you about your reservation",
      "Providing customer support and handling inquiries",
      "Improving our services and website experience",
      "Complying with legal obligations and law enforcement requests",
      "Sending promotional offers (only with your consent)"
    ]
  },
  {
    id: "storage",
    icon: Lock,
    title: "Data Storage and Security",
    content: [
      "Your data is stored on secure servers located within the European Union",
      "We use industry-standard encryption (SSL/TLS) for data transmission",
      "Access to personal data is restricted to authorized personnel only",
      "We implement regular security audits and vulnerability assessments",
      "Payment information is processed through PCI-DSS compliant payment gateways",
      "We never store your full credit card details on our servers",
      "Physical records are stored in secure, access-controlled facilities"
    ]
  },
  {
    id: "retention",
    icon: FileText,
    title: "Data Retention",
    content: [
      "Reservation data is retained for 7 years for legal and tax purposes",
      "Marketing preferences and consent records are retained until you withdraw consent",
      "Website analytics data is anonymized after 26 months",
      "Security logs are retained for 1 year",
      "You may request deletion of your data subject to legal obligations"
    ]
  },
  {
    id: "cookies",
    icon: Cookie,
    title: "Cookie Policy",
    content: [
      "Essential cookies: Required for website functionality and cannot be disabled",
      "Analytics cookies: Help us understand how visitors interact with our website",
      "Marketing cookies: Used to deliver relevant advertisements (only with consent)",
      "You can manage cookie preferences through your browser settings",
      "Our cookie consent banner allows you to customize your preferences",
      "Third-party cookies may be set by our payment and analytics partners"
    ]
  },
  {
    id: "rights",
    icon: UserCheck,
    title: "Your Rights Under KVKK",
    content: [
      "Right to access: Request a copy of your personal data we hold",
      "Right to rectification: Request correction of inaccurate data",
      "Right to erasure: Request deletion of your data (right to be forgotten)",
      "Right to restrict processing: Limit how we use your data",
      "Right to data portability: Receive your data in a machine-readable format",
      "Right to object: Object to certain types of data processing",
      "Right to withdraw consent: Withdraw previously given consent at any time"
    ]
  },
  {
    id: "sharing",
    icon: Shield,
    title: "Data Sharing and Third Parties",
    content: [
      "We only share your data with necessary third-party service providers",
      "Payment processing is handled by certified payment providers",
      "Insurance partners receive only necessary information for coverage",
      "We may share data with law enforcement when legally required",
      "We do not sell or rent your personal data to third parties",
      "All third-party partners are contractually bound to data protection standards"
    ]
  }
];

export default function PrivacyPage() {
  const t = useTranslations();

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="bg-[#0F172A] py-16 lg:py-24">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-3xl lg:text-5xl font-bold text-white mb-6">
              Privacy Policy
            </h1>
            <p className="text-lg lg:text-xl text-white/70">
              How we protect and handle your personal data
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="bg-emerald-50 border border-emerald-200 rounded-2xl p-6 mb-12">
          <div className="flex items-start gap-4">
            <Shield className="h-6 w-6 text-emerald-600 flex-shrink-0 mt-0.5" />
            <div>
              <h2 className="font-semibold text-emerald-800 mb-2">KVKK Compliance</h2>
              <p className="text-emerald-700 text-sm leading-relaxed">
                Alanya Car Rental is fully compliant with the Turkish Personal Data Protection Law 
                (KVKK) No. 6698 and the EU General Data Protection Regulation (GDPR). We are 
                committed to protecting your privacy and ensuring the security of your personal data.
              </p>
            </div>
          </div>
        </div>

        <div className="prose max-w-none mb-12">
          <p className="text-[#475569] leading-relaxed">
            This Privacy Policy explains how Alanya Car Rental collects, uses, stores, and 
            protects your personal information when you use our services. By using our website 
            and services, you consent to the practices described in this policy.
          </p>
        </div>

        <div className="space-y-8">
          {sections.map((section, index) => {
            const Icon = section.icon;
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
                      Section {String(index + 1).padStart(2, "0")}
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
              <h2 className="text-xl font-bold mb-4">Contact Our Data Protection Officer</h2>
              <p className="text-white/70 mb-6">
                If you have any questions about this Privacy Policy, want to exercise your rights, 
                or have concerns about how we handle your data, please contact our Data Protection Officer:
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="p-4 rounded-xl bg-white/5 border border-white/10">
                  <p className="text-sm text-white/60 mb-1">Email</p>
                  <a
                    href="mailto:dpo@alanyacarrental.com"
                    className="text-[#0369A1] font-medium hover:underline"
                  >
                    dpo@alanyacarrental.com
                  </a>
                </div>
                <div className="p-4 rounded-xl bg-white/5 border border-white/10">
                  <p className="text-sm text-white/60 mb-1">Postal Address</p>
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
              <h3 className="font-semibold text-amber-800 mb-2">Data Breach Notification</h3>
              <p className="text-amber-700 text-sm leading-relaxed">
                In the unlikely event of a data breach that may affect your personal information, 
                we will notify you and the relevant authorities within 72 hours as required by law. 
                We maintain comprehensive incident response procedures to minimize any potential impact.
              </p>
            </div>
          </div>
        </div>

        <div className="mt-12 text-center">
          <p className="text-sm text-[#64748B]">
            Last updated: March 2025
          </p>
        </div>
      </div>
    </div>
  );
}
