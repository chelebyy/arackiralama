import { useTranslations } from "next-intl";
import type { VehicleCatalogue } from "@/lib/api/types";

export default function VehicleFacts({
  vehicle,
  detail = false
}: {
  vehicle: VehicleCatalogue;
  detail?: boolean;
}) {
  const t = useTranslations("catalogue");
  const facts: [string, string | number | null | undefined][] = [
    ["transmission", vehicle.transmission ? t(`values.${vehicle.transmission}`) : null],
    ["fuelType", vehicle.fuelType ? t(`values.${vehicle.fuelType}`) : null],
    ["seatCount", vehicle.seatCount],
    ["luggageCapacity", vehicle.luggageCapacity]
  ];
  if (detail)
    facts.push(
      ["bodyType", vehicle.bodyType ? t(`values.${vehicle.bodyType}`) : null],
      ["doorCount", vehicle.doorCount],
      ["engine", vehicle.engine],
      ["powerHp", vehicle.powerHp]
    );
  return (
    <dl className="grid grid-cols-2 gap-3 text-sm">
      {facts.map(([label, value]) => (
        <div key={label} className="min-w-0 rounded-lg bg-slate-50 p-2">
          <dt className="text-xs text-slate-500">{t(label)}</dt>
          <dd className="font-medium break-words text-slate-800">{value ?? t("unknown")}</dd>
        </div>
      ))}
    </dl>
  );
}
