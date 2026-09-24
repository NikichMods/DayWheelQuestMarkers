# Final Interaction Coverage Audit — 1.1.10 Candidate

Status: **Point 4 complete at the static/evidence layer; player/runtime acceptance pending**.

Candidate branch: `dev/1.1.10`.

Candidate source audited: `7e7b429ca9d8c9b6d10c7a236f546809f2589378`.

Validator evidence: workflow run `35907144881`, job `107337542683` — **PASS, 99 checks / 0 failed**.

## Purpose

This is the final coverage pass after:

1. adversarially re-checking every historical semantic-negative interaction decision;
2. cancelling Runtime Watchdog 0.3 as a redundant second classifier;
3. auditing and eliminating the six answer-backed `VerifiedCompletionReminderRules` exceptions through already verified local task topology.

The question here is not whether aggregate counts look plausible. The question is whether every bounded interaction surface used by CalendarQuestsPins has an explicit evidence partition with no unexplained remainder.

## 1. Raw authored interaction universe

The frozen GK 1.407 six-weekday-NPC snapshot contains:

- **482 total structural rows**;
- **243 answer occurrences**;
- **150 task-state mutations**;
- **66 custom events**;
- **19 AddInteractionEvent nodes**;
- **4 RemoveInteractionEvent nodes**.

All 243 answer occurrences are unique by NPC + MultiAnswer node + answer index + answer ID.

Across those occurrences there are **224 unique NPC + answer identities**.

## 2. Interaction-root coverage

The 224 unique NPC + answer identities partition exactly as:

`224 = 210 normal interaction-root answers + 14 event-invoked no-root answers`.

The sets are disjoint.

There is no third/unclassified set.

### Normal interaction-root side

- **210 unique answers**;
- **270 exact root-to-answer navigation paths**;
- **0 unsupported paths**;
- path indices are contiguous per NPC + answer.

### No-root side

Exactly **14** answers have no normal player interaction-root path.

All 14 are independently accounted for as `EVENT_INVOKED_NON_REMINDER`:

- Inquisitor `first_meet_under_mountains`: 7;
- Inquisitor `on_came_to_mountain_for_witch_burning`: 2;
- Inquisitor `inquisitor_after_dark_event`: 3;
- Snake `player_back_to_cultist`: 2.

This proves the old no-root frontier has no unexplained member.

## 3. Owner-task completion coverage

The complete owner-local task census is:

`72 completion nodes = 70 selectable routes + 2 event-only stages`.

The validator now stores all **72 exact task routes**, not only aggregate per-NPC counts.

The six routes formerly handled by answer-backed special rules are present in the independent SELECTABLE set:

- Astrologer `dlc_souls_s29_1 -> @souls_s_s30_ask`;
- Snake `snake_key -> @snake_give_key`;
- Snake `dlc_souls_s29_3 -> @souls_s_s33_ask`;
- Ms Charm `dlc_souls_s29_2 -> @souls_s_s31_ask`;
- Bishop `bishop_rcitezen -> @bishop_get_citezen`;
- Snake `snake_trap -> snake_stone_ready`.

1.1.10 derives them through the three exact same-graph topology relations already established by frozen research:

- numbered `Flow_WaitForFlow` inputs;
- exact `CustomFunctionCall._sourceOutputUID == CustomFunctionEvent._UID`;
- exact `Flow_FireEvent.event == CustomEvent.eventName._value`.

These edges are scoped to owner-local task completion derivation. Cross-owner and dialogue-lifecycle derivation remain on their previously accepted topology.

## 4. Residual event-only coverage

Exactly two owner-task stages remain outside selectable answer derivation:

- `npc_inquisitor / inquisitor_talk`;
- `npc_cultist / snake_back`.

Both have prior direct runtime/authored evidence for a required NPC visit represented by a scripted/event interaction rather than a selectable completion answer.

No other completion node remains outside the 70 selectable + 2 event-only partition.

## 5. Dialogue lifecycle coverage

Accepted lifecycle evidence remains unchanged:

- **216 path records**;
- **60 unique admitted owners**;
- **0 reversible admitted owners**;
- nearest persistent ancestor ownership and task-owned/same-visit suppression remain unchanged.

The 1.1.10 owner-task topology extension is deliberately not applied to the dialogue-lifecycle compiler.

## 6. Historical negative-decision audit

The separate adversarial audit corrected six stale Watchdog 0.2 negative labels.

Those six are already represented by production semantics. The remaining historical negative cases all have an explicit reason not to represent a separate weekday visit.

Therefore Watchdog 0.2's `UNKNOWN=0` is historical provenance only, not the semantic oracle, and Watchdog 0.3 remains intentionally cancelled.

## 7. Compound requirement coverage

Relay-backed `MultipleAnswerData` remains an exact **7-use** family across four weekday NPCs.

The accepted native semantics remain AND across all authored `d_lock` / `d_price` children through the game's own `WorldGameObject.IsEnough` path.

The 1.1.10 change does not add any item-specific compound-condition hard-code.

## 8. Persisted manifest compatibility

The derived rule content changes in 1.1.10:

- six former answer-backed specials become generic owner-task rule variants;
- five former promoted `@` completion topics cease to be base one-shot topics.

The candidate therefore uses **manifest schema 7**.

This is required: loading a schema-6 1.1.9 manifest after removing the supplement could otherwise omit the newly generic task rules.

Declared candidate canonical partition:

- owner supported: **87**;
- owner unsupported: **0**;
- cross tasks: **8**;
- cross supported: **6**;
- cross unsupported: **0**;
- base `@` topics: **50**;
- base `@` supported: **50**;
- base `@` unsupported: **0**.

The schema/count values still require live bootstrap confirmation because static CI cannot execute Graveyard Keeper graph loading.

## 9. Static validator result

Workflow run `35907144881`, job `107337542683`:

- **PASS**;
- **99 checks**;
- **0 failures**;
- no remaining coverage warning for task routes or navigation paths.

The validator now stores and checks:

- raw six-NPC interaction universe;
- exact normal-root/no-root partition;
- every navigation path;
- every owner-task completion route;
- lifecycle path census;
- exact no-root frontier;
- exact MultipleAnswerData family;
- source-level production contracts;
- exact two-stage event-only residual set.

## 10. Remaining runtime/build obligations

The evidence model has no unresolved structural frontier.

Static evidence cannot prove:

- that the new C# candidate compiles;
- that schema-7 bootstrap produces the declared 87/50 canonical counts inside GK;
- live `Player.IsEnough` behavior;
- save/task/phrase runtime state transitions;
- Unity HUD behavior.

Those are candidate acceptance gates, not missing interaction-universe research.

No new broad diagnostic is justified. If a live contradiction appears, use a targeted read-only diagnostic for that exact state.

## Point 4 conclusion

**Interaction coverage research is complete for the current Graveyard Keeper 1.407 six-weekday-NPC universe.**

There is no remaining known authored interaction class that requires another universal research subsystem.

The next step is candidate build/runtime acceptance of 1.1.10, not more interaction discovery.
