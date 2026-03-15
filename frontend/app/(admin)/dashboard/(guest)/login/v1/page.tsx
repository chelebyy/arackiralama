import { generateMeta } from "@/lib/utils";
import { LoginForm } from "@/components/auth/login-form";

export function generateMetadata() {
  return generateMeta({
    title: "Customer Login",
    description: "Customer sign-in page for dashboard auth integration.",
    canonical: "/dashboard/login/v1"
  });
}

export default function Page() {
  return (
    <LoginForm
      principalScope="Customer"
      title="Customer Login"
      description="Sign in with your customer account."
      successRedirect="/dashboard/customer-portal"
      registerHref="/dashboard/register/v1"
    />
  );
}
