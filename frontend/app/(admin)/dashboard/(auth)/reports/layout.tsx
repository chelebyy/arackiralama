"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

export default function ReportsLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const segment = pathname.split("/reports/")[1]?.split("/")[0] || "revenue";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Raporlar</h1>
      </div>
      <Tabs value={segment} className="w-full">
        <TabsList>
          <TabsTrigger value="revenue" asChild>
            <Link href="/dashboard/reports/revenue">Gelir</Link>
          </TabsTrigger>
          <TabsTrigger value="occupancy" asChild>
            <Link href="/dashboard/reports/occupancy">Doluluk</Link>
          </TabsTrigger>
          <TabsTrigger value="popular" asChild>
            <Link href="/dashboard/reports/popular">Popüler Araçlar</Link>
          </TabsTrigger>
        </TabsList>
      </Tabs>
      <div className="mt-4">{children}</div>
    </div>
  );
}
