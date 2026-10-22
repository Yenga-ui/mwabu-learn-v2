import { Brand } from "../components/Brand";
import { useEffect, useState } from "react";
import {
  Link,
  Navigate,
  NavLink,
  Outlet,
  useLocation,
  useNavigate,
} from "react-router-dom";
import {
  BookOpen,
  Building2,
  ChartNoAxesCombined,
  ChevronRight,
  Files,
  GraduationCap,
  Heart,
  LayoutDashboard,
  LogOut,
  Menu,
  Monitor,
  Search,
  Settings,
  ShieldCheck,
  Tags,
  Users,
  X,
  Layers,
  FolderKanban,
  PanelLeftClose,
  PanelLeftOpen,
  FilePlus,
  Sparkles,
} from "lucide-react";
import { useSession } from "../auth/Session";
import { api, ApiError, sessionChanged } from "../api/client";
import { Action, ErrorNotice, Loading } from "../components/ui";
import type { Scope } from "../auth/permissions";

export function RouteFailure() {
  return (
    <main className="standalone">
      <h1>This page is unavailable</h1>
      <p>The address may have changed, or an unexpected error occurred.</p>
      <Link className="button" to="/app">
        Return to your workspace
      </Link>
    </main>
  );
}
export function Shell() {
  const navigate = useNavigate();
  const session = useSession();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  useEffect(() => {
    if (open)
      document
        .querySelector<HTMLAnchorElement>("#workspace-navigation a")
        ?.focus();
  }, [open]);
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem("mwabu.navigation.collapsed") === "true";
    } catch {
      return false;
    }
  });
  const secondaryTeacherLinks = [
    "/app/assessments",
    "/app/collections",
    "/app/organisations",
    "/app/account",
  ];
  const teacherNavigation =
    session.access?.roleCodes.includes("Teacher") &&
    !session.access.platformAuthority;
  const platformNavigation = !!session.access?.platformAuthority;
  const organisationNavigation =
    !platformNavigation &&
    !!session.access?.roleCodes.includes("OrganisationAdmin");
  const learnerNavigation =
    !platformNavigation &&
    !organisationNavigation &&
    !teacherNavigation &&
    !!session.access?.roleCodes.includes("Learner");
  const homeLabel = platformNavigation
    ? "Platform overview"
    : organisationNavigation
      ? "Organisation overview"
      : learnerNavigation
        ? "My learning"
        : teacherNavigation
          ? "Today"
          : "Your workspace";
  const primaryRoutes = platformNavigation
    ? [
        "/app",
        "/app/users",
        "/app/organisations",
        "/app/content",
        "/app/curriculum",
        "/app/role-permissions",
        "/app/reports",
      ]
    : organisationNavigation
      ? [
          "/app",
          "/app/organisations",
          "/app/role-permissions",
          "/app/reports",
          "/app/resources",
          "/app/curriculum",
        ]
      : learnerNavigation
        ? [
            "/app",
            "/app/resources",
            "/app/library",
            "/app/assessments",
            "/app/collections",
            "/app/curriculum",
          ]
        : undefined;
  if (session.loading) return <Loading />;
  if (
    !session.user &&
    (!session.error ||
      (session.error instanceof ApiError && session.error.status === 401))
  )
    return (
      <Navigate
        replace
        to={
          "/login?return=" +
          encodeURIComponent(location.pathname + location.search)
        }
      />
    );
  if (session.error)
    return (
      <main className="standalone">
        <ErrorNotice error={session.error} retry={session.retry} />
      </main>
    );
  const links: {
    to: string;
    label: string;
    icon: typeof BookOpen;
    permission?: string;
    scope?: Scope;
    show?: boolean;
  }[] = [
    {
      to: "/app",
      label: homeLabel,
      icon: LayoutDashboard,
    },
    {
      to: "/app/lessons/new?planning=topics",
      label: "Create lesson",
      icon: FilePlus,
      permission: "lessons.manage",
    },
    {
      to: "/app/lessons/new?planning=topics&assist=ai",
      label: "Create with AI",
      icon: Sparkles,
      show: session.can("lessons.manage") && session.can("lessons.ai"),
    },
    {
      to: "/app/role-permissions",
      label: "Roles and permissions",
      icon: ShieldCheck,
      show:
        !!session.access?.platformAuthority ||
        session.can("memberships.manage"),
    },
    {
      to: "/app/library",
      label: "Find a lesson",
      icon: BookOpen,
      permission: "content.read",
      scope: "catalogue",
    },
    {
      to: "/app/assessments",
      label: "Learning assessments",
      icon: GraduationCap,
      permission: "assessments.take",
    },
    {
      to: "/app/lessons",
      label: session.access?.roleCodes.includes("Teacher")
        ? "My lesson plans"
        : "Lesson Planner",
      icon: BookOpen,
      permission: "lessons.read",
    },
    {
      to: "/app/resources",
      label: "Learning resources",
      icon: BookOpen,
      permission: "content.read",
      scope: "catalogue",
    },
    {
      to: "/app/curriculum",
      label: session.access?.roleCodes.includes("Teacher")
        ? "Curriculum"
        : "Curriculum explorer",
      icon: GraduationCap,
      permission: "curriculum.read",
      scope: "catalogue",
    },
    {
      to: "/app/collections",
      label: "Resource collections",
      icon: Layers,
      permission: "content.read",
      scope: "catalogue",
    },
    {
      to: "/app/family",
      label: "My learners",
      icon: Heart,
      show: session.access?.roleCodes.includes("ParentGuardian"),
    },
    {
      to: "/app/content",
      label: "Content studio",
      icon: Files,
      permission: "content.manage",
      scope: "global",
    },
    {
      to: "/app/content-imports",
      label: "Content imports",
      icon: Files,
      permission: "content.manage",
      scope: "global",
    },
    {
      to: "/app/tags",
      label: "Tags",
      icon: Tags,
      permission: "content.manage",
      scope: "global",
    },
    {
      to: "/app/organisations",
      label: session.access?.platformAuthority
        ? "Organisations"
        : "School & organisation",
      icon: Building2,
      show:
        session.can("organisations.read", "platform") ||
        session.can("organisations.read"),
    },
    {
      to: "/app/users",
      label: "People",
      icon: Users,
      permission: "users.read",
      scope: "platform",
    },
    {
      to: "/app/projects",
      label: "Projects",
      icon: FolderKanban,
      permission: "projects.read",
    },
    {
      to: "/app/reports",
      label: "Reports",
      icon: ChartNoAxesCombined,
      show:
        session.can("reports.read") || session.can("reports.read", "platform"),
    },
    {
      to: "/app/devices",
      label: "Devices",
      icon: Monitor,
      permission: "reports.read",
    },
    {
      to: "/app/audit",
      label: "Audit trail",
      icon: ShieldCheck,
      permission: "users.read",
      scope: "platform",
    },
    { to: "/app/account", label: "Your account", icon: Settings },
  ];
  const isSecondary = (to: string) =>
    primaryRoutes
      ? !primaryRoutes.includes(to)
      : !!teacherNavigation && secondaryTeacherLinks.includes(to);
  const visibleLinks = links.filter(
    (x) =>
      x.show !== false &&
      (!x.permission || session.can(x.permission, x.scope)) &&
      (!learnerNavigation || x.to !== "/app/account"),
  );
  return (
    <div
      className={
        "app-shell modern-shell" +
        (platformNavigation
          ? " role-platform"
          : organisationNavigation
            ? " role-organisation"
            : learnerNavigation
              ? " role-learner"
              : "") +
        (collapsed ? " sidebar-collapsed" : "")
      }
    >
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <button
        className="mobile-nav icon-button"
        aria-label="Open navigation"
        aria-expanded={open}
        aria-controls="workspace-navigation"
        onClick={() => setOpen(true)}
      >
        <Menu />
      </button>
      {open && (
        <button
          className="nav-scrim"
          aria-label="Close navigation"
          onClick={() => setOpen(false)}
        />
      )}
      <aside
        id="workspace-navigation"
        className={"sidebar " + (open ? "open" : "")}
        onKeyDown={(event) => {
          if (open && event.key === "Tab") {
            const targets = Array.from(
              event.currentTarget.querySelectorAll<HTMLElement>(
                "a[href], button:not(:disabled), select:not(:disabled)",
              ),
            ).filter((x) => x.getClientRects().length > 0);
            const first = targets[0];
            const last = targets.at(-1);
            if (event.shiftKey && document.activeElement === first) {
              event.preventDefault();
              last?.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
              event.preventDefault();
              first?.focus();
            }
          }
          if (event.key === "Escape") {
            setOpen(false);
            document.querySelector<HTMLButtonElement>(".mobile-nav")?.focus();
          }
        }}
      >
        <Link to="/app" className="brand">
          <Brand />
        </Link>
        <button
          className="mobile-close icon-button"
          aria-label="Close navigation"
          onClick={() => setOpen(false)}
        >
          <X />
        </button>
        <p className="nav-label">YOUR WORKSPACE</p>
        <nav aria-label="Main navigation">
          {visibleLinks
            .filter(
              (x) =>
                x.show !== false &&
                !isSecondary(x.to) &&
                (!x.permission || session.can(x.permission, x.scope)),
            )
            .sort((a, b) =>
              primaryRoutes
                ? primaryRoutes.indexOf(a.to) - primaryRoutes.indexOf(b.to)
                : 0,
            )
            .map(({ to, label, icon: Icon }) => (
              <NavLink
                end={to === "/app" || to.startsWith("/app/lessons")}
                key={to}
                to={to}
                onClick={() => setOpen(false)}
                title={label}
                aria-current={
                  to.includes("?") &&
                  new URLSearchParams(to.split("?")[1]).get("assist") !==
                    new URLSearchParams(location.search).get("assist")
                    ? "false"
                    : undefined
                }
                className={({ isActive }) => {
                  const query = to.includes("?")
                    ? new URLSearchParams(to.split("?")[1])
                    : null;
                  const active =
                    isActive &&
                    (!query ||
                      query.get("assist") ===
                        new URLSearchParams(location.search).get("assist"));
                  return active ? "active" : "";
                }}
              >
                <Icon size={19} />
                <span className="nav-text">{label}</span>
              </NavLink>
            ))}
        </nav>
        {visibleLinks.some((x) => isSecondary(x.to)) && (
          <details className="sidebar-extra">
            <summary title="More workspace tools">More tools</summary>
            <nav aria-label="More workspace tools">
              {visibleLinks
                .filter(
                  (x) =>
                    isSecondary(x.to) &&
                    x.show !== false &&
                    (!x.permission || session.can(x.permission, x.scope)),
                )
                .map(({ to, label, icon: Icon }) => (
                  <NavLink
                    key={to}
                    to={to}
                    title={label}
                    onClick={() => setOpen(false)}
                  >
                    <Icon size={18} />
                    <span className="nav-text">{label}</span>
                  </NavLink>
                ))}
            </nav>
          </details>
        )}
        <div className="sidebar-bottom">
          <button
            className="sidebar-toggle"
            onClick={() => {
              setCollapsed(!collapsed);
              try {
                localStorage.setItem(
                  "mwabu.navigation.collapsed",
                  String(!collapsed),
                );
              } catch {
                /* Optional browser preference. */
              }
            }}
            aria-label={
              collapsed ? "Show navigation labels" : "Hide navigation labels"
            }
            aria-expanded={!collapsed}
          >
            {collapsed ? (
              <PanelLeftOpen size={18} />
            ) : (
              <PanelLeftClose size={18} />
            )}
            <span className="nav-text">Hide labels</span>
          </button>
          <Link
            className="sidebar-profile"
            to="/app/account"
            title="Your account"
          >
            <span className="avatar">
              {session.user?.firstName[0]}
              {session.user?.lastName[0]}
            </span>
            <span className="nav-text">
              {session.user?.firstName} {session.user?.lastName}
              <small>Your account</small>
            </span>
          </Link>
        </div>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <strong className="topbar-title">
            {location.pathname === "/app"
              ? homeLabel
              : location.pathname.endsWith("/new")
                ? "Create lesson"
                : (links
                    .filter((x) => x.to !== "/app" && !x.to.includes("?"))
                    .find((x) => location.pathname.startsWith(x.to))?.label ??
                  "Lesson workspace")}
          </strong>
          <Link to="/app" className="brand header-brand">
            <Brand />
          </Link>
          <div className="context">
            <label htmlFor="organisation">Your organisation</label>
            <select
              id="organisation"
              value={session.organisationId ?? ""}
              onChange={(e) => session.setOrganisation(e.target.value)}
            >
              {session.memberships.length === 0 && (
                <option value="">No active membership</option>
              )}
              {session.memberships.map((x) => (
                <option
                  key={x.membership.id}
                  value={x.membership.organisationId}
                >
                  {x.organisationName}
                </option>
              ))}
            </select>
          </div>
          <form className="global-search" action="/app/resources">
            <Search size={18} />
            <label className="sr-only" htmlFor="global-search">
              Search learning resources
            </label>
            <input
              id="global-search"
              name="text"
              placeholder="Find a lesson or resource…"
              maxLength={200}
            />
          </form>
          <Link className="profile" to="/app/account">
            <span className="avatar">
              {session.user?.firstName[0]}
              {session.user?.lastName[0]}
            </span>
            <span>
              {session.user?.firstName}
              <small>Your account</small>
            </span>
          </Link>
          <Action
            className="icon-button"
            confirm="Sign out of this browser? Your session will be revoked."
            run={async () => {
              await api("/api/browser/session/logout", {
                method: "POST",
                retryAuth: false,
              });
              sessionChanged();
              navigate("/login", { replace: true });
            }}
          >
            <LogOut size={19} />
            <span className="sr-only">Sign out</span>
          </Action>
        </header>
        <main id="main" tabIndex={-1}>
          <nav className="breadcrumbs" aria-label="Breadcrumb">
            <Link to="/app">Workspace</Link>
            {location.pathname !== "/app" && (
              <>
                <ChevronRight size={14} />
                <span>
                  {links.find(
                    (x) =>
                      location.pathname.startsWith(x.to) && x.to !== "/app",
                  )?.label ?? "Details"}
                </span>
              </>
            )}
          </nav>
          <Outlet />
        </main>
        <footer className="footer">
          Mwabu Learn · A little discovery, every day.
        </footer>
      </div>
    </div>
  );
}
