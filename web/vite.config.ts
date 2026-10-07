import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import { readFileSync } from "node:fs";

export default defineConfig(({ command, mode }) => {
  if (
    command === "serve" &&
    mode !== "test" &&
    (!process.env.MWABU_WEB_CERT || !process.env.MWABU_WEB_KEY)
  )
    throw new Error(
      "HTTPS development requires MWABU_WEB_CERT and MWABU_WEB_KEY. See docs/live-web-platform.md.",
    );
  return {
    plugins: [react()],
    build: { sourcemap: false, chunkSizeWarningLimit: 400 },
    server: {
      host: "127.0.0.1",
      strictPort: true,
      port: 5173,
      ...(command === "serve" &&
      mode !== "test" &&
      process.env.MWABU_WEB_CERT &&
      process.env.MWABU_WEB_KEY
        ? {
            https: {
              cert: readFileSync(process.env.MWABU_WEB_CERT),
              key: readFileSync(process.env.MWABU_WEB_KEY),
            },
            proxy: {
              "/api": {
                target:
                  process.env.MWABU_API_ORIGIN ?? "https://localhost:7215",
                secure: true,
                changeOrigin: true,
              },
            },
          }
        : {}),
    },
    test: {
      environment: "jsdom",
      setupFiles: ["./src/test/setup.ts"],
      include: ["src/**/*.test.{ts,tsx}"],
      clearMocks: true,
    },
  };
});
