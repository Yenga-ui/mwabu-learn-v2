import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
const roles = [
  {
    role: "PlatformAdmin",
    heading: "Your platform, at a glance",
    nav: "Platform administration tasks",
    routes: ["/app/users", "/app/organisations", "/app/content"],
  },
  {
    role: "OrganisationAdmin",
    heading: "Your organisation workspace",
    nav: "Organisation administration tasks",
    routes: ["/app/organisations"],
  },
  {
    role: "Learner",
    heading: "What will you discover today?",
    nav: "Learning tasks",
    routes: ["/app/resources", "/app/assessments", "/app/collections"],
  },
];
for (const role of roles)
  for (const width of [390, 1366])
    test(`${role.role} workspace supports its real permissions and journeys at ${width}px`, async ({
      page,
    }, info) => {
      await page.setViewportSize({ width, height: width === 390 ? 844 : 768 });
      const fixture = JSON.parse(
        readFileSync(".local/e2e/fixture.json", "utf8"),
      );
      const account =
        role.role === "PlatformAdmin"
          ? fixture.admin
          : fixture.roles[role.role];
      await page.goto("/login");
      await page.getByLabel("Email address").fill(account.email);
      await page.getByLabel(/^Password/).fill(account.password);
      await page.getByRole("button", { name: "Sign in", exact: true }).click();
      await expect(
        page.getByRole("heading", { name: role.heading, exact: true }),
      ).toBeVisible();
      await expect(
        page.getByRole("navigation", { name: role.nav, exact: true }),
      ).toBeVisible();
      await expect(page.locator("#main")).not.toContainText(
        "Loading your workspace…",
      );
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth,
        ),
      ).toBe(true);
      await page.screenshot({
        path: info.outputPath("home.png"),
        fullPage: true,
      });
      if (role.role === "OrganisationAdmin") {
        await expect(
          page.getByRole("link", { name: /People & licences/ }),
        ).toHaveCount(0);
        await page
          .getByRole("link", { name: /People in your organisation/ })
          .click();
        await expect(page.locator("#members")).toBeFocused();
        await expect(
          page.getByRole("heading", {
            name: "People in this organisation",
            exact: true,
          }),
        ).toBeVisible();

        await page
          .getByRole("navigation", { name: "Organisation sections" })
          .getByRole("link", { name: "Curriculum", exact: true })
          .click();
        await expect(page.locator("#assigned-curriculum")).toBeFocused();
      }
      if (role.role === "Learner") {
        await expect(
          page.getByRole("link", {
            name: /People & licences|Content studio|Create lesson/,
          }),
        ).toHaveCount(0);
        const response = await page.request.get("/api/users");
        expect(response.status()).toBe(403);
        const resource = page.locator(".content-card").first();
        await expect(resource).toBeVisible();
        await resource.click();
        await expect(page.locator("#main")).not.toContainText(
          "Loading your workspace…",
        );
        await expect(page.getByRole("heading").first()).toBeVisible();
      }
      for (const path of role.routes) {
        await page.goto(path);
        await expect(page.locator("#main h1")).toBeVisible();
        await expect(page.locator("#main")).not.toContainText(
          "Loading your workspace…",
        );
        expect(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
          ),
        ).toBe(true);
        await page.screenshot({
          path: info.outputPath(path.split("/").at(-1) + ".png"),
          fullPage: true,
        });
      }
    });
