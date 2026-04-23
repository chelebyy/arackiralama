"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { useFeatureFlags, mutateUpdateFeatureFlag } from "@/hooks/admin";
import { toast } from "sonner";

export default function FeatureFlagsPage() {
  const { flags, isLoading, isError, mutate } = useFeatureFlags();

  const handleToggle = async (id: string, enabled: boolean) => {
    try {
      await mutateUpdateFeatureFlag(id, enabled);
      toast.success("Özellik bayrağı güncellendi");
      mutate();
    } catch {
      toast.error("Güncelleme başarısız");
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Özellik Bayrakları</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : isError ? (
          <div className="text-sm text-destructive">Veri yüklenirken hata oluştu</div>
        ) : (
          <div className="space-y-4">
            {flags.map((f) => (
              <div
                key={f.id}
                className="flex items-center justify-between rounded-lg border p-4"
              >
                <div className="space-y-0.5">
                  <div className="text-sm font-medium">{f.name}</div>
                  <div className="text-xs text-muted-foreground">{f.description}</div>
                </div>
                <Switch
                  checked={f.enabled}
                  onCheckedChange={(checked) => handleToggle(f.id, checked)}
                />
              </div>
            ))}
            {flags.length === 0 && (
              <div className="text-muted-foreground text-sm">
                Özellik bayrağı bulunamadı
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
