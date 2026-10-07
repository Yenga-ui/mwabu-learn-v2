import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowUpRight,
  BookOpen,
  Compass,
  FileText,
  GraduationCap,
  Users,
} from "lucide-react";
import { api } from "../../api/client";
import type {
  Content,
  CurriculumAssignment,
  Page,
  Recent,
  Report,
  Project,
} from "../../api/types";
import { useSession } from "../../auth/Session";
import { Empty, PageTitle, QueryState } from "../../components/ui";
import { ContentCard } from "../content/Catalogue";
export default function Dashboard() {
  const session = useSession();
  const org = session.organisationId;
  const roles = session.access?.roleCodes ?? [];
  const teacher = roles.includes("Teacher");
  const learner = roles.includes("Learner");
  const head = roles.includes("HeadTeacher");
  const parent = roles.includes("ParentGuardian");
  const resources = useQuery({
    queryKey: ["home-resources"],
    queryFn: () =>
      api<Page<Content>>(
        "/api/learning/content?pageSize=4&newestPublished=true",
      ),
    enabled: session.can("content.read", "catalogue"),
  });
  const curricula = useQuery({
    queryKey: ["assigned-curricula", org],
    queryFn: () =>
      api<CurriculumAssignment[]>(
        `/api/organisations/${org}/workspace/curricula`,
      ),
    enabled: !!org && session.can("curriculum.read", "catalogue"),
  });
  const recents = useQuery({
    queryKey: ["recent-resources", org],
    queryFn: () => api<Recent[]>(`/api/organisations/${org}/workspace/recent`),
    enabled: !!org && (teacher || learner),
  });
  const report = useQuery({
    queryKey: ["home-report", org],
    queryFn: () =>
      api<Report>(
        session.can("reports.read", "platform")
          ? "/api/reports/platform"
          : `/api/reports/organisations/${org}`,
      ),
    enabled:
      session.can("reports.read", "platform") ||
      (!!org && session.can("reports.read")),
  });
  const projects = useQuery({
    queryKey: ["home-projects", org],
    queryFn: () =>
      api<Page<Project>>(`/api/organisations/${org}/projects?pageSize=4`),
    enabled: !!org && session.can("projects.read"),
  });
  const greeting = learner
    ? "What will you discover today?"
    : teacher
      ? "A little inspiration for your next lesson."
      : head
        ? "A clear view of your school day."
        : parent
          ? "Stay connected to their learning."
          : session.can("content.manage", "global") &&
              !session.access?.platformAuthority
            ? "Make something worth learning."
            : roles.includes("DataAnalyst")
              ? "From real data to useful insight."
              : roles.includes("ProjectManager")
                ? "Keep your programme moving."
                : "Your learning community, connected.";
  return (
    <>
      <PageTitle
        eyebrow={"WELCOME, " + session.user?.firstName.toUpperCase()}
        title={greeting}
      >
        {session.memberships.find((x) => x.membership.organisationId === org)
          ?.organisationName ?? "Your Mwabu Learn workspace"}
      </PageTitle>
      {session.can("content.read", "catalogue") && (
        <section className="hero">
          <div>
            <span className="hero-label">
              {teacher
                ? "LESS TIME SEARCHING. MORE TIME TEACHING."
                : "EVERY DAY IS A CHANCE TO DISCOVER."}
            </span>
            <h2>
              {teacher
                ? "Your next great lesson starts with a good resource."
                : "Open a resource. Start a new possibility."}
            </h2>
            <p>
              Find lessons and activities connected to the way you learn and
              teach.
            </p>
            <Link className="button light" to="/app/resources">
              Explore resources
              <ArrowUpRight size={17} />
            </Link>
          </div>
          <div className="hero-art" aria-hidden="true">
            <BookOpen size={126} strokeWidth={1} />
            <span className="hero-sun" />
            <span className="hero-sprout" />
          </div>
        </section>
      )}
      {(teacher || learner || head) && (
        <section>
          <div className="section-heading">
            <h2>Your learning path</h2>
            <Link to="/app/curriculum">Explore curriculum →</Link>
          </div>
          <div className="quick-links">
            <Link to="/app/curriculum">
              <GraduationCap />
              <strong>Grades & subjects</strong>
              <span>Follow your curriculum</span>
            </Link>
            {teacher && (
              <Link to="/app/resources?contentType=lesson-plan">
                <FileText />
                <strong>Lesson plans</strong>
                <span>Curated plans, ready to use</span>
              </Link>
            )}
            <Link to="/app/collections">
              <Compass />
              <strong>Resource collections</strong>
              <span>Discover something useful</span>
            </Link>
            {head && (
              <Link to={"/app/organisations/" + org}>
                <Users />
                <strong>Teachers & learners</strong>
                <span>Your school community</span>
              </Link>
            )}
          </div>
          {curricula.isEnabled && (
            <QueryState query={curricula}>
              {(items) =>
                items.filter((x) => x.isActive).length ? (
                  <div className="assignment-cards">
                    {items
                      .filter((x) => x.isActive)
                      .map((x) => (
                        <Link
                          className="card"
                          key={x.id}
                          to={"/app/curriculum?curriculumId=" + x.curriculumId}
                        >
                          <GraduationCap size={22} />
                          <strong>{x.name}</strong>
                          <span>Assigned by your organisation →</span>
                        </Link>
                      ))}
                  </div>
                ) : (
                  <p className="muted">
                    Your organisation has not assigned a curriculum yet. You can
                    explore available curricula.
                  </p>
                )
              }
            </QueryState>
          )}
        </section>
      )}
      {parent && (
        <section className="card parent-banner">
          <h2>A window into their learning</h2>
          <p>
            Explore the curriculum and resources for learners linked to your
            account by their school.
          </p>
          <Link className="button" to="/app/family">
            View my learners
          </Link>
        </section>
      )}
      {report.isEnabled && (
        <section>
          <div className="section-heading">
            <h2>
              {session.access?.platformAuthority
                ? "Platform overview"
                : "Organisation overview"}
            </h2>
            <Link to="/app/reports">View reports →</Link>
          </div>
          <QueryState query={report}>
            {(data) => (
              <div className="stat-grid">
                {[
                  ["People", data.users],
                  ["Active memberships", data.activeMemberships],
                  ["Projects", data.projects],
                  [
                    "Published resources",
                    data.publicationStates.find((x) => x.code === "Published")
                      ?.count ?? 0,
                  ],
                ].map(([label, value]) => (
                  <div className="stat card" key={label}>
                    <span>{label}</span>
                    <strong>{value}</strong>
                  </div>
                ))}
              </div>
            )}
          </QueryState>
        </section>
      )}
      {session.can("content.manage", "global") && (
        <section className="card studio-banner">
          <div>
            <h2>Your content studio</h2>
            <p>
              Create resources, organise files, connect curriculum and guide
              content through review.
            </p>
          </div>
          <Link className="button" to="/app/content">
            Open content studio →
          </Link>
        </section>
      )}
      {session.can("organisations.manage") && !head && (
        <div className="quick-links">
          <Link to={"/app/organisations/" + org}>
            <Users />
            <strong>Manage your organisation</strong>
            <span>Members, roles and curriculum</span>
          </Link>
          {session.access?.platformAuthority && (
            <>
              <Link to="/app/organisations">
                <GraduationCap />
                <strong>Organisations</strong>
                <span>Manage the platform network</span>
              </Link>
              <Link to="/app/users">
                <Users />
                <strong>People</strong>
                <span>Provision and manage accounts</span>
              </Link>
            </>
          )}
        </div>
      )}
      {projects.isEnabled && (
        <section>
          <div className="section-heading">
            <h2>Your projects</h2>
            <Link to="/app/projects">View projects →</Link>
          </div>
          <QueryState query={projects}>
            {(data) =>
              data.items.length ? (
                <div className="node-grid">
                  {data.items.map((x) => (
                    <Link
                      key={x.id}
                      className="card"
                      to={"/app/projects/" + x.id}
                    >
                      <h3>{x.name}</h3>
                      <p>{x.description}</p>
                      <span>{x.status}</span>
                    </Link>
                  ))}
                </div>
              ) : (
                <Empty title="No projects in this organisation yet" />
              )
            }
          </QueryState>
        </section>
      )}
      {recents.isEnabled && (
        <QueryState query={recents}>
          {(items) =>
            items.length ? (
              <section>
                <h2>Pick up where you left off</h2>
                <div className="recent-grid">
                  {items.map((x) => (
                    <Link
                      className="card"
                      key={x.id}
                      to={"/app/resources/" + x.id}
                    >
                      <BookOpen size={22} />
                      <strong>{x.title}</strong>
                      <small>
                        {new Date(x.lastOpenedAt).toLocaleDateString()}
                      </small>
                    </Link>
                  ))}
                </div>
              </section>
            ) : null
          }
        </QueryState>
      )}
      {resources.isEnabled && (
        <section>
          <div className="section-heading">
            <h2>
              {teacher
                ? "Ready for your classroom"
                : "Explore learning resources"}
            </h2>
            <Link to="/app/resources">Browse all →</Link>
          </div>
          <QueryState query={resources}>
            {(data) =>
              data.items.length ? (
                <div className="content-grid">
                  {data.items.map((x) => (
                    <ContentCard key={x.id} content={x} />
                  ))}
                </div>
              ) : (
                <Empty title="Your next discovery is on its way">
                  Published resources will appear here when they are ready.
                </Empty>
              )
            }
          </QueryState>
        </section>
      )}
      {!org && (
        <Empty title="Your account needs an organisation">
          Ask your administrator to add an active membership and role.
        </Empty>
      )}
    </>
  );
}
