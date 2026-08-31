# Money Manager Security

## Security Boundary

No internet-connected application can be guaranteed impossible to penetrate. Money Manager uses defense in depth, but security also depends on the Supabase configuration, user devices, release signing, dependency updates, monitoring, and operational response.

The loopback ASP.NET Core server is for development only. It binds to `127.0.0.1`, uses HTTP, and must never be exposed through port forwarding, a reverse proxy, a tunnel, or a public host. A multi-user or multi-device deployment must use the hosted Supabase configuration.

## Implemented Controls

- Login is required before any financial service can obtain a user context.
- Every local financial row is filtered and mutated using the authenticated user ID.
- User IDs used in statement paths accept only bounded alphanumeric, hyphen, and underscore values.
- Access and refresh tokens are stored with the platform `SecureStorage` API rather than preferences or source code.
- Supabase requests require HTTPS, and secret/service-role keys are never accepted by the client configuration.
- Hosted project configuration is read only from build metadata; runtime preferences cannot redirect authentication to another server.
- Supabase tables use RLS, private statement storage, owner-scoped paths, and read-only client access to payment state.
- Statement uploads are limited to 10 MB, validated against their declared CSV/PDF type, stored under generated names, and bounded during PDF extraction.
- Backup imports are limited to 5 MB and validated completely before existing data is cleared.
- Android app-data backup and cleartext traffic are disabled.
- Payment contact and online-banking preferences are scoped to the signed-in user, and page/view-model instances are recreated after account switching.
- The development API uses a default-deny request firewall for known routes: loopback and Host checks, forwarding-header rejection, method/content-type/body/query constraints, traversal checks, request/header limits, rate limiting, no-store security headers, bounded sessions, hashed bearer tokens, and generic server errors.
- Common scanner paths are decoys that never access account data. Probes, rejected authentication, rate-limit triggers, and unknown routes write privacy-minimized, size-bounded local audit events.
- New local-development passwords require 12-128 characters and use salted PBKDF2-HMAC-SHA256 with 600,000 iterations. Older hashes are upgraded after a successful login.
- Edge Functions restrict browser CORS to `ALLOWED_ORIGIN`, limit JSON request sizes, validate identifiers and HTTPS return URLs, and avoid exposing internal 5xx errors.
- Stripe webhook signatures are verified before state changes, duplicate event IDs are rejected, and only a minimal event audit record is retained.

## Production Release Blockers

Complete every item before allowing real users or sensitive production data:

1. Build with a dedicated Supabase production project URL and publishable key. Never bundle a secret key or `service_role` key.
2. Apply every migration in `supabase/migrations/` and run Supabase Security Advisor until no unintended public access remains.
3. Enable email confirmation, leaked-password protection, CAPTCHA, and appropriate Auth rate limits in Supabase.
4. Add and test a complete MFA enrollment, challenge, recovery, and unenrollment user experience before claiming high-assurance account protection. Enforce `aal2` with restrictive RLS policies only after that client flow exists.
5. Enable database SSL enforcement, network restrictions where supported, backups/PITR, log retention, and alerts for anomalous Auth, Storage, Function, and payment activity.
6. Set `ALLOWED_ORIGIN` to the exact trusted HTTPS web origin. Native MAUI clients do not require wildcard CORS.
7. Store Function and payment secrets only in Supabase secret storage. Rotate them after staff changes, suspected exposure, and on a documented schedule.
8. Use signed Release builds, protected CI environments, least-privilege repository access, branch protection, and dependency/security scanning.
9. Commission an independent penetration test covering Auth, RLS, Storage, Edge Functions, backup restore, payment webhooks, mobile binaries, and lost-device scenarios. Remediate and retest all significant findings.
10. Establish vulnerability reporting, incident response, token/key revocation, breach notification, backup recovery, and audit-log review procedures.
11. Put the hosted API behind provider-supported network controls, rate limits, bot protection, and alerting. Keep any production honeypot in a separate account/project with no route, credential, network trust, or storage shared with Money Manager.

Supabase's official production checklist should be reviewed for every release: <https://supabase.com/docs/guides/deployment/going-into-prod>.

Implementation baselines:

- OWASP password storage guidance: <https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html>
- ASP.NET Core rate limiting: <https://learn.microsoft.com/aspnet/core/performance/rate-limit>
- OWASP application logging guidance: <https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html>
- .NET MAUI secure storage: <https://learn.microsoft.com/dotnet/maui/platform-integration/storage/secure-storage>
- Supabase Storage access control: <https://supabase.com/docs/guides/storage/security/access-control>

## Data At Rest

The operating system protects the app-private directory, and session secrets use platform secure storage. The SQLite database, local statement files, and user-exported JSON backups are not additionally encrypted by Money Manager. A device administrator, rooted/jailbroken device, malware running as the user, or anyone receiving an exported backup may be able to read that data.

Before handling higher-risk financial data, decide whether to add SQLCipher or field-level encryption and encrypted statement files. That design must include recoverable key management, device migration, logout behavior, backup encryption, key rotation, and data-loss testing; encryption without a sound key lifecycle can permanently destroy user data.

## Multi-User Isolation Checks

- Use two test accounts and verify that transactions, goals, schedules, accounts, statements, splits, backups, payment accounts, and payment requests cannot be read or changed across users.
- Test direct REST and Storage requests with each user's JWT, not only through the app UI.
- Test expired, revoked, malformed, and lower-assurance tokens.
- Test object names outside `<auth-user-id>/<sha256>.csv|pdf`; all must fail.
- Repeat these checks after every RLS or backup-schema change.

## Firewall And Deception Limits

The included application firewall and `Install-LocalFirewallRule.ps1` protect the loopback development API only. They do not configure Supabase's network edge and must not be treated as a production WAF. The local decoys provide detection evidence, not proof that an attacker has been contained.

Security audit files are stored beside the development account store as `security-events.jsonl`, rotate at 5 MB, and retain one previous file. They intentionally omit request bodies, authorization headers, tokens, email addresses, query values, route text, IP addresses, and raw user-agent strings. A client fingerprint is a truncated one-way hash used only to correlate repeated local events.

Do not connect a production honeypot to the production Supabase project. A useful production deception system needs a separate trust boundary, strict cost/volume controls, centralized alerts, retention rules, and an incident-response owner. Otherwise it adds attack surface and can become a denial-of-service or data-contamination path.

## Payment Boundary

Stripe integration remains test-only. Enabling real card or bank money movement requires an approved provider, identity and sanctions controls, fraud operations, an immutable ledger, reconciliation, dispute/refund handling, privacy and regulatory review, and provider/webhook incident procedures. A successful technical test is not authorization to move production funds.
