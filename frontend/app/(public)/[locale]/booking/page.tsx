import { redirect } from "next/navigation";

interface BookingPageProps {
  params: Promise<{ locale: string }>;
}

export default async function BookingPage({ params }: BookingPageProps) {
  const { locale } = await params;
  redirect(`/${locale}/booking/step1`);
}
