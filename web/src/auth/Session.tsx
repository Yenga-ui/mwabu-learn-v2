import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import {
  useInfiniteQuery,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { api, ensureSession, sessionEvents } from "../api/client";
import type { Access, MyMembership, Page, User } from "../api/types";
import { can, type Scope } from "./permissions";

interface SessionState {
  user?: User;
  memberships: MyMembership[];
  organisationId?: string;
  setOrganisation: (id: string) => void;
  access?: Access;
  loading: boolean;
  error: Error | null;
  retry: () => void;
  can: (permission: string, scope?: Scope) => boolean;
}
const Session = createContext<SessionState | undefined>(undefined);
export function SessionProvider({ children }: { children: ReactNode }) {
  const client = useQueryClient();
  const [organisationId, setOrganisationId] = useState<string>();
  const user = useQuery({
    queryKey: ["me"],
    queryFn: ({ signal }) => api<User | null>("/api/auth/me", { signal }),
    retry: false,
  });
  const memberships = useInfiniteQuery({
    queryKey: ["memberships"],
    initialPageParam: 1,
    getNextPageParam: (last: Page<MyMembership>) =>
      last.hasMore ? (last.pageNumber ?? last.page ?? 1) + 1 : undefined,
    queryFn: ({ signal, pageParam }) =>
      api<Page<MyMembership>>(
        `/api/auth/me/memberships/search?page=${pageParam}&pageSize=100`,
        { signal },
      ),
    enabled: !!user.data,
  });
  const active =
    memberships.data?.pages
      .flatMap((page) => page.items)
      .filter((x) => x.membership.isActive && x.organisationIsActive) ?? [];
  const selected = organisationId ?? active[0]?.membership.organisationId;
  const access = useQuery({
    queryKey: ["access", selected],
    queryFn: ({ signal }) =>
      api<Access>(
        `/api/workspace/access${selected ? "?organisationId=" + selected : ""}`,
        { signal },
      ),
    enabled: !!user.data,
  });
  const refetchUser = user.refetch;
  const userId = user.data?.id;
  useEffect(() => {
    if (!userId) return;
    let stopped = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const schedule = async () => {
      try {
        const status = await api<{ expiresAt: string; serverTime: string }>(
          "/api/browser/session",
        );
        if (stopped) return;
        const delay = Math.max(
          1000,
          Date.parse(status.expiresAt) - Date.parse(status.serverTime) - 45000,
        );
        timer = setTimeout(async () => {
          try {
            if (!(await ensureSession(60))) {
              sessionEvents.dispatchEvent(new Event("expired"));
              return;
            }
            if (!stopped) void schedule();
          } catch {
            if (!stopped) timer = setTimeout(() => void schedule(), 30000);
          }
        }, delay);
      } catch {
        if (!stopped) timer = setTimeout(() => void schedule(), 30000);
      }
    };
    void schedule();
    return () => {
      stopped = true;
      if (timer) clearTimeout(timer);
    };
  }, [userId]);
  useEffect(() => {
    const reset = () => {
      client.clear();
      setOrganisationId(undefined);
      void refetchUser();
    };
    const expired = () => {
      void client.cancelQueries({
        predicate: (query) => query.queryKey[0] !== "me",
      });
      client.removeQueries({
        predicate: (query) => query.queryKey[0] !== "me",
      });
      client.setQueryData(["me"], null);
      setOrganisationId(undefined);
    };
    sessionEvents.addEventListener("changed", reset);
    sessionEvents.addEventListener("expired", expired);
    return () => {
      sessionEvents.removeEventListener("changed", reset);
      sessionEvents.removeEventListener("expired", expired);
    };
  }, [client, refetchUser]);
  const value: SessionState = {
    user: user.data ?? undefined,
    memberships: active,
    organisationId: selected,
    setOrganisation: (id) => {
      void client.cancelQueries();
      client.removeQueries({
        predicate: (query) =>
          !["me", "memberships"].includes(String(query.queryKey[0])),
      });
      setOrganisationId(id);
    },
    access: access.data,
    loading:
      user.isPending ||
      (!!user.data && (memberships.isPending || access.isPending)),
    error: user.error ?? memberships.error ?? access.error,
    retry: () => {
      void user.refetch();
      void memberships.refetch();
      void access.refetch();
    },
    can: (code, scope) => can(access.data, code, scope),
  };
  return (
    <Session.Provider value={value}>
      {children}
      {memberships.hasNextPage && (
        <button
          className="context-more"
          disabled={memberships.isFetchingNextPage}
          onClick={() => void memberships.fetchNextPage()}
        >
          More organisation memberships
        </button>
      )}
    </Session.Provider>
  );
}
export function useSession() {
  const value = useContext(Session);
  if (!value) throw new Error("Session provider is required.");
  return value;
}
export function Can({
  permission,
  scope,
  children,
}: {
  permission: string;
  scope?: Scope;
  children: ReactNode;
}) {
  return useSession().can(permission, scope) ? children : null;
}
