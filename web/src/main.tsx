import React, { Suspense, lazy } from "react";
import ReactDOM from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { SessionProvider } from "./auth/Session";
import { Shell, RouteFailure } from "./app/Shell";
import { Loading, ToastRegion } from "./components/ui";
import { ApiError } from "./api/client";
import "./app/styles.css";
const Auth = lazy(() => import("./auth/AuthPages"));
const Dashboard = lazy(() => import("./features/dashboard/Dashboard"));
const Catalogue = lazy(() => import("./features/content/Catalogue"));
const Resource = lazy(() => import("./features/content/Resource"));
const Curriculum = lazy(() => import("./features/curriculum/Curriculum"));
const ContentManagement = lazy(() => import("./features/content/Management"));
const Organisations = lazy(
  () => import("./features/organisations/Organisations"),
);
const Users = lazy(() => import("./features/users/Users"));
const Projects = lazy(() => import("./features/projects/Projects"));
const Parents = lazy(() => import("./features/parents/Parents"));
const Reports = lazy(() => import("./features/reports/Reports"));
const Devices = lazy(() => import("./features/devices/Devices"));
const Taxonomy = lazy(() => import("./features/content/Taxonomy"));
const Audit = lazy(() => import("./features/reports/Audit"));
const Account = lazy(() => import("./auth/Account"));
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: true,
      retry: (count, error) =>
        count < 1 && !(error instanceof ApiError && error.status < 500),
    },
  },
});
const route = (element: React.ReactNode) => (
  <Suspense fallback={<Loading />}>{element}</Suspense>
);
const router = createBrowserRouter([
  { path: "/", element: route(<Auth />), errorElement: <RouteFailure /> },
  { path: "/login", element: route(<Auth />), errorElement: <RouteFailure /> },
  {
    path: "/recovery",
    element: route(<Auth mode="recovery" />),
    errorElement: <RouteFailure />,
  },
  {
    path: "/reset-password",
    element: route(<Auth mode="reset" />),
    errorElement: <RouteFailure />,
  },
  {
    path: "/app",
    element: (
      <SessionProvider>
        <Shell />
      </SessionProvider>
    ),
    errorElement: <RouteFailure />,
    children: [
      { index: true, element: route(<Dashboard />) },
      { path: "resources", element: route(<Catalogue />) },
      { path: "resources/:id", element: route(<Resource />) },
      { path: "curriculum", element: route(<Curriculum />) },
      { path: "content", element: route(<ContentManagement />) },
      { path: "content/:id", element: route(<ContentManagement />) },
      { path: "organisations", element: route(<Organisations />) },
      { path: "organisations/:id", element: route(<Organisations />) },
      { path: "users", element: route(<Users />) },
      { path: "users/:id", element: route(<Users />) },
      { path: "projects", element: route(<Projects />) },
      { path: "projects/:id", element: route(<Projects />) },
      { path: "family", element: route(<Parents />) },
      { path: "reports", element: route(<Reports />) },
      { path: "devices", element: route(<Devices />) },
      { path: "collections", element: route(<Taxonomy kind="collections" />) },
      { path: "tags", element: route(<Taxonomy kind="tags" />) },
      { path: "audit", element: route(<Audit />) },
      { path: "account", element: route(<Account />) },
      { path: "*", element: <RouteFailure /> },
    ],
  },
  { path: "*", element: <RouteFailure /> },
]);
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <ToastRegion />
    </QueryClientProvider>
  </React.StrictMode>,
);
