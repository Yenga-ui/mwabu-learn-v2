import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../../api/client";
import type { Content } from "../../api/types";
import { Action } from "../../components/ui";
import NodePicker from "../curriculum/NodePicker";

export default function MappingRevision({ resource }: { resource: Content }) {
  const navigate = useNavigate();
  const [target, setTarget] = useState<{
    nodeType: string;
    nodeId: string;
    context: string[];
  }>();
  const [evidence, setEvidence] = useState("");
  const [slug, setSlug] = useState(resource.slug + "-mapped");
  return (
    <section className="card">
      <h2>Review curriculum mapping</h2>
      <p>
        Choose the most specific curriculum record supported by the source. The
        full context is shown before saving. If a topic or outcome is missing,
        select a supported parent or ask a curriculum manager to add the
        verified record.
      </p>
      <p>
        This creates a draft revision with verified copies of the original
        files. The published version stays unchanged. Review and publish the
        revision separately.
      </p>
      <NodePicker
        startWithGrade
        onReset={() => setTarget(undefined)}
        onSelect={(nodeType, nodeId, name, context) =>
          setTarget({
            nodeType,
            nodeId,
            context: context.at(-1) === name ? context : [...context, name],
          })
        }
      />
      {target && (
        <>
          <p>
            <strong>Selected curriculum context:</strong>{" "}
            {target.context.join(" → ")}
          </p>
          <label>
            Evidence for this mapping
            <textarea
              maxLength={2000}
              value={evidence}
              onChange={(e) => setEvidence(e.target.value)}
              placeholder="Source document, edition, grade and exact matching evidence. Record unresolved claims rather than guessing."
            />
          </label>
          <label>
            Revision URL name
            <input
              value={slug}
              maxLength={200}
              pattern="[a-z0-9]+(-[a-z0-9]+)*"
              onChange={(e) => setSlug(e.target.value)}
            />
          </label>
          {evidence.trim().length >= 20 &&
            /^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug) && (
              <Action
                confirm="Create a draft mapping revision with copies of the original files?"
                run={async () => {
                  const created = await api<Content>(
                    `/api/content/${resource.id}/mapping-revisions`,
                    {
                      method: "POST",
                      body: {
                        slug,
                        mapping: {
                          nodeType: target.nodeType,
                          nodeId: target.nodeId,
                          evidence,
                        },
                      },
                    },
                  );
                  navigate(`/app/content/${created.id}`);
                }}
              >
                Create mapping revision
              </Action>
            )}
        </>
      )}
    </section>
  );
}
