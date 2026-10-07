import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import NodePicker from "../curriculum/NodePicker";
import {
  ArrowUpRight,
  BookOpen,
  Headphones,
  FileText,
  Play,
  Clock,
} from "lucide-react";
import { api } from "../../api/client";
import type { Content, Page, Collection, Tag } from "../../api/types";
import { useSession } from "../../auth/Session";
import {
  Badge,
  Empty,
  PageTitle,
  Pagination,
  QueryState,
  SearchBar,
} from "../../components/ui";
export function ContentCard({ content }: { content: Content }) {
  const Icon = content.contentType.includes("audio")
    ? Headphones
    : content.contentType.includes("video")
      ? Play
      : content.contentType.includes("plan")
        ? FileText
        : BookOpen;
  return (
    <Link className="content-card" to={"/app/resources/" + content.id}>
      <div
        className={
          "resource-art " + content.contentType.replace(/[^a-z-]/g, "")
        }
      >
        <Icon size={42} strokeWidth={1.4} />
        <span>{content.languageCode.toUpperCase()}</span>
      </div>
      <div className="content-card-body">
        <Badge>{content.contentType.replaceAll("-", " ")}</Badge>
        <h3>{content.title}</h3>
        <p>{content.summary ?? "Open this resource to explore more."}</p>
        <div className="card-meta">
          {content.estimatedDurationMinutes && (
            <span>
              <Clock size={14} />
              {content.estimatedDurationMinutes} min
            </span>
          )}
          <span>
            Explore <ArrowUpRight size={16} />
          </span>
        </div>
      </div>
    </Link>
  );
}
export default function Catalogue() {
  const session = useSession();
  const [params, setParams] = useSearchParams();
  const [collectionPage, setCollectionPage] = useState(1);
  const [tagPage, setTagPage] = useState(1);
  const page = Math.max(1, Number(params.get("page")) || 1);
  const search = useQuery({
    queryKey: ["resources", params.toString()],
    queryFn: ({ signal }) =>
      api<Page<Content>>("/api/learning/content?" + params.toString(), {
        signal,
      }),
    enabled: session.can("content.read", "catalogue"),
  });
  const collections = useQuery({
    queryKey: ["collections-picker", collectionPage],
    queryFn: ({ signal }) =>
      api<Page<Collection>>(
        `/api/collections/search?pageSize=100&page=${collectionPage}`,
        { signal },
      ),
    enabled: session.can("content.read", "catalogue"),
  });
  const tags = useQuery({
    queryKey: ["tags-picker", tagPage],
    queryFn: ({ signal }) =>
      api<Page<Tag>>(`/api/tags/search?pageSize=100&page=${tagPage}`, {
        signal,
      }),
    enabled: session.can("content.read", "catalogue"),
  });
  const update = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    next.delete("page");
    if (value) next.set(key, value);
    else next.delete(key);
    setParams(next);
  };
  if (!session.can("content.read", "catalogue"))
    return (
      <Empty title="Resources are not available for this account">
        Ask your organisation administrator about catalogue access.
      </Empty>
    );
  return (
    <>
      <PageTitle
        eyebrow="EXPLORE & DISCOVER"
        title={
          params.get("contentType") === "lesson-plan"
            ? "A good lesson starts here."
            : "Find your next learning moment."
        }
      >
        Classroom-ready resources, connected to your curriculum.
      </PageTitle>
      <section className="catalogue-tools card">
        <SearchBar
          value={params.get("text") ?? ""}
          onSearch={(text) => update("text", text)}
          placeholder="Search lessons, topics, tags or descriptions…"
        />
        <div className="filter-row">
          <label>
            Resource type
            <select
              value={params.get("contentType") ?? ""}
              onChange={(e) => update("contentType", e.target.value)}
            >
              <option value="">All resources</option>
              {[
                "lesson",
                "lesson-plan",
                "teacher-guide",
                "video",
                "audio",
                "interactive-activity",
              ].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </label>
          <label>
            Collection
            <select
              value={params.get("collectionId") ?? ""}
              onChange={(e) => update("collectionId", e.target.value)}
            >
              <option value="">All collections</option>
              {collections.data?.items.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Tag
            <select
              value={params.get("tagId") ?? ""}
              onChange={(e) => update("tagId", e.target.value)}
            >
              <option value="">All tags</option>
              {tags.data?.items.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Language
            <input
              maxLength={35}
              value={params.get("languageCode") ?? ""}
              onChange={(e) => update("languageCode", e.target.value)}
              placeholder="e.g. en"
            />
          </label>
          <button className="button text" onClick={() => setParams({})}>
            Clear filters
          </button>
        </div>
      </section>
      {(collections.data?.hasMore || collectionPage > 1) && (
        <section className="card">
          <h3>Browse more collections</h3>
          <Pagination
            page={collectionPage}
            hasMore={collections.data?.hasMore ?? false}
            onChange={setCollectionPage}
          />
        </section>
      )}
      {(tags.data?.hasMore || tagPage > 1) && (
        <section className="card">
          <h3>Browse more tags</h3>
          <Pagination
            page={tagPage}
            hasMore={tags.data?.hasMore ?? false}
            onChange={setTagPage}
          />
        </section>
      )}
      <details className="card">
        <summary>
          Filter by curriculum, grade, subject or learning objective
        </summary>
        <NodePicker
          onSelect={(type, id) => {
            const next = new URLSearchParams(params);
            for (const key of [
              "curriculumVersionId",
              "gradeId",
              "subjectId",
              "termId",
              "topicId",
              "competencyId",
              "learningOutcomeId",
            ])
              next.delete(key);
            next.delete("page");
            next.set(type[0].toLowerCase() + type.slice(1) + "Id", id);
            setParams(next);
          }}
        />
      </details>
      <QueryState query={search}>
        {(data) => (
          <>
            <div className="section-heading">
              <h2>Learning resources</h2>
              <span className="muted">{data.totalCount} resources</span>
            </div>
            {data.items.length ? (
              <div className="content-grid">
                {data.items.map((x) => (
                  <ContentCard key={x.id} content={x} />
                ))}
              </div>
            ) : (
              <Empty title="No resources match this search">
                Try a broader topic, another collection or clear your filters.
              </Empty>
            )}
            <Pagination
              page={page}
              hasMore={page * data.pageSize < (data.totalCount ?? 0)}
              onChange={(p) => {
                const next = new URLSearchParams(params);
                next.set("page", String(p));
                setParams(next);
              }}
            />
          </>
        )}
      </QueryState>
    </>
  );
}
