import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Heart } from "lucide-react";
import { api } from "../../api/client";
import type {
  LinkedLearner,
  Page,
  CurriculumAssignment,
} from "../../api/types";
import { Empty, PageTitle, Pagination, QueryState } from "../../components/ui";
export default function Parents() {
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["linked-learners", page],
    queryFn: () =>
      api<Page<LinkedLearner>>(
        `/api/guardians/me/learners?page=${page}&pageSize=20`,
      ),
  });
  return (
    <>
      <PageTitle
        eyebrow="YOUR FAMILY'S LEARNING"
        title="Stay close to their discoveries."
      >
        Your school manages these relationships. Only learners linked to your
        active account appear here.
      </PageTitle>
      <QueryState query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="node-grid">
                {data.items.map((x) => (
                  <LearnerCard key={x.linkId} learner={x} />
                ))}
              </div>
            ) : (
              <Empty title="No linked learners yet">
                Contact your learner's school to arrange a verified guardian
                relationship.
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
    </>
  );
}
function LearnerCard({ learner }: { learner: LinkedLearner }) {
  const query = useQuery({
    queryKey: ["learner-curriculum", learner.linkId],
    queryFn: () =>
      api<CurriculumAssignment[]>(
        `/api/guardians/me/learners/${learner.linkId}/curricula`,
      ),
  });
  return (
    <section className="card learner-card">
      <Heart size={27} />
      <h2>
        {learner.firstName} {learner.lastName}
      </h2>
      <p>{learner.organisationName}</p>
      <h3>Learning context</h3>
      <QueryState query={query}>
        {(items) =>
          items.length ? (
            items.map((x) => (
              <p key={x.id}>
                <Link
                  to={
                    "/app/resources?curriculumVersionId=" +
                    x.curriculumVersionId
                  }
                >
                  {x.name} — explore learning resources →
                </Link>
              </p>
            ))
          ) : (
            <p className="muted">
              The school has not assigned a curriculum yet.
            </p>
          )
        }
      </QueryState>
      <Link className="button secondary" to="/app/resources">
        Discover resources together
      </Link>
    </section>
  );
}
