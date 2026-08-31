begin;

alter table public.user_backups force row level security;
alter table public.payment_accounts force row level security;
alter table public.payment_requests force row level security;
alter table public.payment_events force row level security;

revoke all on table public.user_backups from anon;
revoke all on table public.payment_accounts from anon;
revoke all on table public.payment_requests from anon;
revoke all on table public.payment_events from anon, authenticated;

revoke all on table public.user_backups from authenticated;
grant select, insert, update on table public.user_backups to authenticated;

revoke all on table public.payment_accounts from authenticated;
grant select on table public.payment_accounts to authenticated;

revoke all on table public.payment_requests from authenticated;
grant select on table public.payment_requests to authenticated;

alter table public.user_backups
    add constraint user_backups_payload_size_check
    check (octet_length(payload::text) <= 4194304) not valid;

alter table public.payment_requests
    add constraint payment_requests_split_sync_id_length_check
    check (char_length(split_sync_id) between 1 and 64) not valid,
    add constraint payment_requests_participant_sync_id_length_check
    check (char_length(participant_sync_id) between 1 and 64) not valid,
    add constraint payment_requests_participant_name_length_check
    check (char_length(participant_name) between 1 and 200) not valid,
    add constraint payment_requests_failure_message_length_check
    check (failure_message is null or char_length(failure_message) <= 500) not valid;

drop policy if exists "Users can read their own statements" on storage.objects;
drop policy if exists "Users can upload their own statements" on storage.objects;
drop policy if exists "Users can update their own statements" on storage.objects;
drop policy if exists "Users can delete their own statements" on storage.objects;

create policy "Users can read their own statements"
on storage.objects for select
to authenticated
using (
    bucket_id = 'statements'
    and owner_id = (select auth.uid())::text
    and name ~ ('^' || (select auth.uid())::text || '/[0-9a-f]{64}[.](csv|pdf)$')
);

create policy "Users can upload their own statements"
on storage.objects for insert
to authenticated
with check (
    bucket_id = 'statements'
    and name ~ ('^' || (select auth.uid())::text || '/[0-9a-f]{64}[.](csv|pdf)$')
);

create policy "Users can update their own statements"
on storage.objects for update
to authenticated
using (
    bucket_id = 'statements'
    and owner_id = (select auth.uid())::text
    and name ~ ('^' || (select auth.uid())::text || '/[0-9a-f]{64}[.](csv|pdf)$')
)
with check (
    bucket_id = 'statements'
    and owner_id = (select auth.uid())::text
    and name ~ ('^' || (select auth.uid())::text || '/[0-9a-f]{64}[.](csv|pdf)$')
);

create policy "Users can delete their own statements"
on storage.objects for delete
to authenticated
using (
    bucket_id = 'statements'
    and owner_id = (select auth.uid())::text
    and name ~ ('^' || (select auth.uid())::text || '/[0-9a-f]{64}[.](csv|pdf)$')
);

commit;
