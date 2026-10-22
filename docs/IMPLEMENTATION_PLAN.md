# BisiTracker implementation plan

Prepared: 13 October 2026
Status: Local foundation, core organizer workflows, and ledger controls implemented, including draft edits, payout reversals, cycle close/reopen, and full history paging. See [implementation status](IMPLEMENTATION_STATUS.md) for validation, release issues, and remaining work. The complete phased MVP is still in progress.

## Product outcome

An organizer can record monthly contributions and payouts, correct mistakes with a visible history, and share a current ledger that members can open without installing an app. The product records money movements performed outside the app.

The first release targets existing, small, fixed-contribution groups. Pilot assumptions: Android first; one organizer per group; INR; monthly cycles; one participation slot per member; English and one local language. Use Marathi as the initial local-language assumption and confirm it with pilot organizers. React Native should retain iOS compatibility, with iOS release work after pilot validation.

## Architecture decisions

| Area | Proposed choice | Reason |
| --- | --- | --- |
| Backend | ASP.NET Core on .NET 10 LTS; latest supported patch | Supported platform for a new API |
| Structure | One deployable backend organized by feature | Straightforward development and low operating overhead |
| Database | PostgreSQL with Entity Framework Core | Transactions, constraints, migrations and managed backups |
| Mobile | React Native, TypeScript, Expo development builds | Shared mobile code and practical Android delivery |
| Offline storage | SQLite on device with a durable pending-operation queue | Records remain accessible and new payment entries survive restarts |
| Member web view | Responsive ASP.NET Core Razor Pages in the backend application | Browser access with one deployment and shared authorization rules |
| API contract | REST, OpenAPI and a generated TypeScript client | Keep mobile requests aligned with backend contracts |
| Organizer authentication | Standards-based OIDC provider selected in Phase 0 | Recoverable accounts; avoid implementing identity infrastructure |
| Hosting | Containerized API and managed PostgreSQL; provider chosen in Phase 1 | Separate staging and production, automated backups and predictable costs |
| Quality | Backend unit/integration tests, mobile component tests and selected device journeys | Focus checks on money records, access and recovery |

Pin compatible stable Expo/React Native versions when scaffolding. Store credentials in secure device storage, not SQLite or ordinary preferences. Use SQLite for mobile records; the member website reads server-confirmed data.

Proposed repository layout:

```text
backend/                 ASP.NET Core application and backend tests
mobile/                  React Native application and mobile tests
contracts/               OpenAPI schema and generated TypeScript client
infra/                   Local services, deployment and operational configuration
docs/                    Product rules, architecture and runbooks
```

## MVP boundaries

Include group creation, member names, fixed monthly contributions, fixed payout order or a manually recorded recipient, full and partial payments, overdue indicators, actual payout recording, history, member sharing, tap-to-send WhatsApp reminders, monthly image/PDF export, backups and bounded offline use.

Defer in-app lucky draws, auctions, bidding, interest, automatic penalties, money collection, payment gateways for group contributions, automatic payment verification, multiple shares, mid-cycle member replacement, co-organizers, automated SMS/WhatsApp delivery and subscription billing.

Initial offline scope: browse previously synced groups and queue new payment entries. Group setup, corrections, closing cycles, payout confirmation and sharing changes require connectivity. Expand offline mutations only after this scope is reliable.

## Phase 0 — Confirm pilot rules and product scope

Indicative effort: 2–3 working days for specification; interviews and external review may take longer.

- Recruit five pilot organizers and inspect real registers and monthly messages.
- Confirm group sizes, payment methods, due dates, payout arrangements and language.
- Define how an already-running group is imported, including opening balances, historical payments and previous recipients.
- Define monthly due-date behavior, late status, advance payments, corrections, shortfalls and cycle closing.
- For the first version, restrict the due day to 1–28; allow partial payments and allocate each entry explicitly to a cycle.
- Select an organizer sign-in method that pilot users can recover after changing phones; defer mandatory SMS OTP unless interviews establish a need.
- Document privacy defaults, account deletion/retention expectations and what members may see.
- Obtain review of the target arrangements and launch state from a qualified lawyer before public launch. Record-keeping language alone does not settle classification.

Deliverables: product brief, business rules, sample monthly ledger, initial screen flows and pilot recruitment list.

Exit gate: a sample group can be explained from setup through its final payout, including a partial payment, a late payment and a corrected entry. Legal review can run alongside development but remains a public-launch dependency.

## Phase 1 — Project foundation and data contracts

Indicative effort: 3–5 working days. Depends on Phase 0 rules.

- Scaffold .NET backend, React Native app, PostgreSQL local environment and documented startup commands.
- Configure sign-in, session expiry, secure credential storage and account recovery.
- Add organizer ownership checks to every private group operation.
- Establish database migrations, request validation, structured errors and OpenAPI generation.
- Define money, identifiers, operation IDs, record versions, pagination and sync cursor contracts before implementing mobile writes.
- Add build/type-check/test automation, secret handling, health checks and a staging deployment.
- Set up translation keys and accessible components from the beginning.

Deliverables: signed-in mobile shell, working staging API, initial database migration and API contract.

Exit gate: an organizer can sign in from Android, call the API, and recover access; a second organizer cannot access the first organizer's data.

## Phase 2 — Groups, cycles and the authoritative ledger

Indicative effort: 5–7 working days. Depends on Phase 1.

- Build group draft/activation, members, contribution rules, monthly cycles and payout ordering.
- Build payment recording, partial-payment allocation, correction/reversal and history endpoints.
- Build separate planned and actual payout records, including recipient and paid date.
- Calculate expected contributions, contributions recorded, outstanding dues and payout shortfall separately.
- Enforce one participation slot per member and one planned recipient per cycle. Prevent accidental repeat recipients within a round.
- Freeze contribution and participation rules when a round activates; preserve historical member and rule information.
- Support a guided import of existing records, with opening amounts clearly labeled and audited.
- Require an explicit explanation for a recorded payout above contributions recorded for that cycle; show the funding gap. The app cannot verify external cash availability.

Deliverables: complete ledger API and realistic seeded examples.

Exit gate: automated tests cover partial payments, reversals, repeated requests, concurrent changes, unauthorized access, imported history and underfunded payouts. A transaction commits the financial entry and its audit event together.

## Phase 3 — Organizer app and monthly workflow

Indicative effort: 7–10 working days. Depends on Phase 2 contracts.

- Build onboarding, group list, group setup and bulk member-name entry.
- Build the monthly ledger with totals and paid, partial, pending and overdue indicators.
- Add quick payment entry with amount, effective payment date, method and optional reference/note.
- Add member history, correction reasons, payout schedule and actual payout recording.
- Add end-of-cycle review and a clear transition to the next cycle.
- Translate the core workflow into the pilot language and verify Indian currency/date formatting.
- Test large text, screen-reader labels and status indicators that do not rely only on color.

Deliverables: installable Android development/beta build that completes the core workflow online.

Exit gate: a pilot organizer can create a sample group, record partial/full payments, correct one mistake and record a payout without developer assistance.

## Phase 4 — Shared member view and offline reliability

Indicative effort: 5–7 working days. Depends on Phases 2 and 3.

- Build a mobile-friendly browser ledger with monthly records, payout schedule, correction history and a server update timestamp.
- Add high-entropy, revocable sharing links with optional PIN protection and expiration settings.
- Store share-token hashes server-side; redact tokens and financial details from logs and analytics. Rate-limit link/PIN attempts.
- Hide phone numbers, private notes, references and account identifiers from shared responses; prevent search indexing and inappropriate caching.
- Add the SQLite cache and durable queue for new payment entries.
- Distinguish saved-on-device, awaiting-sync, synced, rejected and conflict states.
- Replay operations with stable operation IDs. Server retries must return the original result without duplicating money entries.
- Handle concurrent payment entries that over-allocate a due as an explicit conflict; do not silently overwrite records.
- Pull changes with a durable server cursor and make local records recoverable after app restart or token expiry.

Deliverables: browser member link and offline-capable Android beta.

Exit gate: members open links without installation; revoked links stop working; airplane-mode entries survive restart and reconnect exactly once. Stale edits and lost responses do not silently change balances.

## Phase 5 — Reminders, monthly exports and recovery

Indicative effort: 3–5 working days. Depends on Phase 4.

- Compose polite WhatsApp reminders using recorded outstanding amounts and the organizer's selected language.
- Open the native share/WhatsApp flow for the organizer to send. Label this as tap-to-send; opening WhatsApp is not proof of message delivery.
- Export a legible monthly image and PDF with group, cycle, totals, recipient, generation time and record version.
- Generate official shared exports from server-confirmed records; explain when pending offline entries require sync first.
- Support CSV/JSON data export and a documented restore path.
- Configure managed database backups and perform a real restore into a separate environment.
- Define initial recovery targets, retention and backup-access restrictions. Proposed targets: at most 24 hours of committed-data loss and restoration within one working day; validate the chosen provider against these targets.

Deliverables: monthly sharing workflow and a tested recovery runbook.

Exit gate: exports match the displayed confirmed ledger, local-language text renders correctly, reminder amounts are correct, and restored records reconcile with the backup snapshot. Unsynced device-only entries are explicitly outside server backup coverage.

## Phase 6 — Hardening and controlled pilot

Indicative effort: 5–7 working days for readiness, followed by two real monthly collection cycles.

- Test on representative lower-cost Android devices and intermittent networks.
- Complete account/group access checks, share-link abuse checks, dependency review and migration/recovery checks.
- Add crash reporting and operational alerts without sending names, phone numbers or financial records to analytics.
- Complete privacy/terms content, support contact, organizer onboarding and legal launch review.
- Onboard five real groups with explicit agreement about which records members can see.
- Track activation, repeat monthly usage, sharing, unresolved discrepancies and organizer reconciliation time.
- Review issues after each collection cycle and fix record correctness before adding features.

Deliverables: pilot release, issue log, usage findings and a release decision.

Exit gate: no known critical record-loss/access defect; backup restore verified; suggested adoption targets are four of five organizers returning in cycle two and three sharing the ledger. Measure actual willingness to pay, aiming for at least two paid commitments or purchases through a reviewed sales process.

## Phase 7 — Monetization and expansion after validation

Indicative effort: estimate after pilot findings.

- Keep one small group and basic sharing/export free; set the size limit using pilot data.
- Test annual per-group prices such as INR 199 and INR 399 as hypotheses.
- Charge for additional active groups and selected convenience features; keep data retrieval available when a plan expires.
- Implement entitlements and subscription billing only after reviewing applicable mobile-store billing rules and the chosen sales channel.
- Track per-group hosting, support and messaging cost before offering paid message allowances.
- Consider automated reminders with recipient consent, delivery tracking and opt-out handling.
- Expand to Hindi/Gujarati, iOS, co-organizers and more flexible group rules based on demonstrated demand.
- Evaluate in-app draw functionality separately after product and legal review.

Exit gate: paid users renew or continue using the product, support costs are understood, and additional scope is justified by measured demand.

## Core data model and invariants

| Entity | Purpose |
| --- | --- |
| Organizer | Authenticated owner with preferences and recoverable identity |
| Group | Name, owner, currency, time zone, language and lifecycle |
| Round | Frozen contribution rules, start date and ordered monthly cycles |
| Membership | Member's participation within a round; historical identity preserved |
| Cycle | Due date, expected contributions, planned recipient and status |
| ContributionDue | Expected obligation for one membership in one cycle |
| PaymentEntry / Allocation | Recorded receipt and amount allocated to a due |
| PayoutEntry | Actual payout, distinct from the planned entitlement |
| AuditEvent | Actor, operation, recorded time and correction reason |
| ShareAccess | Hashed token, permitted view, expiry/revocation and optional PIN hash |
| ProcessedOperation | Idempotency result for a device-submitted operation |

- Store money as integer paise with currency; use compatible safe-integer bounds in .NET, PostgreSQL and JavaScript.
- Store event timestamps in UTC; evaluate due dates in the group's time zone, initially Asia/Kolkata.
- Keep effective payment date separate from server recording time.
- Derive outstanding and payment status from posted entries. Overdue means an outstanding amount remains after the due date.
- Use server-side transactions, authorization, constraints and version checks regardless of client validation.
- Apply corrections using linked reversal/replacement entries. Preserve original financial records and reasons through the application workflow.
- Distinguish operational audit history from tamper-proof proof: the app must not claim database administrators cannot alter records.
- Treat shared views and exports as organizer-recorded information, not bank-verified payment confirmations.
- Retain closed-cycle history; an authorized reopening or correction must be visible and update derived totals.
- Keep billing for use of BisiTracker separate from group contribution records.

## Delivery estimate and first implementation milestone

Planning estimate: approximately 8–12 weeks to a reliable pilot-ready Android release for one experienced full-time developer, with contingency for sync, localization, identity integration and device testing. This is an estimate, not a deadline. External legal review, pilot recruitment and store review can change the calendar. Two monthly pilot cycles add roughly two months of observation, during which fixes continue.

The first implementation milestone is Phase 1 plus a small Phase 2/3 vertical slice: sign in, create a draft group, add members, activate the first cycle, record a contribution and reload the same correct ledger from the backend.

## Reference documentation

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core): .NET 10 is an LTS release; use the current supported patch.
- [Expo SDK reference](https://docs.expo.dev/versions/latest/): select and pin a compatible stable mobile toolchain at implementation time.
- [Expo SQLite](https://docs.expo.dev/versions/latest/sdk/sqlite/): local mobile database capabilities; synchronization is application work.
- [Chit Funds Act, official published text](https://www.indiacode.nic.in/bitstream/123456789/1797/1/A1982-40.pdf): background for qualified review; verify current central/state requirements for the actual launch scope.
