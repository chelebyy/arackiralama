import type { NextConfig } from "next";
import { config } from "dotenv";
import createNextIntlPlugin from "next-intl/plugin";

config();

const isProduction = process.env.NODE_ENV === "production";

const withNextIntl = createNextIntlPlugin("./i18n/config.ts");

const nextConfig: NextConfig = {
  output: "standalone",
  images: {
    remotePatterns: [
      {
        protocol: "http",
        hostname: "localhost"
      },
      {
        protocol: "https",
        hostname: "bundui-images.netlify.app"
      }
    ]
  }
};

export default withNextIntl(nextConfig);
