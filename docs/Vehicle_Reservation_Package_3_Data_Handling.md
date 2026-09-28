# Package 3 data handling and activation design

Publication handoff and next-session checklist: [Package 3 handoff](Vehicle_Reservation_Package_3_Handoff.md).

## Classification and current implementation

| Data | Purpose and current treatment |
|---|---|
| Name, email, phone | Necessary contact data; existing Customer storage and authorization remain. Not newly field-encrypted. |
| Pickup/return, chosen vehicle, totals, conditions | Booking fulfilment, price evidence and conflict protection; previous accepted snapshots preserved in amendment history. |
| Declared age and completed licence years | Eligibility check, without birth date or document number; reconfirmed for changed dates. |
| Identity/licence numbers and full birth dates | No active reservation/profile collection; legacy request fields ignored and serialized driver/profile responses omit them. Existing database columns retained pending reviewed cleanup. |
| Verification code | Digest in access record; encrypted queue delivery payload; plaintext only at mail rendering/delivery and user input. Never part of URL or browser persistence. |
| Guest session and CSRF | Digests in database; scoped HttpOnly cookies in browser. CSRF alone is returned to the current page; session token is not. |
| Audit / mail queue | Booking event identifiers and accepted fee, without contact details in new audit entries. Queue recipient email remains personal data. Guest delivery errors are generic and do not copy provider detail. |

No blanket logs/backup scan was performed. The accompanying SQL returns aggregate counts only. Run it only against an explicitly selected authorized environment, using a read-only account. Do not export values to tickets, logs or prompts. Include database dumps, mail queue exports, object storage and old logs in a separate backup inventory; source column names alone do not prove that real personal data exists.

## Retention decisions

Technical validity is fixed at ten minutes for verification and twenty minutes for management sessions. Expiry revokes authority; it is not physical deletion. Five unsuccessful attempts exhaust a challenge. Logout revokes that session. Encrypted queued codes must not be treated as usable after their challenge expires.

The operator must define retention for contact records, completed/failed notification payloads, access records, audit history, amendments and backups before live collection. No legal retention duration is invented here. A cleanup design must:
- delete expired unused grants only after delivery retries are finished;
- retain or detach amendment references deliberately (the foreign key currently restricts deleting referenced grants);
- scrub delivery payloads after the agreed support/retry window;
- preserve required financial evidence while minimizing linked identifiers;
- record deletion counts, scope and restoration limits without logging values.

Historical cleanup is a separate authorized operation with inventory, preservation decisions and a tested restore path. This implementation does not drop sensitive columns or purge historical rows.

## Keys, encryption and recoverability

API and Worker use the same Data Protection application name and purpose `GuestReservationEmail.v1`. Provide the same durable key repository to both via `GuestAccess:KeyRingPath`; keep its access separate from database credentials and backups. Protect keys at rest with `GuestAccess:CertificateThumbprint` or a reviewed platform key-encryption provider. Explicit file persistence alone is not encryption at rest.

Only service identities should read key material. Certificate/private-key backup and rotation require controlled access, separate storage and a recovery exercise. Keep old decryption keys until pending/retry payloads that need them have expired or been replaced; do not remove keys solely because new keys were issued. A key compromise requires revocation and re-verification; encryption does not replace authorization.

For existing contact fields, the proposed migration is envelope encryption with versioned ciphertext and an external wrapping key. Exact email lookup requires a separate keyed normalized-email index; a plain SHA digest is not a private searchable index. Do not silently encrypt existing normalized-email columns, which would break login and reservation matching.

Stage contact encryption with additive columns, bounded backfill, dual-read compatibility, correctness counts, then cutover and separately authorized removal of old plaintext. Test key rotation, search equality, uniqueness, multi-host access and restore before real use. No encryption/backfill keys or credentials are included in this repository.

Backups must have an explicit retention schedule, restricted access and encryption independent of database login secrets. Restore tests need both data and approved key recovery while ensuring mail/SMS cannot reach real recipients. Deleting live rows does not erase older backups.

## Operational acceptance

- Verify missing policy disables guest mutations on old and new offices.
- Verify mail configuration and shared keys in both API and Worker using a local sink.
- Verify session and code expiry after restart, lost-response replay, concurrent workers and a permanent SMTP failure.
- Review HTTPS cookies, BFF origin checks and IP rate limiting behind the actual ingress.
- Record retention owner, schedule, key owner, backup owner and recovery evidence.
- Activate real mail or production changes only under separate authorization.
