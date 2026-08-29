# Money Manager Roadmap

## Current Foundation

- Required Supabase authentication and secure session restoration
- Offline-first transaction, account, goal, schedule, asset, and liability tracking
- Bank and credit-card CSV/PDF statement attachments
- CSV transaction imports with duplicate-statement detection
- Equal and custom transaction splitting
- Participant settlement tracking and shareable payment requests
- Cross-device JSONB backup and private statement storage

## Phase 1: Stronger Money Management

- Monthly category budgets with rollover rules
- Cash-flow forecasting from schedules and recurring transactions
- Net-worth history rather than only the current asset/liability snapshot
- Rules for automatically categorizing imported transactions
- Duplicate-transaction detection across overlapping statements
- Search, tags, notes, and receipt attachments

## Phase 2: Collaborative Splits

The current split belongs to one Money Manager account. Collaboration requires normalized PostgreSQL tables rather than relying only on the owner's backup snapshot.

- Invite another registered user by email or secure link
- Shared split membership with Supabase Row Level Security
- Participant acceptance and correction requests
- Activity history for share changes, reminders, and settlements
- Push notifications and reminder preferences
- Group balances and debt simplification
- Realtime updates across participants' devices

## Phase 3: In-App Payments

Money movement must be implemented through a licensed payment provider. Money Manager should never store raw card numbers, bank-login credentials, or payment-provider secret keys in the MAUI client.

Required platform work:

- Server-side payment intents or payment links
- Provider-hosted account connection and identity verification
- An immutable double-entry payment ledger
- Idempotency keys for every payment operation
- Signed webhook processing and replay protection
- Refund, cancellation, dispute, and failed-payment states
- Reconciliation between provider balances and Money Manager records
- Regional availability, currency, tax, privacy, and financial-regulation review
- Apple App Store and Google Play policy review before release

Implemented foundations:

- Owner-scoped payment request records and webhook event history
- Idempotent server-side Checkout creation
- Signed webhook reconciliation
- Stripe test-only card flow for UI and integration development
- Interac Request Money bank handoff
- Provider-neutral MAUI gateway interface

Production card charging remains blocked until an approved provider supports the exact peer-to-peer reimbursement model. Stripe's production service cannot be used for this purpose under its current prohibited-business policy.

## Phase 4: Institution Connectivity

- Open-banking provider integration through a server-side connector
- Consent lifecycle and connection health
- Incremental transaction synchronization
- Institution account matching and merge tools
- Read-only connections by default
- Clear data-retention and disconnect controls

## Engineering Priorities

- Replace last-write-wins backup sync with incremental record synchronization
- Add conflict resolution and tombstones for cross-device deletion
- Add database integration tests against a disposable Supabase project
- Add encrypted diagnostic logging with financial-data redaction
- Add accessibility, localization, and multi-currency domain support
- Add CI builds for Android, iOS, macOS, and Windows
