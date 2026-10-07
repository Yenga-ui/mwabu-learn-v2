import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../api/client";
import type { Device, Page } from "../../api/types";
import { useSession } from "../../auth/Session";
import {
  Action,
  Badge,
  Empty,
  PageTitle,
  Pagination,
  QueryState,
} from "../../components/ui";
export default function Devices() {
  const session = useSession();
  const org = session.organisationId;
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [checkpointPage, setCheckpointPage] = useState(1);
  const allowed = !!org && session.can("reports.read");
  const query = useQuery({
    queryKey: ["devices", org, page],
    queryFn: () =>
      api<Page<Device>>(
        `/api/organisations/${org}/devices?page=${page}&pageSize=20`,
      ),
    enabled: allowed,
  });
  const checkpoints = useQuery({
    queryKey: ["device-checkpoints", org, checkpointPage],
    queryFn: ({ signal }) =>
      api<
        Page<{
          deviceId: string;
          deviceName: string;
          scope: string;
          version: number;
          ordinal: number;
          updatedAt: string;
        }>
      >(
        `/api/reports/organisations/${org}/checkpoints?page=${checkpointPage}&pageSize=20`,
        { signal },
      ),
    enabled: allowed,
  });
  if (!allowed)
    return (
      <Empty title="Select an organisation with device reporting access" />
    );
  return (
    <>
      <PageTitle
        eyebrow="DEVICE SUPPORT"
        title="A clear view of connected devices."
      >
        Registration belongs to an organisation, not permanently to one learner.
        Device credentials remain private.
      </PageTitle>
      <QueryState query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Device</th>
                      <th>Platform</th>
                      <th>State</th>
                      <th>Last activity</th>
                      <th>Credential expiry</th>
                      {session.can("memberships.manage") && <th>Actions</th>}
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((x) => (
                      <tr key={x.id}>
                        <td>
                          {x.displayName}
                          <small>
                            Registered{" "}
                            {new Date(x.registeredAt).toLocaleDateString()}
                          </small>
                        </td>
                        <td>
                          {x.platform}
                          <small>
                            {x.appVersion ?? "Version not reported"}
                          </small>
                        </td>
                        <td>
                          <Badge>{x.isActive ? "Active" : "Revoked"}</Badge>
                        </td>
                        <td>
                          {x.lastSeenAt
                            ? new Date(x.lastSeenAt).toLocaleString()
                            : "No activity recorded"}
                        </td>
                        <td>
                          {new Date(x.credentialExpiresAt).toLocaleDateString()}
                        </td>
                        {session.can("memberships.manage") && (
                          <td>
                            {x.isActive && (
                              <Action
                                confirm="Revoke this device's access? It will need a new registration before synchronising."
                                run={async () => {
                                  await api(
                                    `/api/organisations/${org}/devices/${x.id}/revoke`,
                                    { method: "POST" },
                                  );
                                  await client.invalidateQueries({
                                    queryKey: ["devices"],
                                  });
                                }}
                              >
                                Revoke access
                              </Action>
                            )}
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <Empty title="No registered devices">
                Devices registered by your organisation will appear here. This
                live browser does not register or pretend to synchronise offline
                content.
              </Empty>
            )}
            <Pagination
              page={page}
              hasMore={data.hasMore ?? false}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
      <section className="card">
        <h2>Catalogue checkpoints</h2>
        <p>
          Last acknowledged catalogue positions. These are operational records,
          not learner progress. No device credentials or sync cursors are
          exposed.
        </p>
        <QueryState query={checkpoints}>
          {(data) => (
            <>
              {data.items.length ? (
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Device</th>
                        <th>Scope</th>
                        <th>Version / ordinal</th>
                        <th>Recorded</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((checkpoint, index) => (
                        <tr key={checkpoint.deviceId + ":" + index}>
                          <td>{checkpoint.deviceName}</td>
                          <td>{checkpoint.scope}</td>
                          <td>
                            {checkpoint.version} / {checkpoint.ordinal}
                          </td>
                          <td>
                            {new Date(checkpoint.updatedAt).toLocaleString()}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <Empty title="No acknowledged checkpoints" />
              )}
              <Pagination
                page={checkpointPage}
                hasMore={data.hasMore ?? false}
                onChange={setCheckpointPage}
              />
            </>
          )}
        </QueryState>
      </section>
    </>
  );
}
