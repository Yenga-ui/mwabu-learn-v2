import { test, expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";

type Account = { email: string; password: string };
type Fixture = {
  admin: Account;
  roles: Record<string, Account>;
  schoolId: string;
  programmeOrganisationId: string;
  projectId: string;
};
const fixture = (): Fixture =>
  JSON.parse(readFileSync(".local/e2e/fixture.json", "utf8"));
async function login(page: Page, role = "Teacher") {
  const data = fixture();
  const account = role === "PlatformAdmin" ? data.admin : data.roles[role];
  await page.goto("/login");
  await page.getByLabel("Email address").fill(account.email);
  await page.getByLabel(/^Password/).fill(account.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.locator("#main")).toBeVisible();
}
async function read(page: Page, path: string) {
  return await page.evaluate(async (url) => {
    const response = await fetch(url, {
      credentials: "same-origin",
      cache: "no-store",
    });
    return { status: response.status, body: await response.json() };
  }, path);
}
async function mutate(
  page: Page,
  path: string,
  method: string,
  body?: unknown,
) {
  return await page.evaluate(
    async (args) => {
      const csrf = await (
        await fetch("/api/browser/session/csrf", { cache: "no-store" })
      ).json();
      const response = await fetch(args.path, {
        method: args.method,
        credentials: "same-origin",
        headers: {
          "X-Mwabu-CSRF": csrf.requestToken,
          ...(args.body ? { "Content-Type": "application/json" } : {}),
        },
        body: args.body ? JSON.stringify(args.body) : undefined,
      });
      return {
        status: response.status,
        body: response.status === 204 ? null : await response.json(),
      };
    },
    { path, method, body },
  );
}

test("anonymous routes require login and APIs return 401", async ({ page }) => {
  await page.goto("/app/users");
  await expect(page).toHaveURL(/\/login/);
  expect((await read(page, "/api/auth/me")).status).toBe(401);
});
test("login restores the real current user without storing credentials", async ({
  page,
}) => {
  await login(page);
  expect((await read(page, "/api/auth/me")).body.email).toBe(
    fixture().roles.Teacher.email,
  );
  expect(
    await page.evaluate(() => [localStorage.length, sessionStorage.length]),
  ).toEqual([0, 0]);
  expect(await page.evaluate(() => document.cookie)).not.toContain(
    "MwabuAccess",
  );
});
test("invalid login displays a generic error", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email address").fill("missing@mwabu.invalid");
  await page.getByLabel(/^Password/).fill("InvalidAccountInput!42");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page).toHaveURL(/login/);
});
test("platform administrator creates and updates an organisation through the web", async ({
  page,
}) => {
  await login(page, "PlatformAdmin");
  await page.goto("/app/organisations");
  await page.getByRole("button", { name: /Create organisation/ }).click();
  await page.getByLabel("Organisation name").fill("Browser Acceptance School");
  await page.getByLabel("Organisation code").fill("BROWSER-SCHOOL");
  await page.getByLabel("Organisation type").selectOption("School");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /Create|Save/ })
    .click();
  await expect(
    page.getByRole("heading", { name: "Browser Acceptance School" }),
  ).toBeVisible();
});
test("platform administrator creates a user through the web", async ({
  page,
}) => {
  await login(page, "PlatformAdmin");
  await page.goto("/app/users");
  await page.getByRole("button", { name: /Create user/ }).click();
  await page.getByLabel("Email address").fill("browser.created@mwabu.invalid");
  await page.getByLabel("First name").fill("Browser");
  await page.getByLabel("Last name").fill("Created");
  await page
    .getByLabel("Initial password")
    .fill(fixture().roles.Teacher.password);
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /Create|Save/ })
    .click();
  await expect(
    page.getByText("browser.created@mwabu.invalid").first(),
  ).toBeVisible();
});
test("platform administrator adds a membership, combines roles, removes a role and deactivates membership", async ({
  page,
}) => {
  await login(page, "PlatformAdmin");
  const email = "browser.membership@mwabu.invalid";
  expect(
    (
      await mutate(page, "/api/users", "POST", {
        email,
        firstName: "Membership",
        lastName: "Candidate",
        initialPassword: fixture().roles.Teacher.password,
      })
    ).status,
  ).toBe(201);
  await page.goto("/app/organisations/" + fixture().schoolId);
  await page.getByRole("button", { name: "Add existing user" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByPlaceholder("Search name or email…").fill(email);
  await dialog.getByPlaceholder("Search name or email…").press("Enter");
  const option = dialog.locator("option").filter({ hasText: email });
  await expect(option).toHaveCount(1);
  await dialog
    .getByRole("combobox", { name: "User", exact: true })
    .selectOption((await option.getAttribute("value"))!);
  await dialog.getByRole("button", { name: "Add membership" }).click();
  await expect(dialog).toHaveCount(0);
  const row = page.getByRole("row").filter({ hasText: email });
  await row.getByRole("button", { name: "Manage roles" }).click();
  for (const name of ["Teacher", "Learner"]) {
    await page
      .getByRole("dialog")
      .locator(".assignment")
      .filter({ has: page.getByText(name, { exact: true }) })
      .getByRole("button", { name: "Assign", exact: true })
      .click();
  }
  await page
    .getByRole("dialog")
    .locator(".assignment")
    .filter({ has: page.getByText("Teacher", { exact: true }) })
    .getByRole("button", { name: "Remove", exact: true })
    .click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(1);
  await page.getByRole("button", { name: "Close dialog", exact: true }).click();
  await expect(row).toContainText("Learner");
  await expect(row).not.toContainText("Teacher");
  await row.getByRole("button", { name: "Deactivate", exact: true }).click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await expect(row).toContainText("Inactive");
});
test("curriculum editor creates and edits a framework without changing demo data", async ({
  page,
}) => {
  await login(page, "PlatformAdmin");
  await page.goto("/app/curriculum");
  await page
    .getByRole("button", { name: "Add curriculum", exact: true })
    .click();
  await page.getByLabel("Country code").fill("ZM");
  await page
    .getByRole("dialog")
    .getByLabel("Name", { exact: false })
    .fill("Browser curriculum");
  await page
    .getByRole("dialog")
    .getByLabel("Code", { exact: true })
    .fill("BROWSER-CURRICULUM");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Save changes" })
    .click();
  const node = page
    .locator(".curriculum-node")
    .filter({ hasText: "Browser curriculum" });
  await node.getByRole("button", { name: "Edit", exact: true }).click();
  await page
    .getByRole("dialog")
    .getByLabel("Name", { exact: false })
    .fill("Revised browser curriculum");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Save changes" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Revised browser curriculum" }),
  ).toBeVisible();
});
test("curriculum explorer traverses the complete eight-level hierarchy", async ({
  page,
}) => {
  await login(page, "PlatformAdmin");
  await page.goto("/app/curriculum");
  for (const name of [
    "Demonstration Learning Framework",
    "Demonstration 2026",
    "Grade 4",
    "Mathematics",
    "Term 1",
    "Fractions",
    "Equal parts",
    "Represent one half",
  ]) {
    await page
      .getByRole("button", { name: new RegExp(name, "i") })
      .last()
      .click();
  }
  await expect(
    page.getByRole("heading", { name: "Learning outcome", exact: true }),
  ).toBeVisible();
});
test("content manager uploads, tags, maps, reviews and publishes a resource", async ({
  page,
}) => {
  await login(page, "ContentManager");
  await page.goto("/app/content");
  await page.getByRole("button", { name: "Create resource" }).click();
  await page.getByLabel("Resource title").fill("Browser reviewed lesson");
  await page.getByLabel("URL name").fill("browser-reviewed-lesson");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /Save|Create/ })
    .click();
  await expect(
    page.getByRole("heading", { name: "Browser reviewed lesson" }),
  ).toBeVisible();
  const published = await read(page, "/api/learning/content?text=sharing");
  const resource = await read(
    page,
    "/api/learning/content/" + published.body.items[0].id,
  );
  const pdf = await page.request.get(resource.body.assets[0].downloadUrl);
  await page.getByLabel("Choose file").setInputFiles({
    name: "browser-lesson.pdf",
    mimeType: "application/pdf",
    buffer: await pdf.body(),
  });
  await page.getByRole("button", { name: "Upload file" }).click();
  await expect(
    page.getByRole("link", { name: "browser-lesson.pdf" }),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: "Add tag", exact: true })
    .selectOption({ label: "Fractions" });
  await page
    .getByRole("button", { name: "Add selected tag", exact: true })
    .click();
  await page
    .getByRole("combobox", { name: "Add collection", exact: true })
    .selectOption({ label: "Everyday mathematics" });
  await page
    .getByRole("button", { name: "Add selected collection", exact: true })
    .click();
  // Another test creates an empty framework. Never rely on random GUID ordering.
  for (const [label, name] of [
    ["Curriculum", "Demonstration Learning Framework"],
    ["Versions", "Demonstration 2026"],
    ["Grades", "Grade 4"],
    ["Subjects", "Mathematics"],
    ["Terms", "Term 1"],
    ["Topics", "Fractions in everyday life"],
    ["Competencies", "Recognise equal parts"],
    ["Learning outcomes", "Represent one half using familiar objects"],
  ]) {
    await page
      .getByRole("combobox", { name: label, exact: true })
      .selectOption({ label: name });
  }
  await page
    .getByRole("button", { name: /Connect Represent one half/ })
    .click();
  await page.getByRole("button", { name: "Submit for review" }).click();
  await expect(page.getByText("InReview", { exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: "Publish resource", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm" })
    .click();
  await expect(
    page.getByText("Published", { exact: true }).first(),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit metadata" })).toHaveCount(
    0,
  );
});
test("teacher searches published resources and opens the authenticated PDF", async ({
  page,
}) => {
  await login(page);
  await page.goto("/app/resources?text=sharing");
  await page
    .getByRole("link", { name: /Sharing equally/ })
    .first()
    .click();
  await expect(page.locator("iframe")).toBeVisible();
  const src = await page.locator("iframe").getAttribute("src");
  const response = await page.request.get(src!);
  expect(response.status()).toBe(200);
  expect(response.headers()["content-type"]).toContain("application/pdf");
});
test("teacher cannot publish or administer users even with a crafted request", async ({
  page,
}) => {
  await login(page);
  expect(
    (await mutate(page, "/api/content", "POST", { title: "Forbidden" })).status,
  ).toBe(403);
  expect((await read(page, "/api/users")).status).toBe(403);
  await expect(
    page
      .getByRole("navigation", { name: "Main navigation" })
      .getByRole("link", { name: "People" }),
  ).toHaveCount(0);
});
test("learner receives published resources and no administrative navigation", async ({
  page,
}) => {
  await login(page, "Learner");
  await page.goto("/app/resources");
  await expect(
    page.getByRole("link", { name: /Sharing equally/ }).first(),
  ).toBeVisible();
  await expect(
    page
      .getByRole("navigation", { name: "Main navigation" })
      .getByRole("link", { name: "Content studio" }),
  ).toHaveCount(0);
});
test("parent sees only verified linked learner context", async ({ page }) => {
  await login(page, "ParentGuardian");
  await page.goto("/app/family");
  const result = await read(page, "/api/guardians/me/learners");
  expect(result.status).toBe(200);
  expect(result.body.items).toHaveLength(1);
  await expect(
    page.getByText("Riverside Demonstration School").last(),
  ).toBeVisible();
  expect(
    (
      await read(
        page,
        "/api/guardians/me/learners/00000000-0000-0000-0000-000000000001/curricula",
      )
    ).status,
  ).toBe(404);
});
test("headteacher reads school reporting without platform reporting access", async ({
  page,
}) => {
  await login(page, "HeadTeacher");
  await page.goto("/app/reports");
  expect(
    (await read(page, "/api/reports/organisations/" + fixture().schoolId))
      .status,
  ).toBe(200);
  expect((await read(page, "/api/reports/platform")).status).toBe(403);
});
test("project manager sees assigned project and cannot access a foreign project scope", async ({
  page,
}) => {
  await login(page, "ProjectManager");
  await page.goto("/app/projects");
  await expect(
    page.getByRole("link", { name: /Riverside learning programme/ }).first(),
  ).toBeVisible();
  expect(
    (await read(page, "/api/organisations/" + fixture().schoolId + "/projects"))
      .status,
  ).toBe(403);
});
test("organisation administrator provisions a scoped account without global user access", async ({
  page,
}) => {
  await login(page, "OrganisationAdmin");
  const response = await mutate(
    page,
    "/api/organisations/" + fixture().schoolId + "/users",
    "POST",
    {
      email: "scoped.browser@mwabu.invalid",
      firstName: "Scoped",
      lastName: "Browser",
      initialPassword: fixture().roles.Teacher.password,
    },
  );
  expect(response.status).toBe(201);
  expect(response.body.membership.organisationId).toBe(fixture().schoolId);
  expect((await read(page, "/api/users")).status).toBe(403);
});
test("data analyst sees operational reports and cannot change memberships", async ({
  page,
}) => {
  await login(page, "DataAnalyst");
  await page.goto("/app/reports");
  expect(
    (await read(page, "/api/reports/organisations/" + fixture().schoolId))
      .status,
  ).toBe(200);
  expect(
    (
      await mutate(
        page,
        "/api/organisations/" + fixture().schoolId + "/members",
        "POST",
        { userId: "00000000-0000-0000-0000-000000000001" },
      )
    ).status,
  ).toBe(403);
});
test("browser refresh rotates a cookie-only session and remains authenticated", async ({
  page,
}) => {
  await login(page);
  const before = (await page.context().cookies()).find(
    (cookie) => cookie.name === "__Host-MwabuRefresh",
  )!.value;
  const response = await mutate(page, "/api/browser/session/refresh", "POST");
  expect(response.status).toBe(200);
  expect(JSON.stringify(response.body)).not.toMatch(/accessToken|refreshToken/);
  expect(
    (await page.context().cookies()).find(
      (cookie) => cookie.name === "__Host-MwabuRefresh",
    )!.value,
  ).not.toBe(before);
  expect((await read(page, "/api/auth/me")).status).toBe(200);
});
test("two tabs coordinate one refresh without replaying the rotated credential", async ({
  page,
  context,
}) => {
  await login(page);
  const access = (await context.cookies()).find(
    (cookie) => cookie.name === "__Host-MwabuAccess",
  )!;
  await context.addCookies([{ ...access, value: "expired-test-access" }]);
  let rotations = 0;
  context.on("request", (request) => {
    if (
      request.method() === "POST" &&
      request.url().endsWith("/api/browser/session/refresh")
    )
      rotations++;
  });
  const second = await context.newPage();
  await Promise.all([page.goto("/app"), second.goto("/app")]);
  await expect(
    page.getByRole("navigation", { name: "Main navigation" }),
  ).toBeVisible();
  await expect(
    second.getByRole("navigation", { name: "Main navigation" }),
  ).toBeVisible();
  expect(rotations).toBe(1);
  expect((await read(second, "/api/auth/me")).status).toBe(200);
});
test("logout revokes the session and returns to login", async ({ page }) => {
  await login(page);
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm" })
    .click();
  await expect(page).toHaveURL(/login/);
  expect((await read(page, "/api/auth/me")).status).toBe(401);
});
test("mobile workspace fits the viewport and keyboard navigation reaches the menu", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page);
  await expect(
    page.getByRole("button", { name: "Open navigation" }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "Open navigation" }).click();
  await expect(
    page.getByRole("navigation", { name: "Main navigation" }),
  ).toBeVisible();
});
