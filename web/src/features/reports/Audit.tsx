import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import type { Audit, Page } from "../../api/types";
import { useSession } from "../../auth/Session";
import { Empty, PageTitle, Pagination, QueryState } from "../../components/ui";
export default function AuditPage() {
  const session = useSession();
  const [filters, setFilters] = useState<Record<string, string>>({});
  const [page, setPage] = useState(1);
  const allowed = session.can("users.read", "platform");
  const query = useQuery({
    queryKey: ["audit", filters, page],
    queryFn: () =>
      api<Page<Audit>>(
        "/api/audit-events?" + queryString({ ...filters, page, pageSize: 25 }),
      ),
    enabled: allowed,
  });
  if (!allowed)
    return <Empty title="Audit access requires platform permission" />;
  return (
    <>
      <PageTitle
        eyebrow="ACCOUNTABILITY"
        title="An identifier-only audit trail."
      >
        Bounded operational events without passwords, tokens or personal-field
        snapshots. Default: the last seven days.
      </PageTitle>
      <form
        className="card filter-row"
        onSubmit={(e) => {
          e.preventDefault();
          const data = new FormData(e.currentTarget);
          setFilters({
            eventType: String(data.get("eventType") ?? ""),
            from: data.get("from")
              ? new Date(String(data.get("from"))).toISOString()
              : "",
            to: data.get("to")
              ? new Date(String(data.get("to")) + "T23:59:59Z").toISOString()
              : "",
          });
          setPage(1);
        }}
      >
        <label>
          Event type
          <input
            name="eventType"
            maxLength={100}
            placeholder="e.g. content.published"
          />
        </label>
        <label>
          From
          <input name="from" type="date" />
        </label>
        <label>
          To
          <input name="to" type="date" />
        </label>
        <button className="button secondary">Apply filters</button>
      </form>
      <QueryState query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>When</th>
                      <th>Event</th>
                      <th>Resource</th>
                      <th>Actor / organisation</th>
                      <th>Support reference</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((x) => (
                      <tr key={x.id}>
                        <td>{new Date(x.occurredAt).toLocaleString()}</td>
                        <td>{x.eventType}</td>
                        <td>
                          {x.entityType}
                          <small>{x.entityId}</small>
                        </td>
                        <td>
                          <small>{x.actorUserId ?? "System"}</small>
                          <small>{x.organisationId}</small>
                        </td>
                        <td>
                          <small>{x.correlationId ?? "Not recorded"}</small>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <Empty title="No events match these filters" />
            )}
            <Pagination
              page={page}
              hasMore={data.hasMore ?? false}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
    </>
  );
}
