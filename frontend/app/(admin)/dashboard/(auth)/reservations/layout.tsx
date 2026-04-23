"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

export default function ReservationsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const activeTab = pathname.includes("/calendar") ? "calendar" : "list";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Rezervasyonlar</h1>
      </div>
      <Tabs value={activeTab} className="w-full">
        <TabsList>
          <TabsTrigger value="list" asChild>
            <Link href="/dashboard/reservations">Liste</Link>
          </TabsTrigger>
          <TabsTrigger value="calendar" asChild>
            <Link href="/dashboard/reservations/calendar">Takvim</Link>
          </TabsTrigger>
        </TabsList>
      </Tabs>
      <div className="mt-4">{children}</div>
    </div>
  );
}
