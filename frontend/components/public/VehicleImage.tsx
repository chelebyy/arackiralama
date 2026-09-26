"use client";

import { useState } from "react";
import { Car } from "lucide-react";

export default function VehicleImage({ src, alt }: { src?: string; alt: string }) {
  const [failedSrc, setFailedSrc] = useState<string>();
  return src && src !== failedSrc ? (
    <img src={src} alt={alt} className="h-full w-full object-cover" onError={() => setFailedSrc(src)} />
  ) : (
    <div className="flex h-full w-full items-center justify-center" role="img" aria-label={alt}>
      <Car className="h-20 w-20 text-slate-300" aria-hidden="true" />
    </div>
  );
}
