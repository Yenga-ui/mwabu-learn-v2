import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import PlannerSettings from "./PlannerSettings";
vi.mock("../../auth/Session", () => ({
  useSession: () => ({ can: () => false }),
}));
vi.mock("../../api/client", () => ({
  api: vi.fn(async (path: string) =>
    path.endsWith("configuration")
      ? {
          aiConfigured: true,
          organisationAiApproved: true,
          searchConfigured: false,
          organisationSearchApproved: false,
        }
      : path.endsWith("settings")
        ? { externalAiApproved: true }
        : {
            requestsToday: 1,
            dailyRequestLimit: 50,
            chargedTokensThisMonth: 100,
            monthlyTokenLimit: 1000000,
            estimatedCostUsdThisMonth: 0,
            monthlyCostLimitUsd: 25,
            uncertainRequests: 0,
          },
  ),
}));
it("shows teacher availability and quotas without provider setup, model selection or credentials", async () => {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <PlannerSettings organisation="timothy-test" />
    </QueryClientProvider>,
  );
  expect(await screen.findByText(/AI assistance: enabled/)).toBeVisible();
  expect(
    screen.queryByText(/server key|OpenAI|Ollama|configured|model selector/i),
  ).not.toBeInTheDocument();
  expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
  expect(await screen.findByText(/USD 0.0000/)).toBeVisible();
});
