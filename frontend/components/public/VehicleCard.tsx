import { useTranslations } from "next-intl";
import Link from "next/link";
import VehicleImage from "@/components/public/VehicleImage";
import type { PublicVehicle } from "@/lib/api/types";
import { vehicleDetailHref, vehicleGroupName, vehiclePhotos } from "@/lib/vehicle-catalogue";
import VehicleFacts from "./VehicleFacts";

export default function VehicleCard({
  vehicle,
  locale,
  search = new URLSearchParams()
}: {
  vehicle: PublicVehicle;
  locale: string;
  search?: Pick<URLSearchParams, "get">;
}) {
  const t = useTranslations("catalogue");
  const name = `${vehicle.brand} ${vehicle.model}`;
  const image = vehiclePhotos(vehicle)[0];
  return (
    <article className="group flex h-full min-w-0 flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white hover:shadow-lg">
      <div className="relative aspect-[16/10] overflow-hidden bg-slate-100">
        <VehicleImage src={image} alt={name} />
        <span className="absolute start-3 top-3 rounded-lg bg-white/95 px-3 py-1 text-xs font-semibold text-sky-800">
          {vehicleGroupName(vehicle, locale)}
        </span>
      </div>
      <div className="flex flex-1 flex-col gap-4 p-4">
        <div>
          <h3 className="text-xl font-bold text-slate-900">{name}</h3>
          <p className="text-sm text-slate-500">
            {vehicle.year} · {vehicle.color}
          </p>
        </div>
        <VehicleFacts vehicle={vehicle} />
        <div className="mt-auto space-y-3 border-t border-slate-200 pt-4">
          <p className="text-sm text-slate-600">{t("chooseDates")}</p>
          <Link
            href={vehicleDetailHref(vehicle.id, locale, search)}
            className="block rounded-xl bg-sky-700 px-4 py-3 text-center font-semibold text-white hover:bg-sky-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
          >
            {t("viewDetails")}
          </Link>
        </div>
      </div>
    </article>
  );
}
