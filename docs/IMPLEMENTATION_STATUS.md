# Implementation status

Updated: 13 October 2026

## Milestone reached

A working local organizer ledger connects the .NET API and PostgreSQL to the React Native app. It can be exercised in Expo's browser preview. This milestone covers the local foundation and core workflows from phases 1–3; it does not complete those phases' deployment, device, or pilot requirements.

| Area | Delivered locally | Outstanding |
| --- | --- | --- |
| Foundation | .NET 10 API, EF migrations, PostgreSQL Compose, generated OpenAPI types, typed client, Dockerfile, CI definition | Staging deployment, production provider selection, hosted CI run, deployment/rollback validation |
| Identity | Development profiles, JWT validation, owner isolation, production configuration guards; mobile OIDC/PKCE, refresh and SecureStore code | Real provider registration, end-to-end sign-in/recovery and device verification |
| Groups | Draft creation, adding draft members, fixed order, activation and monthly schedule | Editing/removing/reordering draft members, existing-group import, group closure |
| Ledger | Full/partial payments, overdue status, reversals, audit entries, partial payouts and funding notes | Payout correction, cycle closing/reopening rules, full history pagination |
| Reliability | Transactions, integer paise, operation IDs, payload-checked retries, optimistic concurrency, consistent reads | Durable mobile queue, offline cache, reconciliation UX and restart recovery |
| App | Expo Router, English/Marathi, group list, setup, checklist, history, payout schedule | Native device testing, language review, accessibility review, production app assets |
| Member access | Not implemented | Private read-only web links, revocation, privacy-safe view, abuse controls |
| Messaging/exports | Not implemented | Tap-to-send WhatsApp reminders and monthly PDF/image sharing |
| Operations/billing | Not implemented | Backup/restore drill, monitoring, retention/deletion workflows; pricing and billing after pilot |

## Verification completed

- 22 backend tests pass against PostgreSQL, including API authentication and organizer isolation, schedules, partial contributions, corrections, conflicting writes, idempotency, and partial payouts.
- 14 mobile unit tests pass for exact paise conversion, dates, API errors, and retry behavior. These are not component or native end-to-end tests.
- Mobile ESLint and TypeScript checks pass.
- Expo Doctor passes all 21 compatibility/configuration checks.
- Android JavaScript/assets export succeeds. No APK/AAB was compiled, installed, or tested on a device. No iOS build was performed.
- Browser workflow checked at a 390×844 viewport: create and activate a fictional five-member group, record full and partial contributions, reverse an entry while retaining its history, record a partial payout, inspect balances, and switch English/Marathi.
- Both database migrations applied locally; records survived an API restart.
- .NET vulnerability scan reports no known vulnerable packages from configured sources.

## Release issues

The current mobile lockfile audit reports **28 affected packages: 18 high and 10 moderate**. Underlying advisories involve `braces`, `node-forge`, `decode-uri-component`, and `uuid`, with affected parent dependencies in Expo/React Native tooling and packages. Compatibility checks passing does not clear these advisories. Complete an exposure assessment and resolve or explicitly evaluate each advisory before release. A forced SDK downgrade was not applied.

Other release gates: complete real-provider authentication tests; verify HTTPS and trusted proxy settings; implement and exercise backups/restores; run native device journeys and restart/failure scenarios; review privacy/retention and pilot arrangements; obtain the external legal review specified in phase 0. No public or production deployment has been made.

## Next development sequence

1. Finish core ledger controls: draft editing/order changes, payout corrections, cycle close/reopen rules, and full audit pagination. Specify existing-group import with pilot organizers before implementing opening balances.
2. Add private member sharing with revocable scoped links, redacted records, and a read-only web view.
3. Add SQLite caching and a durable payment-operation queue; verify reconnect, concurrent edits, uncertain responses, and app restarts.
4. Add WhatsApp reminder composition, monthly exports, and backup/restore procedures.
5. Configure the selected OIDC provider and staging deployment, resolve dependency issues, then run a controlled Android pilot.

Interviews, real registers, identity provider credentials, deployment accounts, and legal review remain external inputs. Development can continue on the independent features above.
