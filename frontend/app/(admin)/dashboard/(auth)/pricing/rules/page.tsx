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
import { usePricingRules, useAdminVehicleGroups } from "@/hooks/admin";
import type { PricingRule } from "@/lib/api/admin/types";
import dynamic from "next/dynamic";

const PricingRuleDialog = dynamic(() => import("@/components/admin/dialogs/PricingRuleDialog"), {
  ssr: false,
});

export default function PricingRulesPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<PricingRule | undefined>();
  
  const { rules, isLoading, isError, mutate } = usePricingRules();
  const { groups } = useAdminVehicleGroups();

  const handleEdit = (rule: PricingRule) => {
    setEditingRule(rule);
    setDialogOpen(true);
  };

  const handleCreate = () => {
    setEditingRule(undefined);
    setDialogOpen(true);
  };

  const handleSuccess = () => {
    setDialogOpen(false);
    setEditingRule(undefined);
    mutate();
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Fiyat Kuralları</CardTitle>
          <Button size="sm" onClick={handleCreate}>
            <Plus className="h-4 w-4 mr-1" /> Yeni Kural Ekle
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
                  <TableHead>Araç Grubu</TableHead>
                  <TableHead>Başlangıç</TableHead>
                  <TableHead>Bitiş</TableHead>
                  <TableHead className="text-right">Günlük Fiyat</TableHead>
                  <TableHead className="text-right">Çarpan</TableHead>
                  <TableHead className="text-right">Öncelik</TableHead>
                  <TableHead>Hesaplama</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rules.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-medium">{r.vehicleGroupId}</TableCell>
                    <TableCell>{r.startDate}</TableCell>
                    <TableCell>{r.endDate}</TableCell>
                    <TableCell className="text-right">
                      ₺{r.dailyPrice.toLocaleString("tr-TR")}
                    </TableCell>
                    <TableCell className="text-right">{r.multiplier}x</TableCell>
                    <TableCell className="text-right">{r.priority}</TableCell>
                    <TableCell>
                      <Badge variant="outline">
                        {r.calculationType === "multiplier" ? "Çarpan" : "Sabit"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="ghost" onClick={() => handleEdit(r)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {rules.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={8}
                      className="text-center text-muted-foreground h-24"
                    >
                      Fiyat kuralı bulunamadı
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
      <PricingRuleDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        rule={editingRule}
        onSuccess={handleSuccess}
        vehicleGroups={groups}
      />
    </Card>
  );
}
