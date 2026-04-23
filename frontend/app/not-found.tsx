import { ArrowRight } from "lucide-react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";

import { Button } from "@/components/ui/button";

export default function NotFound() {
  const t = useTranslations("common");

  return (
    <div className="bg-background grid h-screen items-center pb-8 lg:grid-cols-2 lg:pb-0">
      <div className="text-center px-4">
        <p className="text-muted-foreground text-base font-semibold">404</p>
        <h1 className="mt-4 text-3xl font-bold tracking-tight md:text-5xl lg:text-7xl">
          {t("errors.notFound")}
        </h1>
        <p className="text-muted-foreground mt-6 text-base leading-7">
          {t("errors.generic")}
        </p>
        <div className="mt-10 flex items-center justify-center gap-x-2">
          <Button size="lg" asChild>
            <Link href="/">{t("buttons.back")}</Link>
          </Button>
          <Button size="lg" variant="ghost" asChild>
            <Link href="/contact">
              {t("buttons.next")} <ArrowRight className="ms-2 h-4 w-4" />
            </Link>
          </Button>
        </div>
      </div>
      <div className="hidden lg:block">
        <img
          src="/404.svg"
          width={300}
          height={400}
          className="w-full object-contain lg:max-w-2xl"
          alt="not found image"
        />
      </div>
    </div>
  );
}
