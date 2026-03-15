"use client";

import { FormEvent, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Loader2Icon } from "lucide-react";
import { toast } from "sonner";

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

interface ResetPasswordApiResponse {
  success: boolean;
  message?: string;
}

interface ResetPasswordFormProps {
  initialToken?: string;
  initialScope?: PrincipalScope;
}

export function ResetPasswordForm({ initialToken = "", initialScope = "Admin" }: ResetPasswordFormProps) {
  const router = useRouter();

  const [token, setToken] = useState(initialToken);
  const [principalScope, setPrincipalScope] = useState<PrincipalScope>(initialScope);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const loginHref = useMemo(
    () => (principalScope === "Customer" ? "/dashboard/login/v1" : "/dashboard/login/v2"),
    [principalScope]
  );

  const onSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!token.trim() || !newPassword) {
      toast.error("Token ve yeni parola zorunludur.");
      return;
    }

    if (newPassword !== confirmPassword) {
      toast.error("Parola doğrulaması eşleşmiyor.");
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await fetch("/api/auth/password-reset/confirm", {
        method: "POST",
        headers: {
          "content-type": "application/json"
        },
        body: JSON.stringify({
          token: token.trim(),
          principalScope,
          newPassword
        })
      });

      const payload = (await response.json()) as ResetPasswordApiResponse;

      if (!response.ok || !payload.success) {
        toast.error(payload.message ?? "Parola sıfırlanamadı.");
        return;
      }

      toast.success(payload.message ?? "Parola başarıyla güncellendi.");
      router.push(loginHref);
      router.refresh();
    } catch {
      toast.error("Parola sıfırlama sırasında beklenmeyen bir hata oluştu.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex items-center justify-center py-4 lg:h-screen">
      <Card className="mx-auto w-[26rem]">
        <CardHeader>
          <CardTitle className="text-2xl">Reset Password</CardTitle>
          <CardDescription>
            Use the token from your email and set a new password.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="space-y-4">
            <div className="grid gap-2">
              <Label htmlFor="principalScope">Account Type</Label>
              <select
                id="principalScope"
                className="border-input bg-background h-10 rounded-md border px-3 text-sm"
                value={principalScope}
                onChange={(event) => setPrincipalScope(event.target.value as PrincipalScope)}>
                <option value="Admin">Admin / SuperAdmin</option>
                <option value="Customer">Customer</option>
              </select>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="token">Reset Token</Label>
              <Input
                id="token"
                placeholder="Paste token from reset email"
                value={token}
                onChange={(event) => setToken(event.target.value)}
                required
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="newPassword">New Password</Label>
              <Input
                id="newPassword"
                type="password"
                autoComplete="new-password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                required
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="confirmPassword">Confirm Password</Label>
              <Input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                required
              />
            </div>

            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <Loader2Icon className="animate-spin" />
                  Please wait
                </>
              ) : (
                "Reset Password"
              )}
            </Button>
          </form>
        </CardContent>
        <CardFooter className="justify-center">
          <Link href={loginHref} className="text-sm underline">
            Back to login
          </Link>
        </CardFooter>
      </Card>
    </div>
  );
}
