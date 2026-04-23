import React from "react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/tr.json";

export default function GuestLayout({
  children
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <NextIntlClientProvider locale="tr" messages={messages}>
      {children}
    </NextIntlClientProvider>
  );
}
