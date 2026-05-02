import { defineConfig, devices } from "@playwright/test";
import type { Config } from "@playwright/test";

const baseConfig: Config = {
  testDir: "./e2e",
  timeout: 60000,
  expect: {
    timeout: 10000,
  },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: [
    ["list"],
    ["html", { outputFolder: "playwright-report", open: "never" }],
    [
      "junit",
      {
        outputFile: "playwright-results/results.xml",
        embedArtifactsOnFailure: true,
      },
    ],
  ],
  use: {
    baseURL: process.env.E2E_BASE_URL || "http://localhost:3000",
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
    actionTimeout: 15000,
    navigationTimeout: 60000,
  },
};

const config: Config = {
  ...baseConfig,
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "chromium-mobile",
      use: { ...devices["Pixel 5"] },
    },
  ],
  ...(process.env.CI
    ? {
        /* CI-only: shard into 2 workers */
        workers: 2,
        shard: { current: Number(process.env.SHARD || 1), total: 2 },
      }
    : {}),
};

export default config;
