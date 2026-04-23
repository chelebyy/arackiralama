"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

export default function UsersLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const segment = pathname.split("/users/")[1]?.split("/")[0] || "customers";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Kullanıcılar</h1>
      </div>
      <Tabs value={segment} className="w-full">
        <TabsList>
          <TabsTrigger value="customers" asChild>
            <Link href="/dashboard/users/customers">Müşteriler</Link>
          </TabsTrigger>
          <TabsTrigger value="admins" asChild>
            <Link href="/dashboard/users/admins">Admin Kullanıcılar</Link>
          </TabsTrigger>
        </TabsList>
      </Tabs>
      <div className="mt-4">{children}</div>
    </div>
  );
}
