# Payment Platform Setup

## Supported Flows

### Card sandbox

Money Manager can create Stripe Connect recipient accounts, generate Stripe Checkout links for Visa/Mastercard test cards, receive signed webhook events, and reconcile successful test payments into split settlement state.

This adapter is intentionally sandbox-only. Stripe lists personal or peer-to-peer money transmission as prohibited, including sending money between friends. Do not attempt to use a live Stripe key for this feature.

- [Stripe restricted businesses](https://stripe.com/en-ca/legal/restricted-businesses)
- [Stripe guidance on peer-to-peer payments](https://support.stripe.com/questions/requirements-for-accepting-tips-or-donations)

### Interac e-Transfer Request Money

Interac Request Money is provided through participating banks and credit unions. Money Manager records the request, copies the participant, amount, and memo, then opens the online-banking URL configured by the user. The user must authenticate with their financial institution and submit the request there.

- [Interac e-Transfer Request Money FAQ](https://www.interac.ca/en/resources/personal-resources/personal-faq/interac-e-transfer/)

Money Manager never asks for or stores an online-banking password.

## Database

Apply both migrations in timestamp order:

```text
supabase/migrations/202608040001_initial_schema.sql
supabase/migrations/202608290001_payment_platform.sql
```

The payment migration creates:

- `payment_accounts`: recipient-provider onboarding state
- `payment_requests`: owner-scoped card and Interac requests
- `payment_events`: idempotent webhook event history
- RLS policies that allow users to read only their own account and requests

Only Edge Functions using Supabase secret credentials can mutate payment tables.

## Stripe Test Configuration

1. Create or use a Stripe test account and enable Connect test mode.
2. Copy [`supabase/functions/.env.example`](../supabase/functions/.env.example) to an ignored local environment file.
3. Fill in test secrets and HTTPS return pages.
4. Store the secrets in Supabase.

```powershell
supabase secrets set --env-file supabase/functions/.env
```

`STRIPE_SECRET_KEY` must start with `sk_test_`. The server rejects live mode and live keys.

## Deploy Functions

```powershell
supabase functions deploy create-connect-account
supabase functions deploy create-card-payment
supabase functions deploy create-interac-request
supabase functions deploy stripe-webhook --no-verify-jwt
```

The webhook is the only function without Supabase JWT verification. It verifies the raw payload against `STRIPE_WEBHOOK_SIGNING_SECRET` before processing an event.

Configure this Stripe test webhook endpoint:

```text
https://<project-ref>.supabase.co/functions/v1/stripe-webhook
```

Subscribe to:

- `account.updated`
- `checkout.session.completed`
- `checkout.session.async_payment_succeeded`
- `checkout.session.async_payment_failed`
- `checkout.session.expired`
- `payment_intent.succeeded`
- `payment_intent.payment_failed`
- `charge.refunded`

## Test Procedure

1. Sign in to Money Manager.
2. Open Settings and select **Set up card sandbox**.
3. Complete Stripe's test Connect onboarding.
4. Open a split and select **Card test** for an unpaid participant.
5. Share or open the generated Checkout URL and use a Stripe test card.
6. Return to the split and select **Refresh payments**.
7. Confirm the participant changes to paid only after the signed webhook updates `payment_requests`.

## Production Requirements

A production implementation needs a provider or network sponsor that explicitly approves consumer expense reimbursement or peer-to-peer transfers. Depending on the chosen model, that can include a Visa Direct, Mastercard Send, or Interac commercial partnership, identity verification, sanctions screening, fraud controls, limits, refunds, disputes, reconciliation, regulatory registration, privacy review, and operational support.

Before production:

- Obtain written provider approval for the exact funds flow.
- Replace `SupabasePaymentGatewayService` with the approved production adapter.
- Implement the provider's onboarding and payout requirements.
- Add immutable double-entry ledger entries for every monetary state transition.
- Complete legal and regulatory review in every supported jurisdiction.
- Complete app-store payment and financial-services policy review.
- Run database and webhook integration tests against the provider sandbox.

Licensing and provider approval cannot be postponed until after live money movement begins. They are prerequisites for enabling it.
