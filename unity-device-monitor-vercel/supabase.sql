create table if not exists public.devices (
  device_id text primary key,
  device_name text not null,
  current_video integer,
  is_playing boolean not null default false,
  fps real,
  error_message text not null default '',
  desired_video integer,
  desired_action text,
  command_payload jsonb not null default '{}'::jsonb,
  command_updated_at timestamptz,
  project_id text not null default 'video-demo',
  project_name text not null default '视频切换 Demo',
  app_version text,
  scene_name text,
  status jsonb not null default '{}'::jsonb,
  last_seen timestamptz not null default now()
);
alter table public.devices enable row level security;

alter table public.devices add column if not exists desired_video integer;
alter table public.devices add column if not exists command_updated_at timestamptz;
alter table public.devices add column if not exists desired_action text;
alter table public.devices add column if not exists command_payload jsonb not null default '{}'::jsonb;
alter table public.devices add column if not exists project_id text not null default 'video-demo';
alter table public.devices add column if not exists project_name text not null default '视频切换 Demo';
alter table public.devices add column if not exists app_version text;
alter table public.devices add column if not exists scene_name text;
alter table public.devices add column if not exists status jsonb not null default '{}'::jsonb;

create index if not exists devices_project_id_idx on public.devices(project_id);
create index if not exists devices_last_seen_idx on public.devices(last_seen desc);
