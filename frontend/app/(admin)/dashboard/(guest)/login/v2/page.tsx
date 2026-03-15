import { generateMeta } from "@/lib/utils";
import { LoginForm } from "@/components/auth/login-form";

export function generateMetadata() {
  return generateMeta({
    title: "Admin Login",
    description: "Admin and SuperAdmin sign-in page.",
    canonical: "/dashboard/login/v2"
  });
}

export default function Page() {
  return (
    <LoginForm
      principalScope="Admin"
      title="Admin Login"
      description="Sign in with your admin account."
      successRedirect="/dashboard/default"
    />
  );
}
