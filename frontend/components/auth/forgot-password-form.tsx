"use client";

import { FormEvent, useState } from "react";
import { Loader2Icon, MailIcon } from "lucide-react";
import { toast } from "sonner";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { PrincipalScope } from "@/lib/auth/types";

interface ForgotPasswordApiResponse {
  success: boolean;
  message?: string;
}

export function ForgotPasswordForm() {
  const t = useTranslations("auth.forgotPassword");
  const [email, setEmail] = useState("");
  const [principalScope, setPrincipalScope] = useState<PrincipalScope>("Admin");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmitted, setIsSubmitted] = useState(false);

  const onSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!email.trim()) {
      toast.error(t("errors.emailRequired"));
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await fetch("/api/auth/password-reset/request", {
        method: "POST",
        headers: {
          "content-type": "application/json"
        },
        body: JSON.stringify({
          email: email.trim(),
          principalScope
        })
      });

      const payload = (await response.json()) as ForgotPasswordApiResponse;

      if (!response.ok || !payload.success) {
        toast.error(payload.message ?? t("errors.generic"));
        return;
      }

      setIsSubmitted(true);
      toast.success(t("messages.successToast"));
    } catch {
      toast.error(t("errors.generic"));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex items-center justify-center py-4 lg:h-screen">
      <Card className="mx-auto w-96">
        <CardHeader>
          <CardTitle className="text-2xl">{t("title")}</CardTitle>
          <CardDescription>{t("description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="space-y-6">
            <div className="grid gap-2">
              <Label htmlFor="principalScope">{t("labels.accountType")}</Label>
              <select
                id="principalScope"
                className="border-input bg-background h-10 rounded-md border px-3 text-sm"
                value={principalScope}
                onChange={(event) => setPrincipalScope(event.target.value as PrincipalScope)}>
                <option value="Admin">{t("options.admin")}</option>
                <option value="Customer">{t("options.customer")}</option>
              </select>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="email">{t("labels.email")}</Label>
              <div className="relative">
                <MailIcon className="text-muted-foreground absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2" />
                <Input
                  id="email"
                  type="email"
                  autoComplete="email"
                  className="pl-10"
                  placeholder="contact@bundui.com"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  required
                />
              </div>
            </div>

            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <Loader2Icon className="animate-spin" />
                  {t("buttons.loading")}
                </>
              ) : (
                t("buttons.submit")
              )}
            </Button>

            {isSubmitted ? (
              <p className="text-muted-foreground text-center text-sm">
                {t("messages.submitted")}
              </p>
            ) : null}
          </form>
        </CardContent>
        <CardFooter className="flex justify-center">
          <p className="text-sm">
            {t("footer.rememberPassword")}{" "}
            <Link href="/dashboard/login/v2" className="underline">
              {t("footer.loginLink")}
            </Link>
          </p>
        </CardFooter>
      </Card>
    </div>
  );
}
