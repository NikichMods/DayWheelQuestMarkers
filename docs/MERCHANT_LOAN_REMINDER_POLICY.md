# Merchant loan reminder policy — 1.1.15 candidate

## Adopted scope

On 2026-10-17 the player approved excluding only the Merchant's optional loan offer from calendar reminders. Repayment of an existing debt and every other optional dialogue remain unchanged. Stable remains 1.1.14 until explicit player acceptance of a numbered candidate.

## Evidence and cause

Read-only inspection of the reported GK 1.407 save, session log, installed 1.1.14 DLL and actual schema-7 manifest established:

- only `merchant_garden` and `merchant_grass` were Visible for the Merchant; required crops and hiccup grass were absent;
- `@merchant_2e_1e` (loan offer) and its parent `@merchant_2e` were unlocked and not blacklisted;
- the offer had no resource gates; declining left it available;
- no `merchant_debt` task had been created;
- the installed DLL matched the accepted 1.1.14 SHA-256;
- the accepted lifecycle census classifies `@merchant_2e_1e` as an independent ancestor owner because accepting through `merchant_2e_1d_4a` persistently consumes it.

This was valid structural classification with an unwanted product result, not evidence of broken item sufficiency checks. The raw save, host payloads and broad logs remain private research inputs.

## Mechanism and readiness

Gate: **READY** before production mutation, based on the inspected emission loop, accepted census, concrete saved state and loaded manifest.

- Observable change: the optional offer alone contributes zero markers.
- Owner/final consumer: `CalendarQuestsPinsPlugin.Tick` adds a Base marker for every actionable dialogue topic, then `CalendarMarkers.ApplyMarkerSets` renders the resulting per-weekday lists.
- Narrow mechanism: skip exactly `(npc_merchant, @merchant_2e_1e)` in that dialogue emission loop through `ShouldRemindDialogueTopic`.
- Blast radius: this policy has one production caller, the dialogue-topic emission loop. Owner-task and cross-owner loops are untouched.
- Preserved invariants: `merchant_debt / @merchant_2e_1f` continues through the existing task path with its authored money/phrase/navigation gates; all other dialogue owners, resources, cache integrity, HUD lifecycle and gameplay remain unchanged.
- Acceptance: compiled policy regression across the 60 accepted dialogue owners must exclude exactly one; the unchanged 103-check interaction validator must pass. Installed-game smoke must show no Merchant marker in the preserved no-items/no-debt state while the offer remains available. Player acceptance is still required.

Filtering in the compiler would change the structural census and invalidate existing manifests for a presentation exception. A post-validation emission policy is smaller and works with both existing and newly generated schema-7 manifests. The underlying 60-owner census and fixtures remain valid; effective dialogue reminder policy admits 59 owners. No translated-text matching, schema bump, graph rebuild, configuration option, or general exclusion of optional content is introduced.

## Verification boundaries

The compiled regression executes the shipped policy against every unique `ADMIT` owner in the independent lifecycle fixture and checks repayment/nearby identities remain allowed. This proves the policy boundary, not native live affordability or rendered HUD behavior. Existing accepted repayment rules are preserved byte-for-byte; no claim of a new in-game repayment test is made.

Candidate identity and installed/player results belong in `TEST_BUILD_LOG.md`.
