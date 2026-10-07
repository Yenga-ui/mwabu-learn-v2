import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { Action, Form } from "./ui";
import { ApiError } from "../api/client";
function route(element: React.ReactNode) {
  return render(
    <RouterProvider router={createMemoryRouter([{ path: "/", element }])} />,
  );
}
describe("accessible forms and actions", () => {
  it("prevents an empty required form and trims submitted names", async () => {
    const user = userEvent.setup();
    const submit = vi.fn().mockResolvedValue(undefined);
    route(
      <Form
        fields={[
          { name: "name", label: "Name", required: true, maxLength: 200 },
        ]}
        submit={submit}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(submit).not.toHaveBeenCalled();
    await user.type(
      screen.getByRole("textbox", { name: /Name/ }),
      "  Riverside School  ",
    );
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(submit).toHaveBeenCalledWith({ name: "Riverside School" });
  });
  it("shows field errors and a support reference after a failed submission", async () => {
    const user = userEvent.setup();
    route(
      <Form
        fields={[{ name: "name", label: "Name" }]}
        submit={async () => {
          throw new ApiError(
            400,
            "Check your entries.",
            { name: ["Name is required."] },
            "trace-abc",
          );
        }}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Name is required.",
    );
    expect(screen.getByRole("alert")).toHaveTextContent("trace-abc");
  });
  it("requires confirmation before a destructive action", async () => {
    const user = userEvent.setup();
    const run = vi.fn().mockResolvedValue(undefined);
    render(
      <Action confirm="Deactivate this membership?" run={run}>
        Deactivate
      </Action>,
    );
    await user.click(screen.getByRole("button", { name: "Deactivate" }));
    expect(run).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Confirm" }));
    expect(run).toHaveBeenCalledOnce();
  });
});
