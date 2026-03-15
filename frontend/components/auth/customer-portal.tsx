"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { LogOutIcon } from "lucide-react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

interface MeResponse {
  success: boolean;
  data?: {
    email?: string;
    role?: string;
  };
}

export function CustomerPortal() {
  const router = useRouter();
  const [email, setEmail] = useState<string>("customer");
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const response = await fetch("/api/auth/me", {
          method: "GET",
          cache: "no-store"
        });

        if (!response.ok) {
          return;
        }

        const payload = (await response.json()) as MeResponse;

        if (mounted && payload.success && payload.data?.email) {
          setEmail(payload.data.email);
        }
      } catch {
        // no-op
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, []);

  const onLogout = async () => {
    setIsLoading(true);

    try {
      await fetch("/api/auth/logout", {
        method: "POST"
      });

      toast.success("Çıkış yapıldı.");
      router.push("/dashboard/login/v1");
      router.refresh();
    } catch {
      toast.error("Çıkış işlemi başarısız.");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="container mx-auto flex min-h-screen items-center justify-center p-6">
      <Card className="w-full max-w-xl">
        <CardHeader>
          <CardTitle>Customer Portal</CardTitle>
          <CardDescription>
            You are signed in as customer: <span className="font-medium">{email}</span>
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-muted-foreground text-sm">
            This route verifies customer session continuity, refresh handling, and RBAC separation from admin
            dashboard routes.
          </p>

          <Button onClick={onLogout} disabled={isLoading}>
            <LogOutIcon />
            {isLoading ? "Signing out..." : "Log out"}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
