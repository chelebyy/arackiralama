"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

export default function PricingLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const segment = pathname.split("/pricing/")[1]?.split("/")[0] || "rules";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Fiyatlandırma</h1>
      </div>
      <Tabs value={segment} className="w-full">
        <TabsList>
          <TabsTrigger value="rules" asChild>
            <Link href="/dashboard/pricing/rules">Fiyat Kuralları</Link>
          </TabsTrigger>
          <TabsTrigger value="campaigns" asChild>
            <Link href="/dashboard/pricing/campaigns">Kampanyalar</Link>
          </TabsTrigger>
        </TabsList>
      </Tabs>
      <div className="mt-4">{children}</div>
    </div>
  );
}
