\"use client\";

import { useEffect, useState } from \"react\";
import { useRouter } from \"next/navigation\";
import { LogOutIcon } from \"lucide-react\";
import { toast } from \"sonner\";
import { useTranslations } from \"next-intl\";

import { Button } from \"@/components/ui/button\";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from \"@/components/ui/card\";

interface MeResponse {
  success: boolean;
  data?: {
    email?: string;
    role?: string;
  };
}

export function CustomerPortal() {
  const router = useRouter();
  const t = useTranslations(\"auth\");
  const [email, setEmail] = useState<string>(\"customer\");
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const response = await fetch(\"/api/auth/me\", {
          method: \"GET\",
          cache: \"no-store\"
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
      await fetch(\"/api/auth/logout\", {
        method: \"POST\"
      });

      toast.success(t(\"logout.success\"));
      router.push(\"/dashboard/login/v1\");
      router.refresh();
    } catch {
      toast.error(t(\"logout.error\"));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className=\"container mx-auto flex min-h-screen items-center justify-center p-6\">
      <Card className=\"w-full max-w-xl\">
        <CardHeader>
          <CardTitle>{t(\"portal.title\")}</CardTitle>
          <CardDescription>
            {t(\"portal.description\", { email })}
          </CardDescription>
        </CardHeader>
        <CardContent className=\"space-y-4\">
          <p className=\"text-muted-foreground text-sm\">
            {t(\"portal.explanation\")}
          </p>

          <Button onClick={onLogout} disabled={isLoading}>
            <LogOutIcon />
            {isLoading ? t(\"logout.loading\") : t(\"logout.button\")}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
