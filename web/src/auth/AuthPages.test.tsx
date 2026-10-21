import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, Navigate, RouterProvider } from "react-router-dom";
import { expect, it, vi } from "vitest";
import AuthPages from "./AuthPages";
import { SessionProvider, useSession } from "./Session";
import { api } from "../api/client";

vi.mock("../api/client", async (original) => ({
  ...(await original<typeof import("../api/client")>()),
  api: vi.fn(),
}));

function Workspace() {
  const session = useSession();
  if (session.loading) return <p>Loading session</p>;
  if (!session.user) return <Navigate to="/login" replace />;
  return <h1>Workspace for {session.user.firstName}</h1>;
}

it.each(["/app", "/app/resources?text=fractions", "/app/account"])(
  "signs back in to %s despite a fresh signed-out cache, without retaining previous private data",
  async (destination) => {
    const client = new QueryClient({
      defaultOptions: { queries: { staleTime: 30_000, retry: false } },
    });
    // Session expiry after logout leaves a fresh null profile in the shared cache.
    client.setQueryData(["me"], null);
    client.setQueryData(["previous-organisation-private-data"], "private");
    vi.mocked(api).mockImplementation(async (path) => {
      if (path === "/api/browser/session/login") return undefined;
      if (path === "/api/auth/me") return { id: "teacher", firstName: "Chipo" };
      if (path.startsWith("/api/auth/me/memberships/search"))
        return { items: [], hasMore: false };
      if (path.startsWith("/api/workspace/access")) return {};
      if (path === "/api/browser/session")
        return {
          expiresAt: new Date(Date.now() + 600_000).toISOString(),
          serverTime: new Date().toISOString(),
        };
      throw new Error("Unexpected request: " + path);
    });
    const router = createMemoryRouter(
      [
        { path: "/login", element: <AuthPages /> },
        {
          path: "/app/*",
          element: (
            <SessionProvider>
              <Workspace />
            </SessionProvider>
          ),
        },
      ],
      { initialEntries: ["/login?return=" + encodeURIComponent(destination)] },
    );
    const view = render(
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    );
    try {
      const user = userEvent.setup();
      await user.type(
        screen.getByRole("textbox", { name: /Email address/ }),
        "teacher@mwabu.invalid",
      );
      await user.type(
        screen.getByLabelText(/^Password/),
        "Test-only-credential7!",
      );
      await user.click(screen.getByRole("button", { name: "Sign in" }));
      expect(
        await screen.findByRole("heading", { name: "Workspace for Chipo" }),
      ).toBeVisible();
      expect(
        router.state.location.pathname + router.state.location.search,
      ).toBe(destination === "/app/account" ? "/app" : destination);
      expect(
        client.getQueryData(["previous-organisation-private-data"]),
      ).toBeUndefined();
      expect(api).toHaveBeenCalledWith("/api/auth/me", expect.anything());
    } finally {
      view.unmount();
      router.dispose();
      client.clear();
    }
  },
);
