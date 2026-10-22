# Implementation status

Updated: 22 October 2026

## Milestone reached

A working local organizer ledger connects the .NET API and PostgreSQL to the React Native app. The second local milestone adds draft member editing/order, payout reversals, cycle closing/reopening, and complete audit history browsing. It can be exercised in Expo's browser preview. These milestones cover the local foundation and core workflows from phases 1–3; deployment, device, and pilot requirements remain open.

| Area | Delivered locally | Outstanding |
| --- | --- | --- |
| Foundation | .NET 10 API, EF migrations, PostgreSQL Compose, generated OpenAPI types, typed client, Dockerfile, CI definition | Staging deployment, production provider selection, hosted CI run, deployment/rollback validation |
| Identity | Development profiles, JWT validation, owner isolation, production configuration guards; mobile OIDC/PKCE, refresh and SecureStore code | Real provider registration, end-to-end sign-in/recovery and device verification |
| Groups | Draft creation, adding/renaming/removing/reordering draft members, fixed order, activation and monthly schedule | Existing-group import, whole-group/round completion |
| Ledger | Full/partial payments, overdue status, payment/payout reversals, partial payouts and funding notes, cycle closing/reopening, version-checked audit paging | Existing-record import and opening balances |
| Reliability | Transactions, integer paise, operation IDs, payload-checked retries, optimistic concurrency, consistent reads | Durable mobile queue, offline cache, reconciliation UX and restart recovery |
| App | Expo Router, English/Marathi, group list, setup, checklist, history, payout schedule | Native device testing, language review, accessibility review, production app assets |
| Member access | Not implemented | Private read-only web links, revocation, privacy-safe view, abuse controls |
| Messaging/exports | Not implemented | Tap-to-send WhatsApp reminders and monthly PDF/image sharing |
| Operations/billing | Not implemented | Backup/restore drill, monitoring, retention/deletion workflows; pricing and billing after pilot |

## Verification completed

- 33 backend tests pass against PostgreSQL, including organizer isolation, schedules, partial contributions, payment/payout reversals, draft reorder/removal, conflicting activation/edit and close/correction requests, idempotency, cycle locking/reopening, and complete history beyond 200 events.
- 16 mobile unit tests pass for exact paise conversion, dates, API errors, operation-preserving retries, and version-bound history requests. These are not component or native end-to-end tests.
- Mobile ESLint and TypeScript checks pass.
- The initial milestone passed all 21 Expo Doctor compatibility/configuration checks on 13 October; dependencies have not changed in this milestone.
- Android JavaScript/assets export succeeds. No APK/AAB was compiled, installed, or tested on a device. No iOS build was performed.
- Browser workflow checked at a 390×844 viewport: create and activate a fictional five-member group, record full and partial contributions, reverse an entry while retaining its history, record a partial payout, inspect balances, and switch English/Marathi.
- Additional browser checks: rename/remove/reorder draft members; activation retains the edited order; closing is blocked while unsettled; close a settled cycle; reopen with a reason; reverse/replace its payout; re-close it; inspect correction history and English/Marathi controls. The fictional two-member demo uses ₹10 contributions.
- History paging checked in the browser with 54 events, then 55 after an intervening write. All entries loaded; a stale page prompted refresh and recovered without mixing versions.
- All three migrations applied locally. `LedgerControls` adds nullable cycle closure time and a separate payout-reversal table, preserving existing payment/payout rows.
- The initial .NET vulnerability scan on 13 October reported no known vulnerable packages; no package versions changed in this milestone.

## Release issues

The 13 October mobile lockfile audit reported **28 affected packages: 18 high and 10 moderate**. Underlying advisories involve `braces`, `node-forge`, `decode-uri-component`, and `uuid`, with affected parent dependencies in Expo/React Native tooling and packages. The lockfile is unchanged in this milestone. Compatibility checks passing does not clear these advisories. Refresh the audit, complete an exposure assessment, and resolve or explicitly evaluate each advisory before release. A forced SDK downgrade was not applied.

Other release gates: complete real-provider authentication tests; verify HTTPS and trusted proxy settings; implement and exercise backups/restores; run native device journeys and restart/failure scenarios; review privacy/retention and pilot arrangements; obtain the external legal review specified in phase 0. No public or production deployment has been made.

## Next development sequence

1. Produce and test an installable Android development build. Specify existing-group import with pilot organizers before implementing opening balances.
2. Add private member sharing with revocable scoped links, redacted records, and a read-only web view.
3. Add SQLite caching and a durable payment-operation queue; verify reconnect, concurrent edits, uncertain responses, and app restarts.
4. Add WhatsApp reminder composition, monthly exports, and backup/restore procedures.
5. Configure the selected OIDC provider and staging deployment, resolve dependency issues, then run a controlled Android pilot.

Interviews, real registers, identity provider credentials, deployment accounts, and legal review remain external inputs. Development can continue on the independent features above.

See [ledger control rules](LEDGER_CONTROLS.md) for the delivered behavior and API details.
