import { useState } from "react";
import { Link, Navigate, NavLink, Outlet, useLocation } from "react-router-dom";
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
  const session = useSession();
  const location = useLocation();
  const [open, setOpen] = useState(false);
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
    { to: "/app", label: "Your workspace", icon: LayoutDashboard },
    {
      to: "/app/resources",
      label: "Learning resources",
      icon: BookOpen,
      permission: "content.read",
      scope: "catalogue",
    },
    {
      to: "/app/curriculum",
      label: "Curriculum explorer",
      icon: GraduationCap,
      permission: "curriculum.read",
      scope: "catalogue",
    },
    {
      to: "/app/collections",
      label: "Collections",
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
  return (
    <div className="app-shell">
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
          if (event.key === "Escape") {
            setOpen(false);
            document.querySelector<HTMLButtonElement>(".mobile-nav")?.focus();
          }
        }}
      >
        <Link to="/app" className="brand">
          <span className="brand-mark">m</span>
          <span>
            mwabu<span className="brand-sub">LEARN</span>
          </span>
        </Link>
        <button
          className="mobile-close icon-button"
          aria-label="Close navigation"
          onClick={() => setOpen(false)}
        >
          <X />
        </button>
        <p className="nav-label">LEARN. TEACH. GROW.</p>
        <nav aria-label="Main navigation">
          {links
            .filter(
              (x) =>
                x.show !== false &&
                (!x.permission || session.can(x.permission, x.scope)),
            )
            .map(({ to, label, icon: Icon }) => (
              <NavLink
                end={to === "/app"}
                key={to}
                to={to}
                onClick={() => setOpen(false)}
              >
                <Icon size={19} />
                {label}
              </NavLink>
            ))}
        </nav>
        <div className="sidebar-note">
          <span className="status-dot" />
          Live web workspace<small>Learning that travels with you.</small>
        </div>
      </aside>
      <div className="workspace">
        <header className="topbar">
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
