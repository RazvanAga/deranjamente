# Slice 9 — CI/CD + deploy to Hetzner (HITL)

## Parent
#1

## What to build
Continuous delivery to production. HITL because it requires infra provisioning, DNS, and secrets that a human must set up. Recommended to do right after #2 (slice 1) so the walking skeleton deploys from the start.

Provision a Hetzner VPS with Docker + Docker Compose, nginx reverse proxy + SSL, and DNS for the domain. Create a `.env` on the VPS (chmod 600, not in git) for app secrets (Postgres password, Sentry DSN, Hangfire Basic-Auth creds). Set up a GitHub Actions pipeline: on push to main → run xUnit (unit + Testcontainers integration) → build Docker images (api+worker, web) → push to **GHCR** → SSH to the VPS → run the **EF migration bundle** as a discrete gated step before the app starts → `docker compose pull && up -d`. App does NOT auto-migrate. API + Hangfire run as one deployable; exactly one scheduler instance. Brief (~seconds) downtime on deploy is acceptable.

## Acceptance criteria
- [ ] Hetzner VPS provisioned (Docker + Compose); nginx + SSL; DNS pointed at it
- [ ] App secrets in a chmod-600 `.env` on the VPS (not in git); deploy secrets (SSH key, GHCR token) in GitHub Actions secrets
- [ ] GitHub Actions: tests run, images build and push to GHCR on push to main
- [ ] Deploy step SSHes to the VPS, runs the EF migration bundle (gated, before app start), then `compose pull && up -d`
- [ ] App does not auto-migrate on boot; exactly one Hangfire scheduler instance runs
- [ ] The app is reachable live over HTTPS at the domain
- [ ] After implementation, create a commit with a descriptive message

## Blocked by
- #2
