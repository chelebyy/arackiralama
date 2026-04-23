\"use client\";

import { FormEvent, useState } from \"react\";
import { useRouter } from \"next/navigation\";
import { Loader2Icon } from \"lucide-react\";
import { toast } from \"sonner\";
import { useTranslations } from \"next-intl\";
import { Link } from \"@/i18n/routing\";

import { Button } from \"@/components/ui/button\";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from \"@/components/ui/card\";
import { Input } from \"@/components/ui/input\";
import { Label } from \"@/components/ui/label\";

interface RegisterApiResponse {
  success: boolean;
  message?: string;
}

export function CustomerRegisterForm() {
  const router = useRouter();
  const t = useTranslations(\"auth.register\");

  const [fullName, setFullName] = useState(\"\");
  const [phone, setPhone] = useState(\"\");
  const [email, setEmail] = useState(\"\");
  const [password, setPassword] = useState(\"\");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const onSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!email.trim() || !password) {
      toast.error(t(\"errors.required\"));
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await fetch(\"/api/auth/register\", {
        method: \"POST\",
        headers: {
          \"content-type\": \"application/json\"
        },
        body: JSON.stringify({
          email: email.trim(),
          password,
          fullName: fullName.trim(),
          phone: phone.trim()
        })
      });

      const payload = (await response.json()) as RegisterApiResponse;

      if (!response.ok || !payload.success) {
        toast.error(payload.message ?? t(\"errors.generic\"));
        return;
      }

      toast.success(payload.message ?? \"OK\");
      router.push(\"/dashboard/login/v1\");
      router.refresh();
    } catch {
      toast.error(t(\"errors.generic\"));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className=\"flex items-center justify-center py-4 lg:h-screen\">
      <Card className=\"mx-auto w-96\">
        <CardHeader>
          <CardTitle className=\"text-2xl\">{t(\"title\")}</CardTitle>
          <CardDescription>{t(\"description\")}</CardDescription>
        </CardHeader>
        <CardContent>
          <form className=\"grid gap-4\" onSubmit={onSubmit}>
            <div className=\"grid gap-2\">
              <Label htmlFor=\"fullName\">{t(\"labels.fullName\")}</Label>
              <Input
                id=\"fullName\"
                placeholder=\"Ada Lovelace\"
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
              />
            </div>

            <div className=\"grid gap-2\">
              <Label htmlFor=\"phone\">{t(\"labels.phone\")}</Label>
              <Input
                id=\"phone\"
                placeholder=\"+90 5xx xxx xx xx\"
                value={phone}
                onChange={(event) => setPhone(event.target.value)}
              />
            </div>

            <div className=\"grid gap-2\">
              <Label htmlFor=\"email\">{t(\"labels.email\")}</Label>
              <Input
                id=\"email\"
                type=\"email\"
                autoComplete=\"email\"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </div>

            <div className=\"grid gap-2\">
              <Label htmlFor=\"password\">{t(\"labels.password\")}</Label>
              <Input
                id=\"password\"
                type=\"password\"
                autoComplete=\"new-password\"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </div>

            <Button type=\"submit\" className=\"w-full\" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <Loader2Icon className=\"animate-spin\" />
                  {t(\"buttons.loading\")}
                </>
              ) : (
                t(\"buttons.submit\")
              )}
            </Button>
          </form>

          <div className=\"mt-4 text-center text-sm\">
            {t(\"footer.alreadyRegistered\")}{\" \"}
            <Link href=\"/dashboard/login/v1\" className=\"underline\">
              {t(\"footer.loginLink\")}
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
