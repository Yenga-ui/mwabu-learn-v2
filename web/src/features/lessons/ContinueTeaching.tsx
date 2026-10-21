import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useSession } from "../../auth/Session";
import { api } from "../../api/client";
import { lessonBase } from "./types";
import type { ClassroomLesson } from "./ClassroomTeaching";
import { readTeachingBookmark } from "./teachingBookmark";

export default function ContinueTeaching() {
  const session = useSession();
  const org = session.organisationId;
  const user = session.user?.id;
  const position = org && user ? readTeachingBookmark(org, user) : undefined;
  const query = useQuery({
    queryKey: [
      "continue-teaching",
      org,
      user,
      position?.lessonId,
      position?.revision,
    ],
    queryFn: ({ signal }) =>
      api<ClassroomLesson>(
        `${lessonBase(org!)}/${position!.lessonId}/classroom?revision=${position!.revision}`,
        { signal },
      ),
    enabled: !!position && session.can("lessons.read"),
    staleTime: 0,
    retry: false,
  });
  // Do not show cached titles until the pinned revision has been reauthorised.
  const lesson =
    session.can("lessons.read") && !query.isFetching && query.isSuccess
      ? query.data
      : undefined;
  const step = lesson?.steps.find((item) => item.id === position?.stepId);
  return (
    <section className="card" aria-label="Continue teaching">
      <h2>Continue teaching</h2>
      {lesson && step ? (
        <>
          <Link
            className="button"
            to={`/app/lessons/${lesson.lessonId}/teach?revision=${lesson.revision}&resume=true`}
          >
            Continue teaching: {lesson.title}
          </Link>
          <p>
            Saved revision {lesson.revision} · {step.title}
          </p>
          <p>
            Your position is kept only in this browser session. Interactive
            courses reopen from the beginning; answers remain hidden.
          </p>
        </>
      ) : (
        <p>
          {query.isFetching
            ? "Checking your saved teaching position…"
            : query.isError
              ? "Your saved lesson is unavailable. Choose an authorised lesson from My lesson plans."
              : "Open Teach Lesson to keep your place in this browser session."}
        </p>
      )}
    </section>
  );
}
