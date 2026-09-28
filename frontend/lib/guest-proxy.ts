import { createHmac } from "node:crypto";
import { isIP } from "node:net";

export function guestProxyHeaders(headers: Headers, method: string, path: string) {
  const secret = process.env.GUEST_PROXY_SECRET;
  const header = process.env.GUEST_CLIENT_IP_HEADER;
  if (!secret && !header && process.env.NODE_ENV !== "production") return {};
  const ip = header ? headers.get(header)?.trim() : undefined;
  if (!secret || secret.length < 32 || !ip || !isIP(ip))
    throw new Error("Guest proxy identity is not configured.");
  const client = createHmac("sha256", secret).update(ip).digest("hex");
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const signature = createHmac("sha256", secret)
    .update(`${timestamp}\n${client}\n${method}\n${path}`).digest("hex");
  return { "X-Guest-Client": client, "X-Guest-Timestamp": timestamp, "X-Guest-Signature": signature };
}
