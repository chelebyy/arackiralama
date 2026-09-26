"use client";
import { Suspense, useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import Link from "next/link";
import VehicleImage from "@/components/public/VehicleImage";
import { useVehicle, useOffices, useExactAvailableVehicles } from "@/hooks/useVehicles";
import VehicleFacts from "@/components/public/VehicleFacts";
import {
  catalogueSearch,
  catalogueOffice,
  equipmentCodes,
  validCatalogueDates,
  vehicleGroupName,
  vehiclePhotos
} from "@/lib/vehicle-catalogue";

function VehicleDetail() {
  const params = useParams(),
    search = useSearchParams();
  const locale = typeof params.locale === "string" ? params.locale : "tr";
  const t = useTranslations("catalogue"),
    tv = useTranslations("vehicles"),
    ts = useTranslations("searchForm");
  const [imageIndex, setImageIndex] = useState(0);
  const { vehicle, isLoading, isError } = useVehicle(
    typeof params.id === "string" ? params.id : null
  );
  const { offices } = useOffices();
  const dates = validCatalogueDates(search);
  const officeId = catalogueOffice(offices, search.get("pickup") ?? "");
  const returnOfficeId = catalogueOffice(
    offices,
    search.get("return") ?? search.get("pickup") ?? ""
  );
  const availability = useExactAvailableVehicles(
    dates && officeId && returnOfficeId && vehicle
      ? {
          pickupOfficeId: officeId,
          returnOfficeId,
          pickupDateTimeUtc: dates.pickup,
          returnDateTimeUtc: dates.returned
        }
      : null
  );
  const groupQuote = availability.vehicles.find(
    (offer) => offer.vehicle.id === vehicle?.id && Number.isFinite(offer.finalTotal) && offer.finalTotal > 0
  );
  const context = catalogueSearch(search);
  const booking = new URLSearchParams(context);
  if (vehicle) {
    booking.set("vehicle", vehicle.groupId ?? "00000000-0000-0000-0000-000000000000");
    booking.set("preferredVehicleId", vehicle.id);
    booking.set("vehicleName", `${vehicle.brand} ${vehicle.model}`);
    if (groupQuote) booking.set("dailyPrice", String(groupQuote.vehicle.dailyPrice));
  }
  const images = vehicle ? vehiclePhotos(vehicle) : [];
  const activeImage = Math.min(imageIndex, Math.max(0, images.length - 1));
  const name = vehicle ? `${vehicle.brand} ${vehicle.model}` : "";
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-6">
        <Link href={`/${locale}/vehicles?${context}`} className="text-sm text-sky-800 underline">
          {tv("detail.backToSearch")}
        </Link>
        {isLoading ? (
          <p role="status">{tv("detail.loading")}</p>
        ) : isError ? (
          <p role="alert">{tv("detail.failed")}</p>
        ) : !vehicle ? (
          <p>{tv("detail.notFoundTitle")}</p>
        ) : (
          <div className="grid gap-8 lg:grid-cols-3">
            <div className="min-w-0 space-y-6 lg:col-span-2">
              <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
                <div className="flex aspect-[16/10] items-center justify-center bg-slate-100">
                  <VehicleImage src={images[activeImage]} alt={`${name} — ${activeImage + 1}`} />
                </div>
                {images.length > 1 && (
                  <div className="flex flex-wrap items-center justify-center gap-2 p-3">
                    <button
                      type="button"
                      onClick={() =>
                        setImageIndex((activeImage + images.length - 1) % images.length)
                      }
                      className="rounded border px-3 py-2"
                    >
                      {t("previousPhoto")}
                    </button>
                    {images.map((image, index) => (
                      <button
                        type="button"
                        key={image}
                        aria-label={t("photo", { number: index + 1 })}
                        aria-pressed={activeImage === index}
                        onClick={() => setImageIndex(index)}
                        className="rounded border px-3 py-2 aria-pressed:bg-sky-100 aria-pressed:text-sky-900"
                      >
                        {index + 1}
                      </button>
                    ))}
                    <button
                      type="button"
                      onClick={() => setImageIndex((activeImage + 1) % images.length)}
                      className="rounded border px-3 py-2"
                    >
                      {t("nextPhoto")}
                    </button>
                  </div>
                )}
              </div>
              <section className="space-y-5 rounded-xl border border-slate-200 bg-white p-6">
                <p className="text-sm text-sky-800">{vehicleGroupName(vehicle, locale)}</p>
                <h1 className="text-3xl font-bold text-slate-900">{name}</h1>
                <p className="text-slate-600">
                  {vehicle.year} · {vehicle.color}
                </p>
                <VehicleFacts vehicle={vehicle} detail />
                <dl className="grid grid-cols-2 gap-3 text-sm">
                  <div>
                    <dt className="text-slate-500">{tv("detail.minAge")}</dt>
                    <dd className="font-medium text-slate-800">{vehicle.minAge}</dd>
                  </div>
                  <div>
                    <dt className="text-slate-500">{tv("detail.minLicenseYears")}</dt>
                    <dd className="font-medium text-slate-800">{vehicle.minLicenseYears}</dd>
                  </div>
                </dl>
              </section>
              <section className="space-y-4 rounded-xl border border-slate-200 bg-white p-6">
                <h2 className="text-xl font-semibold">{t("equipment")}</h2>
                <p className="text-sm text-slate-600">{t("equipmentNote")}</p>
                {vehicle.features.length ? (
                  <ul className="grid gap-3 sm:grid-cols-2">
                    {vehicle.features
                      .filter((feature) => equipmentCodes.some((code) => code === feature))
                      .map((feature) => (
                        <li key={feature} className="rounded-lg bg-slate-50 p-3">
                          {t(`values.${feature}`)}
                        </li>
                      ))}
                  </ul>
                ) : (
                  <p className="text-slate-600">{t("unknown")}</p>
                )}
              </section>
            </div>
            <aside className="min-w-0 space-y-5 self-start rounded-xl border border-slate-200 bg-white p-6">
              <h2 className="text-xl font-semibold">{t("chooseDates")}</h2>
              <form action={`/${locale}/vehicles/${vehicle.id}`} method="get" className="space-y-4">
                {(["pickup", "return"] as const).map((prefix) => (
                  <fieldset key={prefix} className="space-y-3">
                    <legend className="font-medium">
                      {ts(prefix === "pickup" ? "pickupLocation" : "returnLocation")}
                    </legend>
                    <label className="grid gap-1 text-sm">
                      {t("office")}
                      <select
                        key={`${prefix}-${(prefix === "pickup" ? officeId : returnOfficeId) ?? ""}`}
                        name={prefix}
                        defaultValue={(prefix === "pickup" ? officeId : returnOfficeId) ?? ""}
                        required
                        className="w-full min-w-0 rounded border border-slate-300 bg-white p-2"
                      >
                        <option value="">{t("select")}</option>
                        {offices.map((office) => (
                          <option key={office.id} value={office.id}>
                            {office.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-1">
                      <label className="grid gap-1 text-sm">
                        {t("date")}
                        <input
                          name={`${prefix}Date`}
                          type="date"
                          required
                          defaultValue={search.get(`${prefix}Date`) ?? ""}
                          className="w-full min-w-0 rounded border border-slate-300 p-2"
                        />
                      </label>
                      <label className="grid gap-1 text-sm">
                        {t("time")}
                        <input
                          name={`${prefix}Time`}
                          type="time"
                          required
                          defaultValue={search.get(`${prefix}Time`) ?? ""}
                          className="w-full min-w-0 rounded border border-slate-300 p-2"
                        />
                      </label>
                    </div>
                  </fieldset>
                ))}
                <button
                  className="w-full rounded-lg bg-sky-700 px-4 py-3 font-semibold text-white hover:bg-sky-800"
                  type="submit"
                >
                  {t("checkDates")}
                </button>
              </form>
              {search.get("pickupDate") && !dates && (
                <p role="alert" className="text-sm text-red-700">
                  {t("invalidDates")}
                </p>
              )}
              {dates && officeId && returnOfficeId && (
                <div className="space-y-3 border-t pt-4">
                  {availability.isLoading ? (
                    <p role="status">{tv("loading")}</p>
                  ) : availability.isError ? (
                    <p role="alert">{tv("failed")}</p>
                  ) : groupQuote ? (
                    <>
                      <p className="text-lg font-semibold text-sky-800">
                        {tv("totalPrice")}: {new Intl.NumberFormat(locale, { style: "currency", currency: groupQuote.currency }).format(groupQuote.finalTotal)}
                      </p>
                      <p className="text-sm text-slate-600">{groupQuote.rentalDays} {tv("days")}</p>
                      <Link
                        href={`/${locale}/booking/step2?${booking}`}
                        className="block rounded-lg bg-sky-700 px-4 py-3 text-center font-semibold text-white"
                      >
                        {t("continue")}
                      </Link>
                    </>
                  ) : (
                    <p>{tv("unavailable")}</p>
                  )}
                </div>
              )}
            </aside>
          </div>
        )}
      </div>
    </div>
  );
}
export default function VehicleDetailPage() {
  return (
    <Suspense>
      <VehicleDetail />
    </Suspense>
  );
}
