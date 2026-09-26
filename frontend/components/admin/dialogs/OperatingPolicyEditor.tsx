"use client";

import type { OperatingPolicy, OperatingWindow } from "@/lib/rental-policy";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useState } from "react";

const labels = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
const time = (minute: number) => minute < 0 ? "" : `${Math.floor(minute / 60).toString().padStart(2, "0")}:${(minute % 60).toString().padStart(2, "0")}`;
const minutes = (value: string) => { const [h, m] = value.split(":").map(Number); return h * 60 + m; };

export function OperatingPolicyEditor({ value, onChange }: { value?: OperatingPolicy | null; onChange: (value: OperatingPolicy) => void }) {
  const [closedDate, setClosedDate] = useState("");
  if (!value) return <section className="rounded border p-4 space-y-3">
    <h3 className="font-semibold">Otomatik rezervasyon ayarları</h3>
    <p className="text-sm">Zorunlu ayarlar henüz yapılmadı. Bu ofisten otomatik rezervasyon alınamaz.</p>
    <Button type="button" onClick={() => onChange({ minimumNoticeMinutes: null, preparationMinutes: null, pickupWindows: [], returnWindows: [], closedDates: [] })}>Ayarları yapılandır</Button>
  </section>;
  const windows = (key: "pickupWindows" | "returnWindows", title: string) => {
    const update = (index: number, patch: Partial<OperatingWindow>) => onChange({ ...value, [key]: value[key].map((w, i) => i === index ? { ...w, ...patch } : w) });
    return <fieldset className="space-y-2"><legend className="font-medium">{title} (Türkiye saati)</legend>
      {value[key].map((w, i) => <div key={i} className="flex flex-wrap gap-2">
        <select aria-label={`${title} günü ${i + 1}`} className="border rounded p-2" value={w.day} onChange={e => update(i, { day: Number(e.target.value) })}>{labels.map((label, day) => <option key={day} value={day}>{label}</option>)}</select>
        <Input className="w-28" type="time" required aria-label={`${title} başlangıç ${i + 1}`} value={time(w.startMinute)} onChange={e => update(i, { startMinute: minutes(e.target.value) })} />
        <Input className="w-28" type="time" required aria-label={`${title} bitiş ${i + 1}`} value={w.endMinute === 1440 ? "00:00" : time(w.endMinute)} onChange={e => update(i, { endMinute: minutes(e.target.value) || 1440 })} />
        <Button type="button" variant="outline" onClick={() => onChange({ ...value, [key]: value[key].filter((_, n) => n !== i) })}>Sil</Button>
      </div>)}
      <Button type="button" variant="outline" onClick={() => onChange({ ...value, [key]: [...value[key], { day: 1, startMinute: -1, endMinute: -1 }] })}>Saat aralığı ekle</Button>
    </fieldset>;
  };
  return <section className="rounded border p-4 space-y-4">
    <h3 className="font-semibold">Otomatik rezervasyon ayarları</h3>
    <p className="text-sm">Her iki işlem için en az bir saat aralığı ve süreler zorunludur. Bitiş saati aralığa dahil değildir; 00:00 gün sonudur.</p>
    <label className="block">En erken rezervasyon (dakika)<Input type="number" min={0} max={525600} required value={value.minimumNoticeMinutes ?? ""} onChange={e => onChange({ ...value, minimumNoticeMinutes: e.target.value === "" ? null : Number(e.target.value) })} /></label>
    <label className="block">İade sonrası hazırlık (dakika)<Input type="number" min={0} max={10080} required value={value.preparationMinutes ?? ""} onChange={e => onChange({ ...value, preparationMinutes: e.target.value === "" ? null : Number(e.target.value) })} /></label>
    {windows("pickupWindows", "Teslim")}{windows("returnWindows", "İade")}
    <label className="block">Kapalı tarih<Input type="date" value={closedDate} onChange={e => setClosedDate(e.target.value)} /></label>
    <Button type="button" variant="outline" disabled={!closedDate} onClick={() => { onChange({ ...value, closedDates: [...new Set([...value.closedDates, closedDate])] }); setClosedDate(""); }}>Kapalı tarih ekle</Button>
    <ul>{value.closedDates.map(date => <li key={date} className="flex items-center gap-3">{date}<Button type="button" variant="outline" aria-label={`${date} kapalı tarihini sil`} onClick={() => onChange({ ...value, closedDates: value.closedDates.filter(d => d !== date) })}>Sil</Button></li>)}</ul>
  </section>;
}
