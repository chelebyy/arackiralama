export type GuestAccessChallenge = { challengeId: string; requestedAt: number };

const storageKey = "guest-access-challenge";
export const challengeLifetimeMs = 10 * 60 * 1000;
export const challengeCooldownMs = 60 * 1000;

export function storeGuestChallenge(challenge: GuestAccessChallenge | null) {
  try {
    if (challenge) sessionStorage.setItem(storageKey, JSON.stringify(challenge));
    else sessionStorage.removeItem(storageKey);
  } catch {}
}

export function readGuestChallenge(): GuestAccessChallenge | null {
  try {
    const value: unknown = JSON.parse(sessionStorage.getItem(storageKey) ?? "null");
    if (value && typeof value === "object" && "challengeId" in value && "requestedAt" in value &&
        typeof value.challengeId === "string" && value.challengeId.length > 0 && value.challengeId.length <= 64 &&
        typeof value.requestedAt === "number" && Number.isFinite(value.requestedAt) &&
        value.requestedAt <= Date.now() && value.requestedAt + challengeLifetimeMs > Date.now()) {
      return { challengeId: value.challengeId, requestedAt: value.requestedAt };
    }
  } catch {}
  storeGuestChallenge(null);
  return null;
}
