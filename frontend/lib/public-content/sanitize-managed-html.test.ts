import { describe, expect, it } from "vitest";

import { sanitizeManagedHtml } from "./sanitize-managed-html";

describe("sanitizeManagedHtml", () => {
  it("keeps approved rich text tags", () => {
    expect(sanitizeManagedHtml("<p>Hello <strong>world</strong></p>")).toBe(
      "<p>Hello <strong>world</strong></p>",
    );
  });

  it("removes script iframe style and event attributes", () => {
    const result = sanitizeManagedHtml(
      '<p style="color:red" onclick="alert(1)">Hello</p><script>alert(1)</script><iframe src="https://example.com"></iframe>',
    );

    expect(result).toBe("<p>Hello</p>");
  });

  it("removes unsafe and protocol-relative links", () => {
    expect(sanitizeManagedHtml('<a href="javascript:alert(1)">bad</a>')).toBe("<a>bad</a>");
    expect(sanitizeManagedHtml('<a href="//example.com">bad</a>')).toBe("<a>bad</a>");
  });

  it("keeps safe links with noopener noreferrer", () => {
    const result = sanitizeManagedHtml('<a href="https://example.com">safe</a>');

    expect(result).toContain('rel="noopener noreferrer"');
    expect(result).toContain('target="_blank"');
  });
});
