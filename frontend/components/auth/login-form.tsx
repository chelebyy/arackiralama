"use client";

import { FormEvent, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Loader2Icon } from "lucide-react";
import { toast } from "sonner";
import { useTranslations } from "next-intl";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { PrincipalScope } from "@/lib/auth/types";

interface LoginFormProps {
  principalScope: PrincipalScope;
  title: string;
  description: string;
  successRedirect: string;
  registerHref?: string;
}

interface LoginApiResponse {
  success: boolean;
  message?: string;
}

export function LoginForm({
  principalScope,
  title,
  description,
  successRedirect,
  registerHref
}: LoginFormProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = useTranslations("auth.login");

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const onSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!email.trim() || !password) {
      toast.error(t("errors.required"));
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: {
          "content-type": "application/json"
        },
        body: JSON.stringify({
          principalScope,
          email: email.trim(),
          password
        })
      });

      const payload = (await response.json()) as LoginApiResponse;

      if (!response.ok || !payload.success) {
        toast.error(payload.message ?? "Login Error");
        return;
      }

      toast.success("OK");

      const nextPath = searchParams.get("next");
      if (nextPath && nextPath.startsWith("/dashboard") && principalScope === "Admin") {
        router.push(nextPath);
      } else {
        router.push(successRedirect);
      }

      router.refresh();
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
          <CardTitle className="text-2xl">{title}</CardTitle>
          <CardDescription>{description}</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4" onSubmit={onSubmit}>
            <div className="grid gap-2">
              <Label htmlFor="email">{t("labels.email")}</Label>
              <Input
                id="email"
                type="email"
                placeholder="contact@bundui.com"
                autoComplete="email"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </div>

            <div className="grid gap-2">
              <div className="flex items-center">
                <Label htmlFor="password">{t("labels.password")}</Label>
                <Link href="/dashboard/forgot-password" className="ml-auto inline-block text-sm underline">
                  {t("forgotPassword")}
                </Link>
              </div>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
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
          </form>

          {registerHref ? (
            <div className="mt-4 text-center text-sm">
              {t("footer.needAccount")}{" "}
              <Link href={registerHref} className="underline">
                {t("footer.registerLink")}
              </Link>
            </div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}
