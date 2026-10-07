import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronRight, Plus, Pencil } from "lucide-react";
import { api, queryString } from "../../api/client";
import type {
  Curriculum as CurriculumType,
  Page,
  Structure,
} from "../../api/types";
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
export const nodeTypes = [
  "CurriculumVersion",
  "Grade",
  "Subject",
  "Term",
  "Topic",
  "Competency",
  "LearningOutcome",
] as const;
export const nodeNames = [
  "Versions",
  "Grades",
  "Subjects",
  "Terms",
  "Topics",
  "Competencies",
  "Learning outcomes",
];
export const parentRoutes = [
  "curricula",
  "versions",
  "grades",
  "subjects",
  "terms",
  "topics",
  "competencies",
];
export const childRoutes = [
  "versions",
  "grades",
  "subjects",
  "terms",
  "topics",
  "competencies",
  "learning-outcomes",
];
export const structureFields: Field[] = [
  { name: "name", label: "Name", required: true, maxLength: 200 },
  { name: "code", label: "Code", maxLength: 50 },
  {
    name: "description",
    label: "Description",
    type: "textarea",
    maxLength: 4000,
  },
  {
    name: "sortOrder",
    label: "Display order",
    type: "number",
    min: 0,
    defaultValue: 0,
    required: true,
  },
  { name: "isActive", label: "Active", type: "checkbox", defaultValue: true },
];
export default function Curriculum() {
  const session = useSession();
  const client = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [page, setPage] = useState(1);
  const [trail, setTrail] = useState<{ id: string; name: string }[]>(
    params.get("curriculumId")
      ? [{ id: params.get("curriculumId")!, name: "Curriculum" }]
      : [],
  );
  const [editing, setEditing] = useState<Structure | CurriculumType | "new">();
  const level = trail.length - 1;
  const root = trail[0]?.id;
  const parent = trail.at(-1)?.id;
  const query = useQuery({
    queryKey: ["curriculum-nodes", root, parent, level, page],
    queryFn: ({ signal }) =>
      level < 0
        ? api<Page<CurriculumType>>(
            `/api/curricula/search?page=${page}&pageSize=30`,
            { signal },
          )
        : api<Page<Structure>>(
            `/api/curricula/${root}/nodes/${nodeTypes[level]}?` +
              queryString({ parentId: parent, page, pageSize: 30 }),
            { signal },
          ),
    enabled: session.can("curriculum.read", "catalogue") && level < 7,
  });
  const managed = session.can("curriculum.manage", "global");
  if (!session.can("curriculum.read", "catalogue"))
    return <Empty title="Curriculum access is not available" />;
  function select(node: Structure) {
    setTrail((x) => [...x, { id: node.id, name: node.name }]);
    setPage(1);
    if (!root) setParams({ curriculumId: node.id });
  }
  const path =
    level < 0
      ? "/api/curricula"
      : `/api/${parentRoutes[level]}/${parent}/${childRoutes[level]}`;
  return (
    <>
      <PageTitle
        eyebrow="CURRICULUM EXPLORER"
        title={trail.at(-1)?.name ?? "Start with your curriculum."}
        actions={
          managed &&
          level < 7 && (
            <button className="button" onClick={() => setEditing("new")}>
              <Plus size={16} />
              Add{" "}
              {level < 0
                ? "curriculum"
                : [
                    "version",
                    "grade",
                    "subject",
                    "term",
                    "topic",
                    "competency",
                    "learning outcome",
                  ][level]}
            </button>
          )
        }
      >
        Follow the learning journey from framework to outcome.
      </PageTitle>
      <nav className="curriculum-trail" aria-label="Curriculum path">
        <button
          onClick={() => {
            setTrail([]);
            setParams({});
            setPage(1);
          }}
        >
          All curricula
        </button>
        {trail.map((node, index) => (
          <span key={node.id}>
            <ChevronRight size={15} />
            <button
              onClick={() => {
                setTrail((x) => x.slice(0, index + 1));
                setPage(1);
              }}
            >
              {node.name}
            </button>
          </span>
        ))}
      </nav>
      {level < 7 ? (
        <QueryState query={query}>
          {(data) => (
            <>
              <h2>{level < 0 ? "Available curricula" : nodeNames[level]}</h2>
              {data.items.length ? (
                <div className="node-grid">
                  {data.items.map((node) => (
                    <article className="card curriculum-node" key={node.id}>
                      <button
                        className="node-open"
                        onClick={() => select(node)}
                      >
                        <span className="node-code">
                          {node.code ??
                            (node.sortOrder + 1).toString().padStart(2, "0")}
                        </span>
                        <div>
                          <h3>{node.name}</h3>
                          <p>{node.description}</p>
                        </div>
                        <ChevronRight size={20} />
                      </button>
                      <div className="node-actions">
                        <Badge>{node.isActive ? "Active" : "Inactive"}</Badge>
                        {managed && (
                          <button
                            className="button text"
                            onClick={() => setEditing(node)}
                          >
                            <Pencil size={14} />
                            Edit
                          </button>
                        )}
                        {level >= 0 && (
                          <Link
                            to={
                              "/app/resources?" +
                              queryString({
                                [nodeTypes[level][0].toLowerCase() +
                                nodeTypes[level].slice(1) +
                                "Id"]: node.id,
                              })
                            }
                          >
                            Find resources
                          </Link>
                        )}
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <Empty
                  title={
                    "No " +
                    (level < 0 ? "curricula" : nodeNames[level].toLowerCase()) +
                    " here yet"
                  }
                >
                  Choose another curriculum branch or add a node if you have
                  management access.
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
      ) : (
        <section className="card">
          <h2>Learning outcome</h2>
          <p>
            You have reached the most specific learning objective in this
            curriculum.
          </p>
          <Link
            className="button"
            to={"/app/resources?learningOutcomeId=" + parent}
          >
            Explore connected resources
          </Link>
        </section>
      )}
      {editing && (
        <Modal
          title={
            editing === "new"
              ? "Add " +
                (level < 0 ? "curriculum" : nodeNames[level].toLowerCase())
              : "Edit " + editing.name
          }
          onClose={() => setEditing(undefined)}
        >
          <Form
            fields={
              level < 0
                ? [
                    {
                      name: "countryCode",
                      label: "Country code",
                      required: true,
                      maxLength: 2,
                      minLength: 2,
                      hint: "ISO alpha-2 code, for example ZM.",
                    },
                    ...structureFields,
                  ]
                : structureFields
            }
            initial={editing === "new" ? {} : { ...editing }}
            submit={async (values) => {
              await api(path + (editing === "new" ? "" : "/" + editing.id), {
                method: editing === "new" ? "POST" : "PUT",
                body: values,
              });
              setEditing(undefined);
              await client.invalidateQueries({
                queryKey: ["curriculum-nodes"],
              });
            }}
            onCancel={() => setEditing(undefined)}
          />
        </Modal>
      )}
    </>
  );
}
