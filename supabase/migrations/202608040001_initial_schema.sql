create table if not exists public.user_backups (
    user_id uuid primary key references auth.users(id) on delete cascade,
    payload jsonb not null default '{}'::jsonb,
    updated_at timestamptz not null default now()
);

alter table public.user_backups enable row level security;

create policy "Users can read their own backup"
on public.user_backups for select
to authenticated
using ((select auth.uid()) = user_id);

create policy "Users can create their own backup"
on public.user_backups for insert
to authenticated
with check ((select auth.uid()) = user_id);

create policy "Users can update their own backup"
on public.user_backups for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
    'statements',
    'statements',
    false,
    10485760,
    array['text/csv', 'text/plain', 'application/pdf', 'application/vnd.ms-excel']
)
on conflict (id) do update set
    public = excluded.public,
    file_size_limit = excluded.file_size_limit,
    allowed_mime_types = excluded.allowed_mime_types;

create policy "Users can read their own statements"
on storage.objects for select
to authenticated
using (
    bucket_id = 'statements'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);

create policy "Users can upload their own statements"
on storage.objects for insert
to authenticated
with check (
    bucket_id = 'statements'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);

create policy "Users can update their own statements"
on storage.objects for update
to authenticated
using (
    bucket_id = 'statements'
    and (storage.foldername(name))[1] = (select auth.uid())::text
)
with check (
    bucket_id = 'statements'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);

create policy "Users can delete their own statements"
on storage.objects for delete
to authenticated
using (
    bucket_id = 'statements'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);
