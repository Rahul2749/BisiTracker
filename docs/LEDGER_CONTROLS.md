# Ledger controls

Implemented locally: 22 October 2026. The app records external payments; reversing a record does not reverse an actual money transfer.

## Draft members and payout order

Open a draft and select **Edit members & order**. Rename members, remove members, and use the up/down controls to set payout order. Saving applies the list in one transaction; canceling discards unsaved edits. Use **Add member** to introduce a new member. A draft may be empty, but activation requires 2–50 members.

Existing member IDs are preserved through renaming/reordering. Positions are normalized to 1 through the retained member count. Activation freezes the list and creates the schedule from that order. Concurrent edits or activation requests cannot combine different versions of the rules. The history records the before/after names and order.

API: `POST /api/groups/{id}/members/edit`, with `operationId`, `expectedVersion`, and `members: [{ id, name }]` in the desired order. Omitted existing members are removed from the draft. Unknown IDs, duplicate IDs, and blank names are rejected. Adding a member uses the existing `POST /members` endpoint.

## Correcting a payout

In **Payout schedule**, select **Reverse payout** on the incorrect entry and supply a reason. The original amount/date remain visible with the reason. Only unreversed entries count toward the actual payout. Reversing an entry restores the recipient's amount still owed, allowing a replacement entry. Contributions and the planned pool do not change.

A closed cycle must be reopened before correcting either a payment or a payout. A second reversal of the same entry is rejected; retrying the same successful operation ID returns its original result.

API: `POST /api/groups/{id}/payouts/{payoutId}/reverse`, with `operationId`, `expectedVersion`, and `reason` (1–300 trimmed characters).

## Closing and reopening a monthly cycle

Select **Close cycle** to review outstanding contributions and payout still owed. Closing requires both to be zero. A funding note does not waive outstanding contributions. This milestone does not support closing with unpaid balances, debt write-offs, or carrying balances into another month.

A closed cycle remains readable. New payments, payment reversals, new payouts, and payout reversals are rejected by the API until it is reopened. Other cycles retain their own state. **Next cycle** selects the following monthly sheet; it does not close or alter it.

Select **Reopen cycle** and supply a reason to allow corrections. Closing and reopening are audited. Reopening does not change balances. Closing again requires the records to be settled again. Closing all monthly cycles does not yet complete/archive the whole group.

APIs:

- `POST /api/groups/{id}/cycles/{cycleId}/close`: `operationId`, `expectedVersion`.
- `POST /api/groups/{id}/cycles/{cycleId}/reopen`: `operationId`, `expectedVersion`, `reason` (1–300 trimmed characters).

## Complete edit history

The ledger initially includes 50 events and `historyNextPage`. **Load older changes** loads the next 50 until the original group creation is reached. This is available on drafts and active groups. Sorting uses recorded time followed by event ID, including a stable order for events sharing a timestamp.

API: `GET /api/groups/{id}/history?page=2&pageSize=50&expectedVersion={ledgerVersion}` returns `items`, `nextPage`, and `version`. Standalone first-page reads may omit the version. Later pages require it; clients must retain the same page size while paging.

If a new mutation changes the ledger version, the API rejects the older-history request with `409 stale_version`. The app explains the change and offers refresh; it starts over with the new first page instead of appending incompatible results. Reads use a consistent database snapshot. All history endpoints enforce organizer ownership.

## Migration and verification

`20261021172635_LedgerControls` adds `Cycle.ClosedAt` and `PayoutReversal` with a unique payout reference. Existing cycles are open; existing payout rows remain valid. Development startup applies migrations. Production still requires a reviewed migration/deployment step.

The backend suite covers settlement checks, closed-cycle write rejection, required reasons, reversal retention, idempotent retries, organizer isolation, concurrent edits/activation and close/reversal races, and history beyond 200 events. Browser checks use fictional local groups; no money is moved. Android JavaScript/assets export is verified, but an installed native build remains the next milestone.
