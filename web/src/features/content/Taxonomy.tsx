import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Layers, Plus } from "lucide-react";
import { api } from "../../api/client";
import type { Collection, Tag, Page } from "../../api/types";
import { useSession } from "../../auth/Session";
import {
  Badge,
  Empty,
  Form,
  Modal,
  PageTitle,
  Pagination,
  QueryState,
  type Field,
} from "../../components/ui";
export default function Taxonomy({ kind }: { kind: "collections" | "tags" }) {
  const session = useSession();
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [edit, setEdit] = useState<Collection | Tag | "new">();
  const allowed = session.can("content.read", "catalogue");
  const manage = session.can("content.manage", "global");
  const query = useQuery({
    queryKey: ["taxonomy", kind, page],
    queryFn: ({ signal }) =>
      api<Page<Collection>>(`/api/${kind}/search?page=${page}&pageSize=24`, {
        signal,
      }),
    enabled: allowed,
  });
  if (!allowed) return <Empty title="Catalogue access is unavailable" />;
  const fields: Field[] = [
    {
      name: "name",
      label: "Name",
      required: true,
      maxLength: kind === "tags" ? 100 : 200,
    },
    {
      name: "slug",
      label: "URL name",
      required: true,
      maxLength: kind === "tags" ? 100 : 200,
      hint: "Lowercase words separated by hyphens.",
    },
  ];
  if (kind === "collections")
    fields.push(
      {
        name: "description",
        label: "Introduction",
        type: "textarea",
        maxLength: 4000,
      },
      {
        name: "sortOrder",
        label: "Display order",
        type: "number",
        required: true,
        min: 0,
        defaultValue: 0,
      },
      {
        name: "isActive",
        label: "Active",
        type: "checkbox",
        defaultValue: true,
      },
    );
  return (
    <>
      <PageTitle
        eyebrow="CURATED FOR LEARNING"
        title={
          kind === "collections"
            ? "Good resources belong together."
            : "Make discovery easier."
        }
        actions={
          manage && (
            <button className="button" onClick={() => setEdit("new")}>
              <Plus size={16} />
              Add {kind === "tags" ? "tag" : "collection"}
            </button>
          )
        }
      >
        {kind === "collections"
          ? "Explore thoughtfully grouped lessons and teaching resources."
          : "Use meaningful tags to connect and discover resources."}
      </PageTitle>
      <QueryState query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="node-grid">
                {data.items.map((x) => (
                  <article className="card collection-card" key={x.id}>
                    <Layers size={26} />
                    <h2>{x.name}</h2>
                    <p>{x.description}</p>
                    {kind === "collections" && (
                      <Badge>{x.isActive ? "Active" : "Inactive"}</Badge>
                    )}
                    <div className="actions">
                      <Link
                        className="button secondary"
                        to={
                          "/app/resources?" +
                          (kind === "collections" ? "collectionId" : "tagId") +
                          "=" +
                          x.id
                        }
                      >
                        Explore resources
                      </Link>
                      {manage && (
                        <button
                          className="button text"
                          onClick={() => setEdit(x)}
                        >
                          Edit
                        </button>
                      )}
                    </div>
                  </article>
                ))}
              </div>
            ) : (
              <Empty title={"No " + kind + " yet"} />
            )}
            <Pagination
              page={page}
              hasMore={data.hasMore ?? false}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
      {edit && (
        <Modal
          title={
            (edit === "new" ? "Add " : "Edit ") +
            (kind === "tags" ? "tag" : "collection")
          }
          onClose={() => setEdit(undefined)}
        >
          <Form
            fields={fields}
            initial={edit === "new" ? {} : { ...edit }}
            submit={async (values) => {
              await api(
                "/api/" + kind + (edit === "new" ? "" : "/" + edit.id),
                { method: edit === "new" ? "POST" : "PUT", body: values },
              );
              setEdit(undefined);
              await client.invalidateQueries({ queryKey: ["taxonomy"] });
            }}
            onCancel={() => setEdit(undefined)}
          />
        </Modal>
      )}
    </>
  );
}
