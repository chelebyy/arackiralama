import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
  useLocale: () => "en",
}));

vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(window.location.search),
}));

vi.mock("@/lib/auth/backend", () => ({
  callAccountClaimEndpoint: vi.fn(async () => ({
    backendResponse: { ok: true, status: 200 },
  })),
}));

import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import AccountClaimPage from "@/app/(public)/[locale]/account-claim/page";
import { callAccountClaimEndpoint } from "@/lib/auth/backend";

describe("AccountClaimPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState({}, "", "/en/account-claim#token=test-token");
  });

  it("reads the token from the URL fragment and removes it from browser history", async () => {
    render(<AccountClaimPage />);

    expect(await screen.findByLabelText("fields.newPassword")).toBeInTheDocument();
    expect(window.location.pathname).toBe("/en/account-claim");
    expect(window.location.search).toBe("");
    expect(window.location.hash).toBe("");
  });

  it("supports legacy query links while removing the token from browser history", async () => {
    window.history.replaceState({}, "", "/en/account-claim?token=legacy-token");

    render(<AccountClaimPage />);

    const newPassword = await screen.findByLabelText("fields.newPassword");
    fireEvent.change(newPassword, { target: { value: "SuperSecret1!" } });
    fireEvent.change(screen.getByLabelText("fields.confirmPassword"), {
      target: { value: "SuperSecret1!" },
    });
    fireEvent.submit(newPassword.closest("form")!);

    await waitFor(() => {
      expect(callAccountClaimEndpoint).toHaveBeenCalledWith({
        token: "legacy-token",
        newPassword: "SuperSecret1!",
      });
    });
    expect(window.location.search).toBe("");
  });

  it("keeps the active locale in home links", async () => {
    window.history.replaceState({}, "", "/en/account-claim");

    render(<AccountClaimPage />);

    expect(await screen.findByRole("link", { name: "buttons.backToHome" }))
      .toHaveAttribute("href", "/en");
  });

  it("submits the claim and renders the success state when backend succeeds", async () => {
    render(<AccountClaimPage />);

    const newPassword = await screen.findByLabelText("fields.newPassword") as HTMLInputElement;
    const confirmPassword = screen.getByLabelText(
      "fields.confirmPassword"
    ) as HTMLInputElement;
    fireEvent.change(newPassword, { target: { value: "SuperSecret1!" } });
    fireEvent.change(confirmPassword, { target: { value: "SuperSecret1!" } });

    const form = newPassword.closest("form")!;
    fireEvent.submit(form);

    await waitFor(() => {
      expect(callAccountClaimEndpoint).toHaveBeenCalledWith({
        token: "test-token",
        newPassword: "SuperSecret1!",
      });
    });

    await waitFor(() => {
      expect(screen.getByText("status.success")).toBeInTheDocument();
    });
  });

  it("surfaces a backend failure as a failed status", async () => {
    vi.mocked(callAccountClaimEndpoint).mockResolvedValueOnce({
      backendResponse: { ok: false, status: 400 },
    } as never);

    render(<AccountClaimPage />);
    const newPassword = await screen.findByLabelText("fields.newPassword") as HTMLInputElement;
    const confirmPassword = screen.getByLabelText(
      "fields.confirmPassword"
    ) as HTMLInputElement;
    fireEvent.change(newPassword, { target: { value: "SuperSecret1!" } });
    fireEvent.change(confirmPassword, { target: { value: "SuperSecret1!" } });

    fireEvent.submit(newPassword.closest("form")!);
    await waitFor(() => expect(screen.getByText("status.failed")).toBeInTheDocument());
  });
});
