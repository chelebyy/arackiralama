"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import { Send, User, Mail, Phone, MessageSquare, CheckCircle, AlertCircle, ChevronDown } from "lucide-react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";

interface FormData {
  name: string;
  email: string;
  phone: string;
  subject: string;
  message: string;
}

interface FormErrors {
  name?: string;
  email?: string;
  phone?: string;
  subject?: string;
  message?: string;
}

export default function ContactForm() {
  const t = useTranslations("contactUs.form");
  const tErr = useTranslations("common.errors.validation");
  
  const [formData, setFormData] = useState<FormData>({
    name: "",
    email: "",
    phone: "",
    subject: "",
    message: ""
  });

  const [errors, setErrors] = useState<FormErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const validateForm = (): boolean => {
    const newErrors: FormErrors = {};

    if (!formData.name.trim()) {
      newErrors.name = tErr("required");
    } else if (formData.name.length < 2) {
      newErrors.name = tErr("minLength", { min: 2 });
    }

    if (!formData.email.trim()) {
      newErrors.email = tErr("required");
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      newErrors.email = tErr("email");
    }

    if (!formData.phone.trim()) {
      newErrors.phone = tErr("required");
    } else if (!/^\+?[\d\s-]{8,}$/.test(formData.phone.replace(/\s/g, ""))) {
      newErrors.phone = tErr("phone");
    }

    if (!formData.subject.trim()) {
      newErrors.subject = tErr("required");
    }

    if (!formData.message.trim()) {
      newErrors.message = tErr("required");
    } else if (formData.message.length < 10) {
      newErrors.message = tErr("minLength", { min: 10 });
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    if (errors[name as keyof FormErrors]) {
      setErrors(prev => ({ ...prev, [name]: undefined }));
    }
    setSubmitError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitError(null);

    if (!validateForm()) {
      return;
    }

    setIsSubmitting(true);

    try {
      await new Promise(resolve => setTimeout(resolve, 1500));
      setIsSubmitted(true);
      setFormData({
        name: "",
        email: "",
        phone: "",
        subject: "",
        message: ""
      });
    } catch (err) {
      console.error(err);
      setSubmitError(t("error"));
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isSubmitted) {
    return (
      <div className="p-8 rounded-2xl bg-emerald-50 border border-emerald-200 text-center">
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-emerald-100 mx-auto mb-4">
          <CheckCircle className="h-8 w-8 text-emerald-600" />
        </div>
        <h3 className="text-xl font-bold text-emerald-800 mb-2">{t("success")}</h3>
        <p className="text-emerald-700 mb-6">
          {t("successDesc")}
        </p>
        <button
          type="button"
          onClick={() => setIsSubmitted(false)}
          className={cn(
            "px-6 py-3 rounded-xl text-sm font-semibold",
            "bg-emerald-600 text-white",
            "hover:bg-emerald-700 transition-all duration-200"
          )}
        >
          {t("sendAnother")}
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      {submitError && (
        <div className="p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3">
          <AlertCircle className="h-5 w-5 text-red-500 flex-shrink-0 mt-0.5" />
          <p className="text-sm text-red-700">{submitError}</p>
        </div>
      )}

      <div>
        <label htmlFor="name" className="block text-sm font-medium text-[#0F172A] mb-2">
          {t("fullName")} <span className="text-red-500">*</span>
        </label>
        <div className="relative">
          <User className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
          <input
            type="text"
            id="name"
            name="name"
            value={formData.name}
            onChange={handleChange}
            placeholder={t("fullNamePlaceholder")}
            className={cn(
              "w-full pl-12 pr-4 py-3 rounded-xl",
              "bg-white border",
              errors.name ? "border-red-300 focus:border-red-500" : "border-[#E2E8F0] focus:border-[#0369A1]",
              "text-[#0F172A] placeholder-[#94A3B8]",
              "focus:outline-none focus:ring-2",
              errors.name ? "focus:ring-red-200" : "focus:ring-[#0369A1]/20",
              "transition-all duration-200"
            )}
          />
        </div>
        {errors.name && (
          <p className="mt-1.5 text-sm text-red-500">{errors.name}</p>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-[#0F172A] mb-2">
            {t("email")} <span className="text-red-500">*</span>
          </label>
          <div className="relative">
            <Mail className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
            <input
              type="email"
              id="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              placeholder={t("emailPlaceholder")}
              className={cn(
                "w-full pl-12 pr-4 py-3 rounded-xl",
                "bg-white border",
                errors.email ? "border-red-300 focus:border-red-500" : "border-[#E2E8F0] focus:border-[#0369A1]",
                "text-[#0F172A] placeholder-[#94A3B8]",
                "focus:outline-none focus:ring-2",
                errors.email ? "focus:ring-red-200" : "focus:ring-[#0369A1]/20",
                "transition-all duration-200"
              )}
            />
          </div>
          {errors.email && (
            <p className="mt-1.5 text-sm text-red-500">{errors.email}</p>
          )}
        </div>

        <div>
          <label htmlFor="phone" className="block text-sm font-medium text-[#0F172A] mb-2">
            {t("phone")} <span className="text-red-500">*</span>
          </label>
          <div className="relative">
            <Phone className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
            <input
              type="tel"
              id="phone"
              name="phone"
              value={formData.phone}
              onChange={handleChange}
              placeholder={t("phonePlaceholder")}
              className={cn(
                "w-full pl-12 pr-4 py-3 rounded-xl",
                "bg-white border",
                errors.phone ? "border-red-300 focus:border-red-500" : "border-[#E2E8F0] focus:border-[#0369A1]",
                "text-[#0F172A] placeholder-[#94A3B8]",
                "focus:outline-none focus:ring-2",
                errors.phone ? "focus:ring-red-200" : "focus:ring-[#0369A1]/20",
                "transition-all duration-200"
              )}
            />
          </div>
          {errors.phone && (
            <p className="mt-1.5 text-sm text-red-500">{errors.phone}</p>
          )}
        </div>
      </div>

      <div>
        <label htmlFor="subject" className="block text-sm font-medium text-[#0F172A] mb-2">
          {t("subject")} <span className="text-red-500">*</span>
        </label>
        <div className="relative">
          <MessageSquare className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
          <select
            id="subject"
            name="subject"
            value={formData.subject}
            onChange={handleChange}
            className={cn(
              "w-full pl-12 pr-4 py-3 rounded-xl appearance-none",
              "bg-white border",
              errors.subject ? "border-red-300 focus:border-red-500" : "border-[#E2E8F0] focus:border-[#0369A1]",
              "text-[#0F172A]",
              "focus:outline-none focus:ring-2",
              errors.subject ? "focus:ring-red-200" : "focus:ring-[#0369A1]/20",
              "transition-all duration-200",
              !formData.subject && "text-[#94A3B8]"
            )}
          >
            <option value="" disabled>{t("subjectPlaceholder")}</option>
            <option value="reservation">{t("subjects.reservation")}</option>
            <option value="support">{t("subjects.support")}</option>
            <option value="feedback">{t("subjects.feedback")}</option>
            <option value="partnership">{t("subjects.partnership")}</option>
            <option value="other">{t("subjects.other")}</option>
          </select>
          <div className="absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none">
            <ChevronDown className="h-4 w-4 text-[#64748B]" aria-hidden="true" />
          </div>
        </div>
        {errors.subject && (
          <p className="mt-1.5 text-sm text-red-500">{errors.subject}</p>
        )}
      </div>

      <div>
        <label htmlFor="message" className="block text-sm font-medium text-[#0F172A] mb-2">
          {t("message")} <span className="text-red-500">*</span>
        </label>
        <textarea
          id="message"
          name="message"
          value={formData.message}
          onChange={handleChange}
          placeholder={t("messagePlaceholder")}
          rows={5}
          className={cn(
            "w-full px-4 py-3 rounded-xl resize-none",
            "bg-white border",
            errors.message ? "border-red-300 focus:border-red-500" : "border-[#E2E8F0] focus:border-[#0369A1]",
            "text-[#0F172A] placeholder-[#94A3B8]",
            "focus:outline-none focus:ring-2",
            errors.message ? "focus:ring-red-200" : "focus:ring-[#0369A1]/20",
            "transition-all duration-200"
          )}
        />
        {errors.message && (
          <p className="mt-1.5 text-sm text-red-500">{errors.message}</p>
        )}
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className={cn(
          "w-full flex items-center justify-center gap-2 px-6 py-4 rounded-xl",
          "text-base font-semibold text-white bg-[#0369A1]",
          "hover:bg-[#0284C7] active:bg-[#075985]",
          "transition-all duration-200",
          "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-2",
          "disabled:opacity-50 disabled:cursor-not-allowed"
        )}
      >
        {isSubmitting ? (
          <>
            <div className="h-5 w-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            {t("submitting")}
          </>
        ) : (
          <>
            <Send className="h-5 w-5" />
            {t("submit")}
          </>
        )}
      </button>

      <p className="text-xs text-[#64748B] text-center">
        {t.rich("termsAgreement", {
          terms: (chunks) => <Link href="/terms" className="text-[#0369A1] hover:underline">{chunks}</Link>,
          privacy: (chunks) => <Link href="/privacy" className="text-[#0369A1] hover:underline">{chunks}</Link>
        })}
      </p>
    </form>
  );
}
