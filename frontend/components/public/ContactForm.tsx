"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import { Send, User, Mail, Phone, MessageSquare, CheckCircle, AlertCircle } from "lucide-react";

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
      newErrors.name = "Name is required";
    } else if (formData.name.length < 2) {
      newErrors.name = "Name must be at least 2 characters";
    }

    if (!formData.email.trim()) {
      newErrors.email = "Email is required";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      newErrors.email = "Please enter a valid email address";
    }

    if (!formData.phone.trim()) {
      newErrors.phone = "Phone number is required";
    } else if (!/^\+?[\d\s-]{8,}$/.test(formData.phone.replace(/\s/g, ""))) {
      newErrors.phone = "Please enter a valid phone number";
    }

    if (!formData.subject.trim()) {
      newErrors.subject = "Subject is required";
    }

    if (!formData.message.trim()) {
      newErrors.message = "Message is required";
    } else if (formData.message.length < 10) {
      newErrors.message = "Message must be at least 10 characters";
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
    } catch {
      setSubmitError("Failed to send message. Please try again later.");
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
        <h3 className="text-xl font-bold text-emerald-800 mb-2">Message Sent Successfully</h3>
        <p className="text-emerald-700 mb-6">
          Thank you for contacting us. We have received your message and will get back to you within 24 hours.
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
          Send Another Message
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
          Full Name <span className="text-red-500">*</span>
        </label>
        <div className="relative">
          <User className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
          <input
            type="text"
            id="name"
            name="name"
            value={formData.name}
            onChange={handleChange}
            placeholder="Enter your full name"
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
            Email Address <span className="text-red-500">*</span>
          </label>
          <div className="relative">
            <Mail className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
            <input
              type="email"
              id="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              placeholder="your@email.com"
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
            Phone Number <span className="text-red-500">*</span>
          </label>
          <div className="relative">
            <Phone className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#94A3B8]" />
            <input
              type="tel"
              id="phone"
              name="phone"
              value={formData.phone}
              onChange={handleChange}
              placeholder="+90 555 123 45 67"
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
          Subject <span className="text-red-500">*</span>
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
            <option value="" disabled>Select a subject</option>
            <option value="reservation">Reservation Inquiry</option>
            <option value="support">Customer Support</option>
            <option value="feedback">Feedback</option>
            <option value="partnership">Business Partnership</option>
            <option value="other">Other</option>
          </select>
          <div className="absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none">
            <svg className="h-4 w-4 text-[#64748B]" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </div>
        {errors.subject && (
          <p className="mt-1.5 text-sm text-red-500">{errors.subject}</p>
        )}
      </div>

      <div>
        <label htmlFor="message" className="block text-sm font-medium text-[#0F172A] mb-2">
          Message <span className="text-red-500">*</span>
        </label>
        <textarea
          id="message"
          name="message"
          value={formData.message}
          onChange={handleChange}
          placeholder="How can we help you?"
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
            Sending Message...
          </>
        ) : (
          <>
            <Send className="h-5 w-5" />
            Send Message
          </>
        )}
      </button>

      <p className="text-xs text-[#64748B] text-center">
        By submitting this form, you agree to our{" "}
        <a href="/terms" className="text-[#0369A1] hover:underline">Terms of Service</a>
        {" "}and{" "}
        <a href="/privacy" className="text-[#0369A1] hover:underline">Privacy Policy</a>.
      </p>
    </form>
  );
}
