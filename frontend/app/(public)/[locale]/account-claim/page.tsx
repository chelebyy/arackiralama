"use client";

import { useTranslations } from "next-intl";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { callAccountClaimEndpoint } from "@/lib/auth/backend";

export const dynamic = "force-dynamic";

type Status = "idle" | "submitting" | "success" | "failed" | "invalid";

export default function AccountClaimPage() {
  const t = useTranslations("accountClaim");
  const searchParams = useSearchParams();
  const token = searchParams?.get("token") ?? "";

  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [status, setStatus] = useState<Status>(token ? "idle" : "invalid");
  const [validationError, setValidationError] = useState<string | null>(null);

  if (!token) {
    return (
      <section className="mx-auto max-w-2xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="bg-white border border-[#E2E8F0] rounded-2xl p-8">
          <h1 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-3">
            {t("title")}
          </h1>
          <p className="text-[#64748B] mb-6">{t("validation.tokenRequired")}</p>
          <Link
            href="/"
            className="inline-flex items-center justify-center rounded-full border border-[#E2E8F0] px-5 py-2.5 text-sm font-medium text-[#0F172A] hover:bg-[#F8FAFC]"
          >
            {t("buttons.backToHome")}
          </Link>
        </div>
      </section>
    );
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setValidationError(null);

    if (!newPassword) {
      setValidationError(t("validation.passwordRequired"));
      return;
    }
    if (newPassword.length < 8) {
      setValidationError(t("validation.passwordMinLength"));
      return;
    }
    if (newPassword !== confirmPassword) {
      setValidationError(t("validation.passwordMismatch"));
      return;
    }

    try {
      setStatus("submitting");
      const { backendResponse } = await callAccountClaimEndpoint({
        token,
        newPassword,
      });
      if (backendResponse.ok) {
        setStatus("success");
        return;
      }
      setStatus("failed");
    } catch {
      setStatus("failed");
    }
  };

  if (status === "success") {
    return (
      <section className="mx-auto max-w-2xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
        <div className="bg-white border border-emerald-200 rounded-2xl p-8">
          <h1 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-3">
            {t("title")}
          </h1>
          <p className="text-[#475569] mb-6">{t("status.success")}</p>
          <Link
            href="/"
            className="inline-flex items-center justify-center rounded-full bg-[#0369A1] px-5 py-2.5 text-sm font-medium text-white hover:bg-[#075985]"
          >
            {t("buttons.backToHome")}
          </Link>
        </div>
      </section>
    );
  }

  return (
    <section className="mx-auto max-w-2xl px-4 sm:px-6 lg:px-8 py-16 lg:py-24">
      <div className="bg-white border border-[#E2E8F0] rounded-2xl p-8">
        <h1 className="text-2xl lg:text-3xl font-bold text-[#0F172A] mb-3">
          {t("title")}
        </h1>
        <p className="text-[#64748B] mb-2">{t("subtitle")}</p>
        <p className="text-sm text-[#94A3B8] mb-6">{t("description")}</p>

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label
              htmlFor="account-claim-new-password"
              className="block text-sm font-medium text-[#0F172A] mb-2"
            >
              {t("fields.newPassword")}
            </label>
            <input
              id="account-claim-new-password"
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              required
              minLength={8}
              className="w-full rounded-lg border border-[#E2E8F0] bg-white px-4 py-2.5 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#0369A1]"
            />
            <p className="mt-1 text-xs text-[#94A3B8]">{t("fields.passwordHint")}</p>
          </div>

          <div>
            <label
              htmlFor="account-claim-confirm-password"
              className="block text-sm font-medium text-[#0F172A] mb-2"
            >
              {t("fields.confirmPassword")}
            </label>
            <input
              id="account-claim-confirm-password"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              required
              minLength={8}
              className="w-full rounded-lg border border-[#E2E8F0] bg-white px-4 py-2.5 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#0369A1]"
            />
          </div>

          {validationError && (
            <p className="text-sm text-red-600" role="alert">
              {validationError}
            </p>
          )}
          {(status === "failed" || status === "invalid") && (
            <p className="text-sm text-red-600" role="alert">
              {status === "invalid" ? t("validation.tokenRequired") : t("status.failed")}
            </p>
          )}

          <button
            type="submit"
            disabled={status === "submitting"}
            className="inline-flex items-center justify-center rounded-full bg-[#0369A1] px-5 py-2.5 text-sm font-medium text-white hover:bg-[#075985] disabled:opacity-70"
          >
            {status === "submitting"
              ? t("buttons.submitting")
              : t("buttons.submit")}
          </button>
        </form>
      </div>
    </section>
  );
}