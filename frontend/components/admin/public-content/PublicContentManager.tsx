"use client";

import { FileText, Phone } from "lucide-react";
import useSWR from "swr";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getAdminPublicContent } from "@/lib/api/admin/publicContent";
import type { AdminPublicContent } from "@/lib/api/admin/types";

const PUBLIC_CONTENT_CACHE_KEY = "admin-public-content";

export default function PublicContentManager() {
  const { data, error, isLoading } = useSWR<AdminPublicContent>(
    PUBLIC_CONTENT_CACHE_KEY,
    () => getAdminPublicContent(),
  );
  const hasLoadError = !data && (error || !isLoading);

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold tracking-tight">İçerik Yönetimi</h1>

      {hasLoadError ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">İçerik Yönetimi</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-sm text-destructive">İçerik verisi yüklenemedi.</div>
          </CardContent>
        </Card>
      ) : (
        <Tabs defaultValue="pages" className="space-y-4">
          {error ? (
            <div role="status" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              İçerik yenilenemedi. Son kayıtlar gösteriliyor.
            </div>
          ) : null}

          <TabsList>
            <TabsTrigger value="pages">
              <FileText className="mr-2 h-4 w-4" />
              Sayfalar
            </TabsTrigger>
            <TabsTrigger value="contact">
              <Phone className="mr-2 h-4 w-4" />
              İletişim
            </TabsTrigger>
          </TabsList>

          <TabsContent value="pages">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Sayfalar</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {isLoading || !data ? (
                  <>
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-16 w-full" />
                  </>
                ) : (
                  <div className="text-sm text-muted-foreground">{data.pages.length} sayfa</div>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="contact">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">İletişim</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-3 text-sm text-muted-foreground md:grid-cols-3">
                {isLoading || !data ? (
                  <>
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                  </>
                ) : (
                  <>
                    <div>{data.contactPageChannels.length} kanal</div>
                    <div>{data.contactPageOffices.length} ofis</div>
                    <div>{data.contactPageWorkingHours.length} çalışma saati</div>
                  </>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      )}
    </div>
  );
}
