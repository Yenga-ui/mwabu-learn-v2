import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import { QueryState } from "../../components/ui";
import TeachingIcon from "../../components/TeachingIcon";

export interface TeachingContext {
  year: string;
  grade: string;
  subject: string;
  topic: string;
  term: string;
  subTopic?: string;
}
interface Choice {
  year: number;
  grade: number;
  subject: string;
  topic: string;
  term: string | null;
  subTopic?: string | null;
}

export default function TeachingContextPicker({
  value,
  change,
  disabled = false,
}: {
  value: TeachingContext;
  change: (context: TeachingContext) => void;
  disabled?: boolean;
}) {
  const query = useQuery({
    queryKey: ["lesson-creation-contexts"],
    queryFn: ({ signal }) =>
      api<{ choices: Choice[] }>("/api/learning/library", { signal }),
  });
  const select = (key: keyof TeachingContext, item: string) => {
    const next = { ...value, [key]: item };
    const order: (keyof TeachingContext)[] = [
      "year",
      "grade",
      "subject",
      "term",
      "topic",
      "subTopic",
    ];
    for (const child of order.slice(order.indexOf(key) + 1)) next[child] = "";
    change(next);
  };
  const distinct = (items: string[]) =>
    [...new Set(items)]
      .filter(Boolean)
      .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  return (
    <fieldset
      disabled={disabled}
      className="card teaching-choices"
      aria-label="Choose your teaching context"
    >
      <h2>What would you like to teach?</h2>
      <p>
        Choose your grade, subject and topic here. Your choices fill in the plan
        for you.
      </p>
      <QueryState query={query}>
        {({ choices }) => {
          const grades = choices.filter(
            (x) => !value.year || String(x.year) === value.year,
          );
          const subjects = grades.filter(
            (x) => String(x.grade) === value.grade,
          );
          const topics = subjects.filter((x) => x.subject === value.subject);
          const subTopics = distinct(
            topics
              .filter(
                (x) =>
                  x.topic === value.topic &&
                  (!value.term || x.term === value.term),
              )
              .flatMap((x) => (x.subTopic ? [x.subTopic] : [])),
          );
          return (
            <>
              <h3>1. Grade</h3>
              <div className="teaching-choice-grid">
                {distinct(grades.map((x) => String(x.grade))).map((grade) => (
                  <button
                    type="button"
                    className="teaching-choice"
                    aria-pressed={value.grade === grade}
                    key={grade}
                    onClick={() => select("grade", grade)}
                  >
                    <TeachingIcon /> Grade {grade}
                  </button>
                ))}
              </div>
              {value.grade && (
                <>
                  <h3>2. Subject</h3>
                  <div className="teaching-choice-grid">
                    {distinct(subjects.map((x) => x.subject)).map((subject) => (
                      <button
                        type="button"
                        className="teaching-choice"
                        aria-pressed={value.subject === subject}
                        key={subject}
                        onClick={() => select("subject", subject)}
                      >
                        <TeachingIcon subject={subject} /> {subject}
                      </button>
                    ))}
                  </div>
                </>
              )}
              {value.grade && value.subject && (
                <>
                  <h3>3. Topic</h3>
                  <div className="teaching-choice-grid">
                    {distinct(
                      topics
                        .filter((x) => !value.term || x.term === value.term)
                        .map((x) => x.topic),
                    ).map((topic) => (
                      <button
                        type="button"
                        className="teaching-choice"
                        aria-pressed={value.topic === topic}
                        key={topic}
                        onClick={() => select("topic", topic)}
                      >
                        <TeachingIcon subject={value.subject} /> {topic}
                      </button>
                    ))}
                  </div>
                </>
              )}
              {value.topic && subTopics.length > 0 && (
                <>
                  <h3>4. Subtopic (optional)</h3>
                  <div className="teaching-choice-grid">
                    {distinct(
                      topics
                        .filter(
                          (x) =>
                            x.topic === value.topic &&
                            (!value.term || x.term === value.term),
                        )
                        .flatMap((x) => (x.subTopic ? [x.subTopic] : [])),
                    ).map((subTopic) => (
                      <button
                        type="button"
                        className="teaching-choice"
                        aria-pressed={value.subTopic === subTopic}
                        key={subTopic}
                        onClick={() => select("subTopic", subTopic)}
                      >
                        {subTopic}
                      </button>
                    ))}
                  </div>
                </>
              )}
              {!choices.length && (
                <p>
                  No published curriculum topics are available yet. Use your own
                  subject and grade below, or select an official learning
                  outcome.
                </p>
              )}
              <details>
                <summary>Curriculum version and term</summary>
                <label>
                  Curriculum collection
                  <select
                    value={value.year}
                    onChange={(e) => select("year", e.target.value)}
                  >
                    <option value="">All available collections</option>
                    {distinct(choices.map((x) => String(x.year))).map(
                      (year) => (
                        <option key={year} value={year}>
                          {year} Zambian curriculum
                        </option>
                      ),
                    )}
                  </select>
                </label>
                <label>
                  Term
                  <select
                    value={value.term}
                    onChange={(e) => select("term", e.target.value)}
                  >
                    <option value="">Unspecified / all terms</option>
                    {distinct(
                      topics.flatMap((x) => (x.term ? [x.term] : [])),
                    ).map((term) => (
                      <option key={term}>{term}</option>
                    ))}
                  </select>
                </label>
              </details>
            </>
          );
        }}
      </QueryState>
      <p className="muted">
        These are topics from published Mwabu plans. Official learning-outcome
        alignment is selected separately.
      </p>
    </fieldset>
  );
}
