import { BookingStepper } from "@/components/public/BookingStepper";

interface BookingLayoutProps {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}

export default async function BookingLayout({ children, params }: BookingLayoutProps) {
  const { locale } = await params;

  return (
    <div className="min-h-screen bg-slate-50">
      <BookingStepper locale={locale} />
      <main className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8 py-8">
        {children}
      </main>
    </div>
  );
}
