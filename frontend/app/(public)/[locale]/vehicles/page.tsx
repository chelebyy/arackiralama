"use client";
import { Suspense, useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import VehicleCard from "@/components/public/VehicleCard";
import { usePublicVehicles } from "@/hooks/useVehicles";
import { vehicleGroupName, validCatalogueDates } from "@/lib/vehicle-catalogue";

function Catalogue() {
  const params = useParams(),
    search = useSearchParams();
  const locale = typeof params.locale === "string" ? params.locale : "tr";
  const t = useTranslations("catalogue"),
    tv = useTranslations("vehicles");
  const { vehicles, isLoading, isError } = usePublicVehicles();
  const [group, setGroup] = useState(""),
    [page, setPage] = useState(1),
    [view, setView] = useState("grid");
  const catalogue = vehicles.filter((vehicle) => vehicle.status === "Available");
  const groups = Array.from(
    new Map(
      catalogue.map((vehicle) => [vehicle.groupId, vehicleGroupName(vehicle, locale)])
    ).entries()
  );
  const filtered = catalogue.filter((vehicle) => !group || vehicle.groupId === group);
  const pages = Math.max(1, Math.ceil(filtered.length / 6)),
    current = Math.min(page, pages);
  const dates = validCatalogueDates(search);
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-6">
        <h1 className="text-3xl font-bold text-slate-900">{tv("title")}</h1>
        <p className="text-slate-600">
          {dates ? `${search.get("pickupDate")} → ${search.get("returnDate")}` : t("chooseDates")}
        </p>
        <div className="flex flex-wrap items-end justify-between gap-4">
          <label className="grid gap-2 text-sm text-slate-700">
            {t("group")}
            <select
              value={group}
              onChange={(event) => {
                setGroup(event.target.value);
                setPage(1);
              }}
              className="rounded-lg border border-slate-300 bg-white p-3"
            >
              <option value="">{t("all")}</option>
              {groups.map(([id, name]) => (
                <option key={id} value={id}>
                  {name}
                </option>
              ))}
            </select>
          </label>
          <div className="flex gap-2">
            {["grid", "list"].map((mode) => (
              <button
                key={mode}
                type="button"
                aria-pressed={view === mode}
                onClick={() => setView(mode)}
                className={
                  view === mode
                    ? "rounded-lg border border-sky-300 bg-sky-50 px-4 py-2 text-sky-900"
                    : "rounded-lg border border-slate-300 bg-white px-4 py-2 text-slate-700"
                }
              >
                {t(mode)}
              </button>
            ))}
          </div>
        </div>
        {isLoading ? (
          <p role="status">{tv("loading")}</p>
        ) : isError ? (
          <p role="alert">{tv("failed")}</p>
        ) : !filtered.length ? (
          <p>{tv("empty")}</p>
        ) : (
          <div
            className={
              view === "grid"
                ? "grid grid-cols-1 gap-6 md:grid-cols-2 xl:grid-cols-3"
                : "grid max-w-2xl grid-cols-1 gap-6"
            }
          >
            {filtered.slice((current - 1) * 6, current * 6).map((vehicle) => (
              <VehicleCard key={vehicle.id} vehicle={vehicle} locale={locale} search={search} />
            ))}
          </div>
        )}
        {pages > 1 && (
          <nav aria-label={t("pages")} className="flex justify-center gap-3">
            <button
              type="button"
              disabled={current === 1}
              onClick={() => setPage(current - 1)}
              className="rounded border bg-white p-3 disabled:opacity-50"
            >
              {t("previous")}
            </button>
            <span className="p-3">
              {current} / {pages}
            </span>
            <button
              type="button"
              disabled={current === pages}
              onClick={() => setPage(current + 1)}
              className="rounded border bg-white p-3 disabled:opacity-50"
            >
              {t("next")}
            </button>
          </nav>
        )}
      </div>
    </div>
  );
}
export default function VehiclesPage() {
  return (
    <Suspense>
      <Catalogue />
    </Suspense>
  );
}
