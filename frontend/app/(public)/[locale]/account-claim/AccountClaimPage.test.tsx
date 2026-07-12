import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
}));

vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams("token=test-token"),
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
  });

  it("renders the form when a token is present", () => {
    render(<AccountClaimPage />);
    expect(screen.getByText("title")).toBeInTheDocument();
    expect(screen.getByLabelText("fields.newPassword")).toBeInTheDocument();
    expect(screen.getByLabelText("fields.confirmPassword")).toBeInTheDocument();
  });

  it("submits the claim and renders the success state when backend succeeds", async () => {
    render(<AccountClaimPage />);

    const newPassword = screen.getByLabelText("fields.newPassword") as HTMLInputElement;
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
    const newPassword = screen.getByLabelText("fields.newPassword") as HTMLInputElement;
    const confirmPassword = screen.getByLabelText(
      "fields.confirmPassword"
    ) as HTMLInputElement;
    fireEvent.change(newPassword, { target: { value: "SuperSecret1!" } });
    fireEvent.change(confirmPassword, { target: { value: "SuperSecret1!" } });

    fireEvent.submit(newPassword.closest("form")!);
    await waitFor(() => expect(screen.getByText("status.failed")).toBeInTheDocument());
  });
});