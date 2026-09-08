create table if not exists public.devices (
  device_id text primary key,
  device_name text not null,
  current_video integer,
  is_playing boolean not null default false,
  fps real,
  error_message text not null default '',
  desired_video integer,
  command_updated_at timestamptz,
  last_seen timestamptz not null default now()
);

alter table public.devices enable row level security;
alter table public.devices add column if not exists desired_video integer;
alter table public.devices add column if not exists command_updated_at timestamptz;

