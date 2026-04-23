"use client";

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
import { Pencil, Plane, Hotel } from "lucide-react";
import { useAdminOffices } from "@/hooks/admin";

export default function OfficesPage() {
  const { offices, isLoading, isError } = useAdminOffices();

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Ofis Listesi</CardTitle>
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
                  <TableHead>Şehir</TableHead>
                  <TableHead>Telefon</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Tip</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead className="text-right">İşlemler</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {offices.map((o) => (
                  <TableRow key={o.id}>
                    <TableCell className="font-medium">{o.code}</TableCell>
                    <TableCell>{o.name}</TableCell>
                    <TableCell>{o.city}</TableCell>
                    <TableCell>{o.phone}</TableCell>
                    <TableCell>{o.email}</TableCell>
                    <TableCell>
                      {o.type === "airport" && (
                        <Badge variant="secondary" className="gap-1">
                          <Plane className="h-3 w-3" /> Havalimanı
                        </Badge>
                      )}
                      {o.type === "hotel" && (
                        <Badge variant="secondary" className="gap-1">
                          <Hotel className="h-3 w-3" /> Otel
                        </Badge>
                      )}
                      {o.type === "office" && <Badge variant="outline">Ofis</Badge>}
                    </TableCell>
                    <TableCell>
                      <Badge variant={o.isActive ? "default" : "destructive"}>
                        {o.isActive ? "Aktif" : "Pasif"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button size="sm" variant="ghost">
                        <Pencil className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
                {offices.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={8}
                      className="text-center text-muted-foreground h-24"
                    >
                      Ofis bulunamadı
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
