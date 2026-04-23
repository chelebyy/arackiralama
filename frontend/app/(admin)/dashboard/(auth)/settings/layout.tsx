"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

export default function SettingsLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const segment = pathname.split("/settings/")[1]?.split("/")[0] || "feature-flags";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Ayarlar</h1>
      </div>
      <Tabs value={segment} className="w-full">
        <TabsList>
          <TabsTrigger value="feature-flags" asChild>
            <Link href="/dashboard/settings/feature-flags">Özellik Bayrakları</Link>
          </TabsTrigger>
          <TabsTrigger value="audit-logs" asChild>
            <Link href="/dashboard/settings/audit-logs">Denetim Kayıtları</Link>
          </TabsTrigger>
        </TabsList>
      </Tabs>
      <div className="mt-4">{children}</div>
    </div>
  );
}
