import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import type { Role } from "../../api/types";
import { useSession } from "../../auth/Session";
import { Empty, PageTitle, QueryState } from "../../components/ui";
export default function RolePermissions() {
  const session = useSession();
  const allowed =
    !!session.access?.platformAuthority || session.can("memberships.manage");
  const roles = useQuery({
    queryKey: ["role-permission-matrix"],
    queryFn: () => api<Role[]>("/api/roles"),
    enabled: allowed,
  });
  const permissions = useQuery({
    queryKey: ["permission-names"],
    queryFn: () => api<{ code: string; name: string }[]>("/api/permissions"),
    enabled: allowed,
  });
  if (!allowed)
    return (
      <Empty title="Role administration is not available for this account" />
    );
  return (
    <>
      <PageTitle eyebrow="ORGANISATION SUPPORT" title="Roles and permissions">
        Effective access also depends on active organisation membership, scope
        and the server’s assignment limits.
      </PageTitle>
      <p>
        Organisation administrators cannot grant platform authority. Staff
        leaving a school should have that membership deactivated; retain their
        historical lessons and records.
      </p>
      <QueryState query={roles}>
        {(items) => (
          <div className="table-scroll card">
            <table>
              <thead>
                <tr>
                  <th>Permission</th>
                  {items.map((r) => (
                    <th key={r.id}>
                      {r.name}
                      {r.grantsPlatformAuthority && (
                        <small>Platform authority</small>
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {[...new Set(items.flatMap((r) => r.permissions))]
                  .sort()
                  .map((code) => (
                    <tr key={code}>
                      <th>
                        {permissions.data?.find((p) => p.code === code)?.name ??
                          code}
                      </th>
                      {items.map((r) => (
                        <td
                          key={r.id}
                          aria-label={`${r.name}: ${r.permissions.includes(code) ? "included" : "not included"}`}
                        >
                          {r.permissions.includes(code) ? "✓" : "—"}
                        </td>
                      ))}
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        )}
      </QueryState>
      <p>
        A role definition alone never authorises a cross-organisation or
        platform action. Assignment and access checks remain server-enforced.
      </p>
    </>
  );
}
