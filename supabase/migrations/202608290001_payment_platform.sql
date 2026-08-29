create table if not exists public.payment_accounts (
    user_id uuid primary key references auth.users(id) on delete cascade,
    provider text not null default 'stripe' check (provider in ('stripe')),
    provider_account_id text not null unique,
    charges_enabled boolean not null default false,
    payouts_enabled boolean not null default false,
    details_submitted boolean not null default false,
    test_mode boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.payment_requests (
    id uuid primary key default gen_random_uuid(),
    owner_id uuid not null references auth.users(id) on delete cascade,
    split_sync_id text not null,
    participant_sync_id text not null,
    participant_name text not null,
    method text not null check (method in ('card', 'interac')),
    amount numeric(12, 2) not null check (amount > 0),
    currency text not null default 'cad' check (currency ~ '^[a-z]{3}$'),
    status text not null default 'pending' check (
        status in ('pending', 'processing', 'paid', 'failed', 'expired', 'cancelled', 'refunded')
    ),
    provider text not null,
    provider_session_id text,
    checkout_url text,
    idempotency_key uuid not null,
    failure_message text,
    paid_at timestamptz,
    expires_at timestamptz,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (owner_id, idempotency_key)
);

create index if not exists payment_requests_owner_created_idx
on public.payment_requests (owner_id, created_at desc);

create index if not exists payment_requests_provider_session_idx
on public.payment_requests (provider_session_id)
where provider_session_id is not null;

create table if not exists public.payment_events (
    id bigint generated always as identity primary key,
    provider text not null,
    provider_event_id text not null,
    event_type text not null,
    payment_request_id uuid references public.payment_requests(id) on delete set null,
    payload jsonb not null,
    processed_at timestamptz not null default now(),
    unique (provider, provider_event_id)
);

alter table public.payment_accounts enable row level security;
alter table public.payment_requests enable row level security;
alter table public.payment_events enable row level security;

create policy "Users can read their own payment account"
on public.payment_accounts for select
to authenticated
using ((select auth.uid()) = user_id);

create policy "Users can read their own payment requests"
on public.payment_requests for select
to authenticated
using ((select auth.uid()) = owner_id);

-- Mutations are intentionally restricted to Edge Functions using the secret key.
-- payment_events has no client policy and is never exposed to authenticated users.
