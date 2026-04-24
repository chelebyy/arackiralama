"use client";

import { useState } from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Pencil, Plus } from "lucide-react";
import { useCampaigns } from "@/hooks/admin";
import type { Campaign } from "@/lib/api/admin/types";
import dynamic from "next/dynamic";

const CampaignDialog = dynamic(() => import("@/components/admin/dialogs/CampaignDialog"), {
  ssr: false,
});

export default function CampaignsPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingCampaign, setEditingCampaign] = useState<Campaign | undefined>();
  
  const { campaigns, isLoading, isError, mutate } = useCampaigns();

  const handleEdit = (campaign: Campaign) => {
    setEditingCampaign(campaign);
    setDialogOpen(true);
  };

  const handleCreate = () => {
    setEditingCampaign(undefined);
    setDialogOpen(true);
  };

  const handleSuccess = () => {
    setDialogOpen(false);
    setEditingCampaign(undefined);
    mutate();
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Kampanyalar</CardTitle>
          <Button size="sm" onClick={handleCreate}>
            <Plus className="h-4 w-4 mr-1" /> Yeni Kampanya
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : isError ? (
          <div className="text-sm text-destructive">Veri yüklenirken hata oluştu</div>
        ) : (
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Kod</TableHead>
                  <TableHead>Ad</TableHead>
                  <TableHead>İndirim Tipi</TableHead>
                  <TableHead className="text-right">İndirim Değeri</TableHead>
                  <TableHead className="text-right">Min. Gün</TableHead>
                  <TableHead>Geçerlilik</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {campaigns.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-medium">{c.code}</TableCell>
                    <TableCell>{c.name}</TableCell>
                    <TableCell>
                      <Badge variant="outline">
                        {c.discountType === "PERCENTAGE" ? "Yüzde" : "Sabit"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      {c.discountType === "PERCENTAGE"
                        ? `%${c.discountValue}`
                        : `₺${c.discountValue.toLocaleString("tr-TR")}`}
                    </TableCell>
                    <TableCell className="text-right">{c.minRentalDays || "—"}</TableCell>
                    <TableCell>
                      {c.validFrom} — {c.validUntil}
                    </TableCell>
                    <TableCell>
                      <Badge variant={c.isActive ? "default" : "outline"}>
                        {c.isActive ? "Aktif" : "Pasif"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="ghost" onClick={() => handleEdit(c)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {campaigns.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={8}
                      className="text-center text-muted-foreground h-24"
                    >
                      Kampanya bulunamadı
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
      <CampaignDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        campaign={editingCampaign}
        onSuccess={handleSuccess}
      />
    </Card>
  );
}
