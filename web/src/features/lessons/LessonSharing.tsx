import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import type { Page } from "../../api/types";
import {
  ErrorNotice,
  Pagination,
  QueryState,
  SearchBar,
} from "../../components/ui";
export default function LessonSharing({
  path,
  prepare,
}: {
  path: string;
  prepare: () => Promise<unknown>;
}) {
  const client = useQueryClient();
  const [user, setUser] = useState("");
  const [text, setText] = useState("");
  const [page, setPage] = useState(1);
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const base = path.slice(0, path.lastIndexOf("/"));
  const shares = useQuery({
    queryKey: ["lesson-shares", path],
    queryFn: () =>
      api<{ userId: string; name: string; isActive: boolean }[]>(
        path + "/shares",
      ),
  });
  const colleagues = useQuery({
    queryKey: ["lesson-colleagues", base, text, page],
    queryFn: ({ signal }) =>
      api<Page<{ userId: string; name: string }>>(
        base + "/colleagues?" + queryString({ text, page, pageSize: 20 }),
        { signal },
      ),
  });
  async function share(userId: string, isActive: boolean) {
    setBusy(true);
    setError(undefined);
    try {
      await prepare();
      await api(path + "/shares", {
        method: "PUT",
        body: { userId, isActive },
      });
      await client.invalidateQueries({ queryKey: ["lesson-shares", path] });
      setUser("");
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section>
      <h3>Explicit read-only sharing</h3>
      <p>
        Choose an active teaching colleague in this organisation. Only necessary
        staff names are shown; learners, email addresses and global accounts are
        excluded. Sharing never grants membership or permissions.
      </p>
      <SearchBar
        value={text}
        placeholder="Find a teaching colleague"
        onSearch={(value) => {
          setText(value);
          setPage(1);
          setUser("");
        }}
      />
      <QueryState query={colleagues}>
        {(data) => (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void share(user, true);
            }}
          >
            <label>
              Teaching colleague
              <select
                required
                value={user}
                onChange={(e) => setUser(e.target.value)}
              >
                <option value="">Choose a colleague</option>
                {data.items.map((item) => (
                  <option value={item.userId} key={item.userId}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
            <Pagination
              page={page}
              hasMore={data.hasMore ?? false}
              onChange={(value) => {
                setPage(value);
                setUser("");
              }}
            />
            <button className="button secondary" disabled={busy || !user}>
              Share lesson with colleague
            </button>
          </form>
        )}
      </QueryState>
      {!!error && <ErrorNotice error={error} />}
      <QueryState query={shares}>
        {(data) => (
          <ul>
            {data.map((item) => (
              <li key={item.userId}>
                {item.name} · {item.isActive ? "Shared" : "Revoked"}{" "}
                <button
                  className="button secondary"
                  disabled={busy}
                  onClick={() => void share(item.userId, !item.isActive)}
                >
                  {item.isActive ? "Revoke share" : "Restore share"}
                </button>
              </li>
            ))}
          </ul>
        )}
      </QueryState>
    </section>
  );
}
