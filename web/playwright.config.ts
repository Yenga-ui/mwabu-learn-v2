import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  globalTeardown: "./e2e/teardown.mjs",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  forbidOnly: !!process.env.CI,
  reporter: [
    ["list"],
    ["junit", { outputFile: "../artifacts/playwright.xml" }],
  ],
  timeout: 60000,
  use: {
    baseURL: "https://127.0.0.1:7443",
    ignoreHTTPSErrors: true,
    trace: "off",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { browserName: "chromium" } }],
  webServer: {
    command: "node e2e/server.mjs",
    url: "http://127.0.0.1:7444/ready",
    ignoreHTTPSErrors: true,
    timeout: 180000,
    reuseExistingServer: false,
  },
});
