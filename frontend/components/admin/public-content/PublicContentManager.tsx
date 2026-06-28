"use client";

import { FileText, Phone } from "lucide-react";
import useSWR from "swr";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getAdminPublicContent } from "@/lib/api/admin/publicContent";
import type { AdminPublicContent } from "@/lib/api/admin/types";
import ContactContentEditor from "./ContactContentEditor";
import PageContentEditor from "./PageContentEditor";

const PUBLIC_CONTENT_CACHE_KEY = "admin-public-content";

export default function PublicContentManager() {
  const { data, error, isLoading, mutate } = useSWR<AdminPublicContent>(
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
            {isLoading || !data ? (
              <div className="space-y-3 rounded-md border p-4">
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-24 w-full" />
              </div>
            ) : (
              <PageContentEditor
                content={data}
                onContentChange={(nextContent) => {
                  void mutate(nextContent, false);
                }}
              />
            )}
          </TabsContent>

          <TabsContent value="contact">
            {isLoading || !data ? (
              <div className="space-y-3 rounded-md border p-4">
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-24 w-full" />
              </div>
            ) : (
              <ContactContentEditor
                content={data}
                onContentChange={(nextContent) => {
                  void mutate(nextContent, false);
                }}
              />
            )}
          </TabsContent>
        </Tabs>
      )}
    </div>
  );
}
