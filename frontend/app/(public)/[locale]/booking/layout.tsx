import { BookingStepper } from "@/components/public/BookingStepper";

interface BookingLayoutProps {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}

export default async function BookingLayout({ children, params }: BookingLayoutProps) {
  const { locale } = await params;

  const pathname = typeof window !== "undefined" ? window.location.pathname : "";
  const stepMatch = pathname.match(/step(\d)/);
  const currentStep = stepMatch ? (parseInt(stepMatch[1]) as 1 | 2 | 3 | 4) : 1;

  return (
    <div className="min-h-screen bg-slate-50">
      <BookingStepper currentStep={currentStep} />
      <main className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8 py-8">
        {children}
      </main>
    </div>
  );
}
