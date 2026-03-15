import { ResetPasswordForm } from "@/components/auth/reset-password-form";
import type { PrincipalScope } from "@/lib/auth/types";

interface ResetPasswordPageProps {
  searchParams: Promise<{
    token?: string;
    scope?: string;
  }>;
}

export default async function Page({ searchParams }: ResetPasswordPageProps) {
  const resolvedSearchParams = await searchParams;

  const scope =
    resolvedSearchParams.scope?.toLowerCase() === "customer"
      ? ("Customer" as PrincipalScope)
      : ("Admin" as PrincipalScope);

  return <ResetPasswordForm initialToken={resolvedSearchParams.token} initialScope={scope} />;
}
