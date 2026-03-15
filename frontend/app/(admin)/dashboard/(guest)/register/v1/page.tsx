import { generateMeta } from "@/lib/utils";
import { CustomerRegisterForm } from "@/components/auth/customer-register-form";

export async function generateMetadata() {
  return generateMeta({
    title: "Customer Register",
    description: "Create a customer account.",
    canonical: "/dashboard/register/v1"
  });
}

export default function Page() {
  return <CustomerRegisterForm />;
}
