"use client";

import { useMemo, useState } from "react";
import {
  ArchiveRestore,
  Pencil,
  Plus,
  Power,
  RefreshCw,
  SlidersHorizontal,
  Trash2
} from "lucide-react";
import { toast } from "sonner";

import ReservationExtraOptionDialog from "@/components/admin/dialogs/ReservationExtraOptionDialog";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle
} from "@/components/ui/empty";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow
} from "@/components/ui/table";
import {
  mutateDeleteReservationExtraOption,
  mutateRestoreReservationExtraOption,
  mutateUpdateReservationExtraOptionStatus,
  useAdminReservationExtras,
  useAdminVehicleGroups
} from "@/hooks/admin";
import type { AdminVehicleGroup } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/client";
import type {
  AdminReservationExtraOption,
  ReservationExtraStatusFilter
} from "@/lib/api/admin/reservationExtras";

type ConfirmAction = "delete" | "restore";

interface ConfirmationTarget {
  action: ConfirmAction;
  option: AdminReservationExtraOption;
}

function optionName(option: AdminReservationExtraOption) {
  return (
    option.translations.find((translation) => translation.locale === "tr")?.name ||
    option.translations.find((translation) => translation.locale === "en")?.name ||
    option.code
  );
}

function groupName(group: AdminVehicleGroup) {
  return group.nameTr ?? group.nameEn ?? group.name ?? group.id;
}

function statusBadge(option: AdminReservationExtraOption) {
  if (option.isArchived) {
    return <Badge variant="outline">Arşivli</Badge>;
  }
  return (
    <Badge variant={option.isActive ? "default" : "secondary"}>
      {option.isActive ? "Aktif" : "Taslak"}
    </Badge>
  );
}

export default function ReservationExtrasPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<ReservationExtraStatusFilter>("all");
  const [vehicleGroupId, setVehicleGroupId] = useState("all");
  const [page, setPage] = useState(1);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editingOption, setEditingOption] = useState<AdminReservationExtraOption>();
  const [confirmation, setConfirmation] = useState<ConfirmationTarget | null>(null);
  const [isMutating, setIsMutating] = useState(false);
  const [conflictOpen, setConflictOpen] = useState(false);

  const filters = useMemo(
    () => ({
      search: search.trim() || undefined,
      status,
      vehicleGroupId: vehicleGroupId === "all" ? undefined : vehicleGroupId,
      includeArchived: status === "archived",
      page,
      pageSize: 20
    }),
    [page, search, status, vehicleGroupId]
  );

  const { options, pagination, isLoading, isError, mutate } = useAdminReservationExtras(filters);
  const {
    groups: vehicleGroups,
    isLoading: groupsLoading,
    isError: groupsError
  } = useAdminVehicleGroups();

  const groupNames = useMemo(
    () => new Map(vehicleGroups.map((group) => [group.id, groupName(group)])),
    [vehicleGroups]
  );

  const resetPage = () => setPage(1);

  const openCreate = () => {
    setEditingOption(undefined);
    setEditorOpen(true);
  };

  const openEdit = (option: AdminReservationExtraOption) => {
    setEditingOption(option);
    setEditorOpen(true);
  };

  const refreshAfterMutation = async () => {
    await mutate();
  };

  const handleSaved = async () => {
    await refreshAfterMutation();
    setEditorOpen(false);
    setEditingOption(undefined);
  };

  const handleEditorReload = async () => {
    await refreshAfterMutation();
    setEditorOpen(false);
    setEditingOption(undefined);
  };

  const handleConflict = () => {
    setConflictOpen(true);
  };

  const toggleStatus = async (option: AdminReservationExtraOption) => {
    try {
      setIsMutating(true);
      await mutateUpdateReservationExtraOptionStatus(option.id, {
        version: option.version,
        isActive: !option.isActive
      });
      toast.success(
        option.isActive
          ? "Rezervasyon ekstrası taslağa alındı"
          : "Rezervasyon ekstrası aktifleştirildi"
      );
      await refreshAfterMutation();
    } catch (error) {
      if (error instanceof ApiError && error.statusCode === 409) {
        handleConflict();
      } else {
        toast.error(
          error instanceof Error && error.message ? error.message : "Durum güncellenemedi"
        );
      }
    } finally {
      setIsMutating(false);
    }
  };

  const executeConfirmedAction = async () => {
    if (!confirmation) return;
    try {
      setIsMutating(true);
      if (confirmation.action === "restore") {
        await mutateRestoreReservationExtraOption(
          confirmation.option.id,
          confirmation.option.version
        );
        toast.success("Rezervasyon ekstrası pasif taslak olarak geri yüklendi");
      } else {
        const result = await mutateDeleteReservationExtraOption(
          confirmation.option.id,
          confirmation.option.version
        );
        toast.success(
          result.disposition.toLowerCase() === "archived"
            ? "Kullanılmış kayıt arşivlendi"
            : "Kullanılmamış kayıt kalıcı olarak silindi"
        );
      }
      setConfirmation(null);
      await refreshAfterMutation();
    } catch (error) {
      if (error instanceof ApiError && error.statusCode === 409) {
        setConfirmation(null);
        handleConflict();
      } else {
        toast.error(
          error instanceof Error && error.message ? error.message : "İşlem tamamlanamadı"
        );
      }
    } finally {
      setIsMutating(false);
    }
  };

  const confirmationCopy =
    confirmation?.action === "restore"
      ? {
          title: "Kayıt geri yüklensin mi?",
          description: `${confirmation ? optionName(confirmation.option) : "Kayıt"} pasif taslak durumuna dönecek. Yeniden yayınlamak için eksiksiz içerikle ayrıca aktifleştirmeniz gerekir.`,
          action: "Geri Yükle"
        }
      : {
          title: "Kayıt silinsin veya arşivlensin mi?",
          description: `${confirmation ? optionName(confirmation.option) : "Kayıt"} hiç kullanılmadıysa kalıcı silinir; rezervasyonda kullanıldıysa geçmişi korumak için arşivlenir.`,
          action: "Devam Et"
        };

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="gap-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <CardTitle className="text-base">Rezervasyon Ekstraları</CardTitle>
              <p className="text-muted-foreground mt-1 text-sm">
                Ek hizmet kataloğunu, beş dilde içeriğini ve araç grubu uygunluğunu yönetin.
              </p>
            </div>
            <Button size="sm" onClick={openCreate} disabled={groupsLoading || Boolean(groupsError)}>
              <Plus className="mr-1 h-4 w-4" /> Yeni Ekstra
            </Button>
          </div>

          <div className="grid gap-3 md:grid-cols-[minmax(220px,1fr)_180px_minmax(220px,1fr)]">
            <Input
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                resetPage();
              }}
              placeholder="Ad veya kod ara"
              aria-label="Rezervasyon ekstralarında ara"
            />
            <Select
              value={status}
              onValueChange={(value: ReservationExtraStatusFilter) => {
                setStatus(value);
                resetPage();
              }}
            >
              <SelectTrigger aria-label="Duruma göre filtrele">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm güncel kayıtlar</SelectItem>
                <SelectItem value="active">Aktif</SelectItem>
                <SelectItem value="inactive">Taslak</SelectItem>
                <SelectItem value="archived">Arşivli</SelectItem>
              </SelectContent>
            </Select>
            <Select
              value={vehicleGroupId}
              onValueChange={(value) => {
                setVehicleGroupId(value);
                resetPage();
              }}
              disabled={groupsLoading || Boolean(groupsError)}
            >
              <SelectTrigger aria-label="Araç grubuna göre filtrele">
                <SelectValue placeholder="Tüm araç grupları" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm araç grupları</SelectItem>
                {vehicleGroups.map((group) => (
                  <SelectItem key={group.id} value={group.id}>
                    {groupName(group)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardHeader>

        <CardContent>
          {isLoading ? (
            <div className="space-y-2" aria-label="Rezervasyon ekstraları yükleniyor">
              {Array.from({ length: 5 }).map((_, index) => (
                <Skeleton key={index} className="h-12 w-full" />
              ))}
            </div>
          ) : isError ? (
            <Alert variant="destructive">
              <RefreshCw className="h-4 w-4" />
              <AlertTitle>Rezervasyon ekstraları yüklenemedi</AlertTitle>
              <AlertDescription className="flex items-center justify-between gap-4">
                <span>{isError.message || "Sunucuya ulaşılamadı."}</span>
                <Button type="button" size="sm" variant="outline" onClick={() => void mutate()}>
                  Tekrar Dene
                </Button>
              </AlertDescription>
            </Alert>
          ) : options.length === 0 ? (
            <Empty className="border">
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <SlidersHorizontal />
                </EmptyMedia>
                <EmptyTitle>Kayıt bulunamadı</EmptyTitle>
                <EmptyDescription>
                  Filtreleri temizleyin veya ilk rezervasyon ekstrasını taslak olarak oluşturun.
                </EmptyDescription>
              </EmptyHeader>
              <EmptyContent>
                <Button size="sm" onClick={openCreate}>
                  <Plus className="mr-1 h-4 w-4" /> Yeni Ekstra
                </Button>
              </EmptyContent>
            </Empty>
          ) : (
            <div className="space-y-4">
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Ad / Kod</TableHead>
                      <TableHead>Fiyat Kuralı</TableHead>
                      <TableHead>Maks. Adet</TableHead>
                      <TableHead>Araç Grupları</TableHead>
                      <TableHead>Durum</TableHead>
                      <TableHead className="text-right">İşlemler</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {options.map((option) => {
                      const label = optionName(option);
                      return (
                        <TableRow key={option.id}>
                          <TableCell>
                            <div className="font-medium">{label}</div>
                            <div className="text-muted-foreground text-xs">{option.code}</div>
                          </TableCell>
                          <TableCell>
                            <div className="font-medium">
                              ₺{option.unitPrice.toLocaleString("tr-TR")}
                            </div>
                            <div className="text-muted-foreground text-xs">
                              {option.pricingMode === "PER_DAY" ? "Günlük" : "Kiralama başına"}
                            </div>
                          </TableCell>
                          <TableCell>{option.maxQuantity}</TableCell>
                          <TableCell>
                            <div className="flex max-w-72 flex-wrap gap-1">
                              {option.vehicleGroupIds.slice(0, 3).map((id) => (
                                <Badge key={id} variant="outline">
                                  {groupNames.get(id) ?? id}
                                </Badge>
                              ))}
                              {option.vehicleGroupIds.length > 3 && (
                                <Badge variant="secondary">
                                  +{option.vehicleGroupIds.length - 3}
                                </Badge>
                              )}
                              {option.vehicleGroupIds.length === 0 && (
                                <span className="text-muted-foreground text-xs">Atama yok</span>
                              )}
                            </div>
                          </TableCell>
                          <TableCell>{statusBadge(option)}</TableCell>
                          <TableCell className="text-right">
                            <div className="flex items-center justify-end gap-1">
                              {!option.isArchived && (
                                <>
                                  <Button
                                    type="button"
                                    size="icon-sm"
                                    variant="ghost"
                                    aria-label={`${label} düzenle`}
                                    title="Düzenle"
                                    onClick={() => openEdit(option)}
                                  >
                                    <Pencil className="h-4 w-4" />
                                  </Button>
                                  <Button
                                    type="button"
                                    size="icon-sm"
                                    variant="ghost"
                                    aria-label={
                                      option.isActive ? `${label} pasif et` : `${label} aktif et`
                                    }
                                    title={option.isActive ? "Pasif et" : "Aktif et"}
                                    disabled={isMutating}
                                    onClick={() => void toggleStatus(option)}
                                  >
                                    <Power className="h-4 w-4" />
                                  </Button>
                                  <Button
                                    type="button"
                                    size="icon-sm"
                                    variant="ghost"
                                    aria-label={`${label} sil veya arşivle`}
                                    title="Sil veya arşivle"
                                    onClick={() => setConfirmation({ action: "delete", option })}
                                  >
                                    <Trash2 className="text-destructive h-4 w-4" />
                                  </Button>
                                </>
                              )}
                              {option.isArchived && (
                                <Button
                                  type="button"
                                  size="icon-sm"
                                  variant="ghost"
                                  aria-label={`${label} taslağa geri yükle`}
                                  title="Taslağa geri yükle"
                                  onClick={() => setConfirmation({ action: "restore", option })}
                                >
                                  <ArchiveRestore className="h-4 w-4" />
                                </Button>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>

              {pagination && pagination.totalPages > 1 && (
                <div className="flex items-center justify-between gap-3 text-sm">
                  <span className="text-muted-foreground">
                    {pagination.totalCount} kayıt · Sayfa {pagination.page}/{pagination.totalPages}
                  </span>
                  <div className="flex gap-2">
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={page <= 1}
                      onClick={() => setPage((current) => Math.max(1, current - 1))}
                    >
                      Önceki
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={page >= pagination.totalPages}
                      onClick={() => setPage((current) => current + 1)}
                    >
                      Sonraki
                    </Button>
                  </div>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <ReservationExtraOptionDialog
        open={editorOpen}
        onOpenChange={(open) => {
          setEditorOpen(open);
          if (!open) setEditingOption(undefined);
        }}
        option={editingOption}
        vehicleGroups={vehicleGroups}
        onSaved={() => void handleSaved()}
        onReload={() => void handleEditorReload()}
      />

      <AlertDialog
        open={Boolean(confirmation)}
        onOpenChange={(open) => !open && setConfirmation(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{confirmationCopy.title}</AlertDialogTitle>
            <AlertDialogDescription>{confirmationCopy.description}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isMutating}>İptal</AlertDialogCancel>
            <AlertDialogAction disabled={isMutating} onClick={() => void executeConfirmedAction()}>
              {isMutating ? "İşleniyor..." : confirmationCopy.action}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={conflictOpen} onOpenChange={setConflictOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Kayıt başka bir işlemde değiştirildi</AlertDialogTitle>
            <AlertDialogDescription>
              Eski sürüm üzerine yazılmadı. Güncel listeyi yükleyip işlemi tekrar değerlendirin.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConflictOpen(false);
                void refreshAfterMutation();
              }}
            >
              Listeyi Yenile
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
