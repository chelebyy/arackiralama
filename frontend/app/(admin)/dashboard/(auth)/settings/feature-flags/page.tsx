"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { useFeatureFlags, mutateUpdateFeatureFlag } from "@/hooks/admin";
import { toast } from "sonner";

const PAYMENT_FLAG_LABELS: Record<string, { title: string; description: string; disabled?: boolean }> = {
  EnableCreditCardPayment: {
    title: "Kredi Kartı",
    description: "Public ödeme ekranında kredi kartı seçeneğini gösterir.",
  },
  EnableDebitCardPayment: {
    title: "Banka Kartı",
    description: "Public ödeme ekranında banka kartı seçeneğini gösterir.",
  },
  EnableUnpaidReservationRequest: {
    title: "Ödemeden Rezervasyon",
    description: "Public ödeme ekranında ödeme yapmadan talep seçeneğini gösterir.",
  },
  EnablePayPalPayment: {
    title: "PayPal",
    description: "Provider entegrasyonu olmadığı için şimdilik public tarafta kullanılamaz.",
    disabled: true,
  },
};

export default function FeatureFlagsPage() {
  const { flags, isLoading, isError, mutate } = useFeatureFlags();
  const paymentFlags = flags.filter((flag) => flag.name in PAYMENT_FLAG_LABELS);
  const otherFlags = flags.filter((flag) => !(flag.name in PAYMENT_FLAG_LABELS));

  const handleToggle = async (name: string, enabled: boolean) => {
    try {
      await mutateUpdateFeatureFlag(name, enabled);
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
            {paymentFlags.length > 0 && (
              <section className="space-y-3">
                <div>
                  <h2 className="text-sm font-semibold">Ödeme Yöntemleri</h2>
                  <p className="text-xs text-muted-foreground">
                    Public rezervasyon ekranında müşteriye gösterilen ödeme seçenekleri.
                  </p>
                </div>
                {paymentFlags.map((f) => {
                  const label = PAYMENT_FLAG_LABELS[f.name];
                  return (
                    <div
                      key={f.name}
                      className="flex items-center justify-between rounded-lg border p-4"
                    >
                      <div className="space-y-0.5">
                        <div className="text-sm font-medium">{label.title}</div>
                        <div className="text-xs text-muted-foreground">{label.description}</div>
                      </div>
                      <Switch
                        checked={f.enabled}
                        disabled={label.disabled}
                        onCheckedChange={(checked) => handleToggle(f.name, checked)}
                      />
                    </div>
                  );
                })}
              </section>
            )}
            {otherFlags.map((f) => (
              <div
                key={f.name}
                className="flex items-center justify-between rounded-lg border p-4"
              >
                <div className="space-y-0.5">
                  <div className="text-sm font-medium">{f.name}</div>
                  <div className="text-xs text-muted-foreground">{f.description}</div>
                </div>
                <Switch
                  checked={f.enabled}
                  onCheckedChange={(checked) => handleToggle(f.name, checked)}
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
