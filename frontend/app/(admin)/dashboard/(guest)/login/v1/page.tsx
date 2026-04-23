import { getTranslations } from "next-intl/server";
import { generateMeta } from "@/lib/utils";
import { LoginForm } from "@/components/auth/login-form";

export async function generateMetadata() {
  const t = await getTranslations("auth.login.v1");
  
  return generateMeta({
    title: t("title"),
    description: t("description"),
    canonical: "/dashboard/login/v1"
  });
}

export default async function Page() {
  const t = await getTranslations("auth.login.v1");

  return (
    <LoginForm
      principalScope="Customer"
      title={t("title")}
      description={t("description")}
      successRedirect="/dashboard/customer-portal"
      registerHref="/dashboard/register/v1"
    />
  );
}
