import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../api/client";
import { useSession } from "../../auth/Session";
import { Action, QueryState } from "../../components/ui";
type Grant = {
  teacherVisible: boolean;
  learnerVisible: boolean;
  canUse: boolean;
};
export default function GuestVisibility({ id }: { id: string }) {
  const session = useSession();
  const client = useQueryClient();
  const allowed = session.can("users.manage", "platform");
  const query = useQuery({
    queryKey: ["guest-visibility", id],
    queryFn: ({ signal }) =>
      api<Grant>(`/api/guest/content/${id}/visibility`, { signal }),
    enabled: allowed,
  });
  if (!allowed) return null;
  return (
    <section className="card">
      <h2>Guest access</h2>
      <p>
        Both guest experiences are public. Choosing “teacher” does not verify a
        visitor's identity; enable only material suitable for public samples.
      </p>
      <p>
        Only platform administrators can expose published content to the public.
        Enabling use makes supported media and anonymous activities accessible
        without a licence. SCORM packages are not enabled by this control.
      </p>
      <QueryState query={query}>
        {(grant) => (
          <div>
            {(["teacherVisible", "learnerVisible", "canUse"] as const).map(
              (key) => (
                <div key={key}>
                  <span>
                    {
                      {
                        teacherVisible: "Teacher guests",
                        learnerVisible: "Learner guests",
                        canUse: "Guest playback",
                      }[key]
                    }
                    : {grant[key] ? "Enabled" : "Disabled"}{" "}
                  </span>
                  <Action
                    confirm={`Change public guest access for this resource? Confirm its rights and suitability for this audience.`}
                    run={async () => {
                      await api(`/api/guest/content/${id}/visibility`, {
                        method: "PUT",
                        body: {
                          ...grant,
                          [key]: !grant[key],
                          ...(key === "canUse" && !grant[key]
                            ? {}
                            : key !== "canUse" &&
                                grant[key] &&
                                !grant[
                                  key === "teacherVisible"
                                    ? "learnerVisible"
                                    : "teacherVisible"
                                ]
                              ? { canUse: false }
                              : {}),
                        },
                      });
                      await client.invalidateQueries({
                        queryKey: ["guest-visibility", id],
                      });
                    }}
                  >
                    {grant[key] ? "Disable" : "Enable"}
                  </Action>
                </div>
              ),
            )}
          </div>
        )}
      </QueryState>
    </section>
  );
}
