"use client";

import { useMemo } from "react";

export default function CurrencyAmount({ amount, currency, locale }: {
  amount: number;
  currency: string;
  locale: string;
}) {
  const formatter = useMemo(
    () => new Intl.NumberFormat(locale, { style: "currency", currency }),
    [locale, currency]
  );
  return <>{formatter.format(amount)}</>;
}
