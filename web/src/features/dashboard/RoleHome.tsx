import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  BookOpen,
  Building2,
  Users,
  ShieldCheck,
  Files,
  GraduationCap,
  ChartNoAxesCombined,
  Layers,
  ClipboardCheck,
  ArrowRight,
} from "lucide-react";
import { useSession } from "../../auth/Session";
import { api } from "../../api/client";
import type {
  Content,
  Page,
  Recent,
  Report,
  CurriculumAssignment,
} from "../../api/types";
import { Empty, PageTitle, QueryState } from "../../components/ui";
import { ContentCard } from "../content/Catalogue";
export type WorkspaceKind = "platform" | "organisation" | "learner" | "other";
export function workspaceKind(
  access: { platformAuthority: boolean; roleCodes: string[] } | undefined,
): WorkspaceKind {
  if (access?.platformAuthority) return "platform";
  if (access?.roleCodes.includes("OrganisationAdmin")) return "organisation";
  if (
    access?.roleCodes.includes("Learner") &&
    !access.roleCodes.includes("Teacher")
  )
    return "learner";
  return "other";
}
type Task = {
  title: string;
  description: string;
  to: string;
  icon: typeof BookOpen;
  allowed: boolean;
};
function Tasks({ items, label }: { items: Task[]; label: string }) {
  return (
    <nav className="role-tasks" aria-label={label}>
      {items
        .filter((x) => x.allowed)
        .map(({ title, description, to, icon: Icon }) => (
          <Link className="role-task" key={title} to={to}>
            <span className="role-task-icon">
              <Icon size={21} aria-hidden="true" />
            </span>
            <span>
              <strong>{title}</strong>
              <small>{description}</small>
            </span>
            <ArrowRight size={16} aria-hidden="true" />
          </Link>
        ))}
    </nav>
  );
}
export function AdminHome({ platform }: { platform: boolean }) {
  const session = useSession();
  const org = session.organisationId;
  const organisation = session.memberships.find(
    (x) => x.membership.organisationId === org,
  )?.organisationName;
  const orgPath = org ? `/app/organisations/${org}` : "/app/organisations";
  const report = useQuery({
    queryKey: ["role-home-report", platform, org],
    queryFn: ({ signal }) =>
      api<Report>(
        platform
          ? "/api/reports/platform"
          : `/api/reports/organisations/${org}`,
        { signal },
      ),
    enabled: platform
      ? session.can("reports.read", "platform")
      : !!org && session.can("reports.read"),
  });
  const tasks: Task[] = platform
    ? [
        {
          title: "People & licences",
          description: "Review accounts and activate paid entitlements.",
          to: "/app/users",
          icon: Users,
          allowed: session.can("users.read", "platform"),
        },
        {
          title: "Organisations",
          description: "Manage schools, partners and memberships.",
          to: "/app/organisations",
          icon: Building2,
          allowed: session.can("organisations.read", "platform"),
        },
        {
          title: "Content studio",
          description: "Review resources, publication and guest access.",
          to: "/app/content",
          icon: Files,
          allowed: session.can("content.manage", "global"),
        },
        {
          title: "Curriculum",
          description: "Maintain frameworks and learning relationships.",
          to: "/app/curriculum",
          icon: GraduationCap,
          allowed: session.can("curriculum.read", "catalogue"),
        },
        {
          title: "Roles and permissions",
          description: "Inspect access across platform roles.",
          to: "/app/role-permissions",
          icon: ShieldCheck,
          allowed: !!session.access?.platformAuthority,
        },
        {
          title: "Reports",
          description: "View operational indicators and platform activity.",
          to: "/app/reports",
          icon: ChartNoAxesCombined,
          allowed: session.can("reports.read", "platform"),
        },
      ]
    : [
        {
          title: "People in your organisation",
          description: "Manage members and their assigned roles.",
          to: orgPath + "#members",
          icon: Users,
          allowed: !!org && session.can("organisations.read"),
        },
        {
          title: "Organisation profile",
          description: "Review your school or organisation details.",
          to: orgPath,
          icon: Building2,
          allowed: !!org && session.can("organisations.read"),
        },
        {
          title: "Assigned curriculum",
          description: "Review and assign the appropriate curriculum.",
          to: orgPath + "#assigned-curriculum",
          icon: GraduationCap,
          allowed: !!org && session.can("organisations.read"),
        },
        {
          title: "Guardian relationships",
          description: "Manage verified parent and learner links.",
          to: orgPath + "#guardian-relationships",
          icon: ShieldCheck,
          allowed: !!org && session.can("memberships.manage"),
        },
        {
          title: "Learning resources",
          description: "Explore published teaching and learning material.",
          to: "/app/resources",
          icon: BookOpen,
          allowed: session.can("content.read", "catalogue"),
        },
        {
          title: "Organisation reports",
          description: "View indicators for the selected organisation.",
          to: "/app/reports",
          icon: ChartNoAxesCombined,
          allowed: !!org && session.can("reports.read"),
        },
      ];
  return (
    <div className="role-home">
      <PageTitle
        eyebrow={
          platform ? "PLATFORM ADMINISTRATION" : "ORGANISATION ADMINISTRATION"
        }
        title={
          platform
            ? "Your platform, at a glance"
            : "Your organisation workspace"
        }
      >
        {platform
          ? "Manage access, support your organisations and keep learning content ready."
          : (organisation ??
            "Choose an organisation to manage its people and learning.")}
      </PageTitle>
      <Tasks
        items={tasks}
        label={
          platform
            ? "Platform administration tasks"
            : "Organisation administration tasks"
        }
      />
      {report.isEnabled && (
        <section className="role-section">
          <div className="section-heading">
            <h2>{platform ? "Platform overview" : "Organisation overview"}</h2>
            <Link to="/app/reports">View reports →</Link>
          </div>
          <QueryState query={report}>
            {(data) => (
              <>
                <dl className="role-metrics">
                  {[
                    ["People", data.users],
                    ["Active memberships", data.activeMemberships],
                    ["Projects", data.projects],
                    [
                      "Published catalogue resources",
                      data.publicationStates.find((x) => x.code === "Published")
                        ?.count ?? 0,
                    ],
                  ].map(([label, value]) => (
                    <div key={label}>
                      <dt>{label}</dt>
                      <dd>{value}</dd>
                    </div>
                  ))}
                </dl>
                <p className="role-caption">
                  Operational snapshots, not learner achievement. Catalogue
                  totals refer to shared published material.
                </p>
              </>
            )}
          </QueryState>
        </section>
      )}
      <section className="role-section">
        <h2>{platform ? "More platform tools" : "Organisation tools"}</h2>
        <div className="role-tool-links">
          {[
            {
              title: "Projects",
              to: "/app/projects",
              allowed: !!org && session.can("projects.read"),
            },
            {
              title: "Devices",
              to: "/app/devices",
              allowed: !!org && session.can("reports.read"),
            },
            {
              title: "Audit trail",
              to: "/app/audit",
              allowed: platform && session.can("users.read", "platform"),
            },
            {
              title: "Content imports",
              to: "/app/content-imports",
              allowed: session.can("content.manage", "global"),
            },
            { title: "Your account", to: "/app/account", allowed: true },
          ]
            .filter((x) => x.allowed)
            .map((x) => (
              <Link key={x.to} to={x.to}>
                {x.title}
                <ArrowRight size={16} />
              </Link>
            ))}
        </div>
      </section>
      {!org && !platform && (
        <Empty title="Choose an organisation">
          Use the organisation selector above to open your authorised workspace.
        </Empty>
      )}
    </div>
  );
}
export function LearnerHome() {
  const session = useSession();
  const org = session.organisationId;
  const resources = useQuery({
    queryKey: ["learner-home-resources", org],
    queryFn: ({ signal }) =>
      api<Page<Content>>(
        "/api/learning/content?pageSize=4&newestPublished=true",
        { signal },
      ),
    enabled: session.can("content.read", "catalogue"),
  });
  const recent = useQuery({
    queryKey: ["learner-home-recent", org],
    queryFn: ({ signal }) =>
      api<Recent[]>(`/api/organisations/${org}/workspace/recent`, { signal }),
    enabled: !!org && session.can("content.read", "catalogue"),
  });
  const curriculum = useQuery({
    queryKey: ["learner-home-curricula", org],
    queryFn: ({ signal }) =>
      api<CurriculumAssignment[]>(
        `/api/organisations/${org}/workspace/curricula`,
        { signal },
      ),
    enabled: !!org && session.can("curriculum.read", "catalogue"),
  });
  return (
    <div className="role-home learner-home">
      <PageTitle
        eyebrow={`WELCOME, ${session.user?.firstName.toUpperCase()}`}
        title="What will you discover today?"
      >
        Choose something to explore, practise what you know or reopen a
        resource.
      </PageTitle>
      <Tasks
        label="Learning tasks"
        items={[
          {
            title: "Explore learning resources",
            description: "Open activities, stories, videos and more.",
            to: "/app/resources",
            icon: BookOpen,
            allowed: session.can("content.read", "catalogue"),
          },
          {
            title: "My assessments",
            description: "Take an assessment and see your saved results.",
            to: "/app/assessments",
            icon: ClipboardCheck,
            allowed: !!org && session.can("assessments.take"),
          },
          {
            title: "Resource collections",
            description: "Discover resources grouped together.",
            to: "/app/collections",
            icon: Layers,
            allowed: session.can("content.read", "catalogue"),
          },
        ]}
      />
      {recent.isEnabled && (
        <section className="role-section">
          <h2>Recently opened</h2>
          <p className="role-caption">
            Reopens the resource from its beginning; this is not a saved
            activity position.
          </p>
          <QueryState query={recent}>
            {(items) =>
              items.length ? (
                <div className="role-tool-links">
                  {items.map((x) => (
                    <Link key={x.id} to={`/app/resources/${x.id}`}>
                      <BookOpen size={18} />
                      {x.title}
                      <ArrowRight size={16} />
                    </Link>
                  ))}
                </div>
              ) : (
                <p>
                  Your recently opened resources will appear here after you
                  explore.
                </p>
              )
            }
          </QueryState>
        </section>
      )}
      {resources.isEnabled && (
        <section className="role-section">
          <div className="section-heading">
            <h2>Explore learning resources</h2>
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
                <Empty title="No resources available yet">
                  Your school will make approved learning material available
                  here.
                </Empty>
              )
            }
          </QueryState>
        </section>
      )}
      {curriculum.isEnabled && (
        <section className="role-section">
          <h2>Your curriculum</h2>
          <QueryState query={curriculum}>
            {(items) => (
              <div className="role-tool-links">
                {items
                  .filter((x) => x.isActive)
                  .map((x) => (
                    <Link
                      key={x.id}
                      to={`/app/curriculum?curriculumId=${x.curriculumId}`}
                    >
                      <GraduationCap size={18} />
                      {x.name}
                    </Link>
                  ))}
                {!items.some((x) => x.isActive) && (
                  <p>Your school has not assigned a curriculum yet.</p>
                )}
              </div>
            )}
          </QueryState>
        </section>
      )}
      {!org && (
        <Empty title="Your account needs an organisation">
          Ask your parent, teacher or Mwabu to arrange your learning access.
        </Empty>
      )}
    </div>
  );
}
