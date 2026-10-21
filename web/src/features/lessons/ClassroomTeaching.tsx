import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import { useSession } from "../../auth/Session";
import { Empty, QueryState } from "../../components/ui";
import { lessonBase, type Detail } from "./types";
import { journeySteps, publicStep } from "./journey";
import { PinnedResource } from "./LessonJourney";
import {
  readTeachingBookmark,
  saveTeachingBookmark,
  forgetTeachingBookmark,
  type TeachingBookmark,
} from "./teachingBookmark";
export interface ClassroomLesson {
  lessonId: string;
  revision: number;
  title: string;
  language: string;
  learningOutcome?: string;
  steps: Omit<ReturnType<typeof publicStep>, "sectionCode">[];
}

export default function ClassroomTeaching() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const session = useSession();
  const org = session.organisationId;
  const revision = Number(params.get("revision"));
  const valid = Number.isInteger(revision) && revision > 0;
  const query = useQuery({
    queryKey: ["classroom-lesson", org, id, revision],
    queryFn: ({ signal }) =>
      api<ClassroomLesson>(
        lessonBase(org!) + `/${id}/classroom?` + queryString({ revision }),
        { signal },
      ),
    enabled: !!org && session.can("lessons.read") && valid,
    refetchOnWindowFocus: false,
  });
  if (!org || !session.can("lessons.read"))
    return <Empty title="Select an organisation with lesson access" />;
  if (!valid)
    return <Empty title="Open a saved revision from the Lesson Journey" />;
  return (
    <QueryState query={query}>
      {(detail) => (
        <ClassroomCanvas
          key={`${org}:${session.user?.id}:${id}:${revision}`}
          detail={detail}
          bookmark={
            session.user && params.get("resume") === "true"
              ? readTeachingBookmark(org!, session.user.id)
              : undefined
          }
          onPosition={(stepId, assetId) => {
            if (session.user)
              saveTeachingBookmark(org!, session.user.id, {
                lessonId: detail.lessonId,
                revision: detail.revision,
                stepId,
                assetId,
                updatedAt: Date.now(),
              });
          }}
          onFinish={() => {
            if (session.user) forgetTeachingBookmark(org!, session.user.id);
          }}
        />
      )}
    </QueryState>
  );
}
export function ClassroomCanvas({
  detail,
  bookmark,
  onPosition,
  onFinish,
}: {
  detail: Detail | ClassroomLesson;
  bookmark?: TeachingBookmark;
  onPosition?: (stepId: string, assetId: string) => void;
  onFinish?: () => void;
}) {
  const model: ClassroomLesson =
    "document" in detail
      ? {
          lessonId: detail.lesson.id,
          revision: detail.viewedRevision,
          title: detail.document.setup.title,
          language: detail.document.setup.language,
          steps: journeySteps(detail).map(publicStep),
        }
      : detail;
  const steps = model.steps;
  const resume =
    bookmark?.lessonId === model.lessonId &&
    bookmark.revision === model.revision
      ? bookmark
      : undefined;
  const [index, setIndex] = useState(() =>
    Math.max(
      0,
      steps.findIndex((step) => step.id === resume?.stepId),
    ),
  );
  const [hint, setHint] = useState(false);
  const [answer, setAnswer] = useState(false);
  const [overview, setOverview] = useState(false);
  const [asset, setAsset] = useState(() =>
    steps
      .find((step) => step.id === resume?.stepId)
      ?.resources.some((resource) => resource.assetId === resume?.assetId)
      ? resume!.assetId
      : "",
  );
  const [notice, setNotice] = useState("");
  const [full, setFull] = useState(false);
  const root = useRef<HTMLElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const step = steps[index];
  useEffect(() => {
    if (step) onPosition?.(step.id, asset);
  }, [step, asset, onPosition]);
  useEffect(() => {
    heading.current?.focus();
  }, [index]);
  useEffect(() => {
    const change = () => setFull(document.fullscreenElement === root.current);
    document.addEventListener("fullscreenchange", change);
    return () => document.removeEventListener("fullscreenchange", change);
  }, []);
  function go(next: number) {
    setIndex(Math.max(0, Math.min(steps.length - 1, next)));
    setHint(false);
    setAnswer(false);
    setAsset("");
    setOverview(false);
  }
  if (!step) return <Empty title="Prepare teaching steps before presenting" />;
  const reference = step.resources.find((r) => r.assetId === asset);
  return (
    <section
      ref={root}
      className="classroom-canvas"
      aria-label="Classroom teaching"
      onKeyDown={(event) => {
        // Never capture arrow keys from media, editable controls or interactive activities.
        if (
          (event.target as HTMLElement).closest(
            "input,textarea,select,button,a,video,audio,[role=slider],.activity-player",
          )
        )
          return;
        if (event.key === "ArrowRight") {
          event.preventDefault();
          go(index + 1);
        }
        if (event.key === "ArrowLeft") {
          event.preventDefault();
          go(index - 1);
        }
      }}
    >
      <header>
        <p>
          {model.title} · Saved revision {model.revision}
        </p>
        <p>
          {step.phase} · Step {index + 1} of {steps.length}
        </p>
      </header>
      <article className="classroom-stage">
        <h1 tabIndex={-1} ref={heading}>
          {step.title}
        </h1>
        {step.learnerExplanation && (
          <p className="preserve-lines">{step.learnerExplanation}</p>
        )}
        {!step.learnerExplanation && !reference && (
          <p>
            {step.phase === "Recap" || step.title.toLowerCase() === "conclusion"
              ? "What did you learn today? Explain one idea in your own words and share an example."
              : "Listen, discuss and explore together."}
          </p>
        )}
        {hint && (
          <p className="classroom-reveal" role="status">
            Hint: {step.hint}
          </p>
        )}
        {answer && (
          <p className="classroom-reveal" role="status">
            Answer: {step.answer}
          </p>
        )}
        {reference && (
          <PinnedResource
            key={`${index}:${reference.assetId}`}
            reference={reference}
            wholeClass
          />
        )}
      </article>
      <nav className="classroom-controls" aria-label="Classroom controls">
        <button
          className="button secondary"
          disabled={index === 0}
          onClick={() => go(index - 1)}
        >
          Previous step
        </button>
        <button
          className="button"
          disabled={index === steps.length - 1}
          onClick={() => go(index + 1)}
        >
          Next step
        </button>
        <button
          className="button secondary"
          aria-expanded={overview}
          onClick={() => setOverview(!overview)}
        >
          Lesson overview
        </button>
        {step.hint && (
          <button className="button secondary" onClick={() => setHint(!hint)}>
            {hint ? "Hide hint" : "Show hint"}
          </button>
        )}
        {step.answer && (
          <button
            className="button secondary"
            onClick={() => setAnswer(!answer)}
          >
            {answer ? "Hide answer" : "Reveal answer"}
          </button>
        )}
        {step.resources.length > 0 && (
          <label>
            Teaching resource
            <select value={asset} onChange={(e) => setAsset(e.target.value)}>
              <option value="">Choose a resource…</option>
              {step.resources.map((r) => (
                <option key={r.assetId} value={r.assetId}>
                  {r.title}
                </option>
              ))}
            </select>
          </label>
        )}
        <StepTimer key={step.id} minutes={step.minutes} />
        <button
          className="button secondary"
          onClick={() =>
            void (async () => {
              try {
                if (document.fullscreenElement) await document.exitFullscreen();
                else if (root.current?.requestFullscreen)
                  await root.current.requestFullscreen();
                else
                  setNotice(
                    "Fullscreen is unavailable. Continue presenting in this browser.",
                  );
              } catch {
                setNotice(
                  "Fullscreen could not start. Continue presenting in this browser.",
                );
              }
            })()
          }
        >
          {full ? "Exit fullscreen" : "Enter fullscreen"}
        </button>
        <Link
          className="button secondary"
          to={`/app/lessons/${model.lessonId}/journey?revision=${model.revision}`}
        >
          Exit to preparation
        </Link>
        <Link className="button secondary" to="/app/lessons" onClick={onFinish}>
          Finish lesson
        </Link>
      </nav>
      {notice && <p role="status">{notice}</p>}
      {resume && (
        <p role="status">
          Resumed your saved teaching step. Answers are hidden. Interactive
          courses reopen from the beginning.
        </p>
      )}
      {overview && (
        <nav aria-label="Jump to teaching step" className="classroom-overview">
          {steps.map((s, i) => (
            <button
              className="button secondary"
              key={s.id}
              aria-current={i === index ? "step" : undefined}
              onClick={() => go(i)}
            >
              {i + 1}. {s.title}
            </button>
          ))}
        </nav>
      )}
    </section>
  );
}
function StepTimer({ minutes }: { minutes: number }) {
  const [running, setRunning] = useState(false);
  const [seconds, setSeconds] = useState(0);
  const started = useRef(0);
  useEffect(() => {
    if (!running) return;
    const timer = setInterval(
      () => setSeconds(Math.floor((Date.now() - started.current) / 1000)),
      1000,
    );
    return () => clearInterval(timer);
  }, [running]);
  return (
    <span className="classroom-timer">
      <output aria-label="Step timer">
        {Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, "0")} /{" "}
        {minutes} min
      </output>
      <button
        className="button secondary"
        onClick={() => {
          started.current = Date.now() - seconds * 1000;
          setRunning(!running);
        }}
      >
        {running ? "Pause timer" : "Start timer"}
      </button>
      <button
        className="button secondary"
        onClick={() => {
          setRunning(false);
          setSeconds(0);
        }}
      >
        Reset timer
      </button>
    </span>
  );
}
