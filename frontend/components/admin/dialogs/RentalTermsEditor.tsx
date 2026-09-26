"use client";

import type { RentalRate, RentalTerms } from "@/lib/rental-policy";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import useSWR from "swr";
import { getReservationExtraOptions } from "@/lib/api/admin/reservationExtras";

export function RentalTermsEditor({ value, onChange }: { value?: RentalTerms | null; onChange: (value: RentalTerms) => void }) {
  const { data: extras, error } = useSWR(value ? "vehicle-rental-extra-options" : null, () => getReservationExtraOptions({ pageSize: 100, includeArchived: false }));
  if (!value) return <section className="border rounded p-4 space-y-2"><p>Araç bazlı fiyat ve koşullar henüz yapılandırılmadı.</p><Button type="button" onClick={() => onChange({ depositAmount: null, minAge: null, minLicenseYears: null, rates: [], extraOptionIds: [] })}>Araç koşullarını yapılandır</Button></section>;
  const update = (index: number, patch: Partial<RentalRate>) => onChange({ ...value, rates: value.rates.map((rate, i) => i === index ? { ...rate, ...patch } : rate) });
  const selectedExtraIds = new Set(value.extraOptionIds);
  const listedExtraIds = new Set(extras?.items.map(option => option.id));
  return <section className="border rounded p-4 space-y-4"><h3 className="font-semibold">Araç fiyatı ve kiralama koşulları</h3>
    <p className="text-sm">Değişiklikler yeni tekliflere uygulanır. Mevcut rezervasyonların koşulları korunur.</p>
    <div className="grid grid-cols-3 gap-3">{([['depositAmount', 'Depozito (TRY)', 0, 10000000], ['minAge', 'Asgari yaş', 18, 100], ['minLicenseYears', 'Ehliyet yılı', 0, 80]] as const).map(([key, label, min, max]) => <label key={key}>{label}<Input type="number" min={min} max={max} required value={value[key] ?? ""} onChange={e => onChange({ ...value, [key]: e.target.value === "" ? null : Number(e.target.value) })} /></label>)}</div>
    {value.rates.map((rate, i) => <fieldset key={rate.id} className="border rounded p-3 space-y-2"><legend>Fiyat dönemi {i + 1}</legend>
      <div className="grid grid-cols-2 gap-2"><label>Başlangıç<Input required type="date" value={rate.startDate} onChange={e => update(i, { startDate: e.target.value })} /></label><label>Bitiş<Input required type="date" value={rate.endDate} onChange={e => update(i, { endDate: e.target.value })} /></label></div>
      <div className="grid grid-cols-2 gap-2">{([['dailyPrice', 'Günlük fiyat (TRY)', 0.01], ['priority', 'Öncelik', 0], ['multiplier', 'Temel çarpan', 0.01], ['weekdayMultiplier', 'Hafta içi çarpanı', 0.01], ['weekendMultiplier', 'Hafta sonu çarpanı', 0.01]] as const).map(([key, label, min]) => <label key={key}>{label}<Input required type="number" step="0.01" min={min} value={rate[key]} onChange={e => update(i, { [key]: Number(e.target.value) })} /></label>)}</div>
      <label>Hesap türü<select className="block border rounded p-2" value={rate.calculationType} onChange={e => update(i, { calculationType: e.target.value as RentalRate['calculationType'] })}><option value="fixed">Sabit</option><option value="multiplier">Çarpanlı</option></select></label>
      <Button type="button" variant="outline" onClick={() => onChange({ ...value, rates: value.rates.filter((_, n) => n !== i) })}>Dönemi sil</Button>
    </fieldset>)}
    <Button type="button" variant="outline" onClick={() => onChange({ ...value, rates: [...value.rates, { id: crypto.randomUUID(), startDate: "", endDate: "", dailyPrice: 0, multiplier: 1, weekdayMultiplier: 1, weekendMultiplier: 1, calculationType: "multiplier", priority: 0, createdAt: new Date().toISOString() }] })}>Fiyat dönemi ekle</Button>
    <fieldset className="space-y-2"><legend>Uygun ek hizmetler</legend>{error && <p role="alert">Ek hizmetler yüklenemedi. Mevcut seçimler korunuyor.</p>}{extras?.items.map(option => <label key={option.id} className="flex gap-2"><input type="checkbox" checked={selectedExtraIds.has(option.id)} onChange={e => onChange({ ...value, extraOptionIds: e.target.checked ? [...value.extraOptionIds, option.id] : value.extraOptionIds.filter(id => id !== option.id) })} />{option.translations.find(t => t.locale === "tr")?.name ?? option.code}</label>)}</fieldset>
    {value.extraOptionIds.filter(id => !listedExtraIds.has(id)).map(id => <div key={id} className="flex items-center gap-2 text-sm"><span>Listede görünmeyen mevcut seçim: {id}</span><Button type="button" variant="outline" onClick={() => onChange({ ...value, extraOptionIds: value.extraOptionIds.filter(selected => selected !== id) })}>Seçimi kaldır</Button></div>)}
  </section>;
}
