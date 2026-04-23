"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

export default function FleetLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const segment = pathname.split("/fleet/")[1]?.split("/")[0] || "vehicles";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Filo Yönetimi</h1>
      </div>
      <Tabs value={segment} className="w-full">
        <TabsList>
          <TabsTrigger value="vehicles" asChild>
            <Link href="/dashboard/fleet/vehicles">Araçlar</Link>
          </TabsTrigger>
          <TabsTrigger value="groups" asChild>
            <Link href="/dashboard/fleet/groups">Gruplar</Link>
          </TabsTrigger>
          <TabsTrigger value="offices" asChild>
            <Link href="/dashboard/fleet/offices">Ofisler</Link>
          </TabsTrigger>
          <TabsTrigger value="maintenance" asChild>
            <Link href="/dashboard/fleet/maintenance">Bakım</Link>
          </TabsTrigger>
        </TabsList>
      </Tabs>
      <div className="mt-4">{children}</div>
    </div>
  );
}
