import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import { useSession } from "../../auth/Session";
import { Empty, QueryState } from "../../components/ui";
import type { Resource } from "../../api/types";
import { MediaViewer } from "../content/Resource";
import { PublishedActivity } from "../content/activities/ActivityPlayer";
import { activityMime } from "../content/activities/activityRuntime";
import { lessonBase, type Detail } from "./types";

export default function LessonPresentation() {
  const { id } = useParams();
  const session = useSession();
  const [params] = useSearchParams();
  const revision = params.has("revision")
    ? Number(params.get("revision"))
    : undefined;
  const valid =
    revision === undefined || (Number.isInteger(revision) && revision > 0);
  const org = session.organisationId;
  const query = useQuery({
    queryKey: ["presented-lesson", org, id, revision],
    queryFn: ({ signal }) =>
      api<Detail>(lessonBase(org!) + `/${id}?` + queryString({ revision }), {
        signal,
      }),
    enabled: !!org && session.can("lessons.read") && valid,
    refetchOnWindowFocus: false,
  });
  if (!org || !session.can("lessons.read"))
    return <Empty title="Select an organisation with lesson access" />;
  if (!valid) return <Empty title="Choose a saved lesson revision" />;
  return (
    <QueryState query={query}>
      {(detail) => (
        <LessonSlides
          key={`${org}:${id}:${detail.viewedRevision}`}
          detail={detail}
          resources={<PresentationResources detail={detail} />}
        />
      )}
    </QueryState>
  );
}
function PresentationResources({ detail }: { detail: Detail }) {
  const references = detail.document.references.filter((x) => x.contentItemId);
  const [selected, setSelected] = useState("");
  const query = useQuery({
    queryKey: ["presentation-resource", selected],
    queryFn: ({ signal }) =>
      api<Resource>(`/api/learning/content/${selected}`, { signal }),
    enabled: !!selected,
  });
  if (!references.length) return null;
  return (
    <section aria-label="Teaching resources">
      <h2>Teaching resources</h2>
      <label>
        Open a teaching resource
        <select
          value={selected}
          onChange={(event) => setSelected(event.target.value)}
        >
          <option value="">Choose a resource…</option>
          {references.map((x) => (
            <option key={x.id} value={x.contentItemId!}>
              {x.title}
            </option>
          ))}
        </select>
      </label>
      {selected && (
        <QueryState query={query}>
          {(resource) => {
            const asset =
              resource.assets.find((x) => x.isPrimary) ?? resource.assets[0];
            return asset ? (
              asset.mimeType === activityMime ? (
                <PublishedActivity
                  key={asset.id}
                  resourceId={resource.content.id}
                  assetId={asset.id}
                  downloadUrl={asset.downloadUrl}
                  downloadable={false}
                />
              ) : (
                <MediaViewer key={asset.id} asset={asset} />
              )
            ) : (
              <p>
                {resource.content.description ??
                  "No teaching files are attached."}
              </p>
            );
          }}
        </QueryState>
      )}
    </section>
  );
}
export function LessonSlides({
  detail,
  resources,
}: {
  detail: Detail;
  resources?: ReactNode;
}) {
  const [index, setIndex] = useState(0);
  const [notes, setNotes] = useState(false);
  const [full, setFull] = useState(false);
  const [notice, setNotice] = useState("");
  const root = useRef<HTMLElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const notesId = useId();
  const slides = useMemo(() => {
    const visible = new Set([
      "objectives",
      "starter",
      "development",
      "learner-activities",
      "questions",
      "formative-assessment",
      "conclusion",
    ]);
    const selected = detail.document.sections.filter(
      (x) => visible.has(x.code) && x.body.trim(),
    );
    return selected.length
      ? selected
      : [
          {
            code: "objectives",
            title: "Learning objectives",
            body:
              detail.document.setup.objectives ||
              "Add teaching content to the saved lesson before presenting it.",
            minutes: 0,
            sourceIds: [],
          },
        ];
  }, [detail.document]);
  const position = Math.min(index, slides.length - 1);
  const slide = slides[position];
  useEffect(() => {
    heading.current?.focus();
  }, [position]);
  useEffect(() => {
    const update = () => setFull(document.fullscreenElement === root.current);
    document.addEventListener("fullscreenchange", update);
    return () => document.removeEventListener("fullscreenchange", update);
  }, []);
  return (
    <section
      ref={root}
      className="lesson-presentation panel"
      aria-label="Classroom presentation"
      onKeyDown={(event) => {
        if (
          ["INPUT", "TEXTAREA", "SELECT"].includes(
            (event.target as HTMLElement).tagName,
          )
        )
          return;
        if (event.key === "ArrowRight") {
          event.preventDefault();
          setIndex(Math.min(slides.length - 1, position + 1));
        }
        if (event.key === "ArrowLeft") {
          event.preventDefault();
          setIndex(Math.max(0, position - 1));
        }
      }}
    >
      <header>
        <p className="eyebrow">CLASSROOM PRESENTATION</p>
        <h1>{detail.document.setup.title}</h1>
        <p>
          Saved revision {detail.viewedRevision} · {detail.lesson.status}
          {detail.lesson.status !== "Published" &&
            " · Teacher preview — review before classroom use"}
        </p>
      </header>
      <nav className="actions" aria-label="Presentation controls">
        <button
          className="button secondary"
          disabled={position === 0}
          onClick={() => setIndex(position - 1)}
        >
          Previous section
        </button>
        <label>
          Lesson section
          <select
            value={position}
            onChange={(event) => setIndex(Number(event.target.value))}
          >
            {slides.map((item, i) => (
              <option key={item.code} value={i}>
                {item.title}
              </option>
            ))}
          </select>
        </label>
        <button
          className="button"
          disabled={position === slides.length - 1}
          onClick={() => setIndex(position + 1)}
        >
          Next section
        </button>
        <button
          className="button secondary"
          onClick={() =>
            void (async () => {
              try {
                if (full) await document.exitFullscreen();
                else if (root.current?.requestFullscreen)
                  await root.current.requestFullscreen();
                else
                  setNotice(
                    "Fullscreen is unavailable. You can continue presenting here.",
                  );
              } catch {
                setNotice(
                  "Fullscreen could not start. You can continue presenting here.",
                );
              }
            })()
          }
        >
          {full ? "Exit fullscreen" : "Enter fullscreen"}
        </button>
        <a
          href={`/app/lessons/${detail.lesson.id}`}
          className="button secondary"
        >
          Back to saved lesson
        </a>
      </nav>
      {!!notice && <p role="status">{notice}</p>}
      <article className="presentation-slide">
        <h2 tabIndex={-1} ref={heading}>
          {slide.title}
        </h2>
        <p className="preserve-lines">{slide.body}</p>
        {slide.minutes > 0 && <p>{slide.minutes} minutes</p>}
      </article>
      <p aria-live="polite">
        Section {position + 1} of {slides.length}. Use the arrow keys or section
        controls.
      </p>
      {resources}
      <button
        className="button secondary"
        aria-expanded={notes}
        aria-controls={notesId}
        onClick={() => setNotes(!notes)}
      >
        {notes ? "Hide teacher notes" : "Show teacher notes"}
      </button>
      {notes && (
        <aside id={notesId} aria-label="Teacher notes">
          {detail.document.sections
            .filter(
              (x) =>
                [
                  "teacher-activities",
                  "differentiation",
                  "disability-support",
                  "low-resource",
                ].includes(x.code) && x.body.trim(),
            )
            .map((x) => (
              <section key={x.code}>
                <h3>{x.title}</h3>
                <p className="preserve-lines">{x.body}</p>
              </section>
            ))}
        </aside>
      )}
    </section>
  );
}
