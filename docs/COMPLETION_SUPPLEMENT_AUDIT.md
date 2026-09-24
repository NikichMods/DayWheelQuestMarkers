# Residual VerifiedCompletionReminderRules Audit — 2026-09-23

Status: **Point 3 evidence complete; implementation candidate justified; production `main` unchanged by this research branch**.

## Question

Audit every remaining answer-backed case in `VerifiedCompletionReminderRules` and determine whether it is genuinely a distinct host mechanism or only compensates for a bounded topology gap in the generic owner-task reverse walker.

The product rule remains:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`.

The goal is not “one algorithm at any cost”. A special case should be removed only when a narrower generic structural rule can represent the same authored semantics without broad provenance parsing or new runtime work.

## Current residual layer in accepted 1.1.9

`VerifiedCompletionReminderRules` contains three categories:

1. five promoted task/topic pairs:
   - `npc_astrologer / dlc_souls_s29_1 -> @souls_s_s30_ask`;
   - `npc_cultist / snake_key -> @snake_give_key`;
   - `npc_cultist / dlc_souls_s29_3 -> @souls_s_s33_ask`;
   - `npc_actress / dlc_souls_s29_2 -> @souls_s_s31_ask`;
   - `npc_bishop / bishop_rcitezen -> @bishop_get_citezen`;
2. one exact answer-backed relation route:
   - `npc_cultist / snake_trap -> snake_stone_ready`, gated by `GameRes:_rel >= 10`;
3. two mandatory event-only stages:
   - `npc_inquisitor / inquisitor_talk`;
   - `npc_cultist / snake_back`.

## Independent task-census evidence

The accepted exhaustive task snapshot proves:

`72 owner-local completion nodes = 70 SELECTABLE + 2 EVENT_ONLY`.

All six answer-backed residual cases are already in the independent 70-route SELECTABLE set:

- `dlc_souls_s29_1 -> @souls_s_s30_ask`;
- `snake_key -> @snake_give_key`;
- `dlc_souls_s29_3 -> @souls_s_s33_ask`;
- `dlc_souls_s29_2 -> @souls_s_s31_ask`;
- `bishop_rcitezen -> @bishop_get_citezen`;
- `snake_trap -> snake_stone_ready`.

Therefore none of these six is semantically “special”. The supplement exists because accepted production 1.1.9 does not yet model three verified local flow-edge types used by those completion routes.

## The three bounded missing topology edges

### 1. Numbered `Flow_WaitForFlow` inputs are flow edges

Probe 0.1.0 stopped on three ordinary selectable completion routes because the reverse walker treated numbered `Flow_WaitForFlow` target ports as value/non-flow inputs.

Probe 0.1.1 verified the bounded correction:

- normal flow input: target port `In` or empty;
- additionally, when the target node is `Flow_WaitForFlow`, a purely numeric target port is an authored flow input.

This recovers:

- Astrologer `dlc_souls_s29_1 -> @souls_s_s30_ask`;
- Snake `dlc_souls_s29_3 -> @souls_s_s33_ask`;
- Ms. Charm `dlc_souls_s29_2 -> @souls_s_s31_ask`.

### 2. Exact CustomFunction UID linkage is a local flow edge

The Bishop route does not safely match by human-readable identifier spelling.

Probe 0.1.1 verified the exact serialized linkage:

`CustomFunctionCall._sourceOutputUID == CustomFunctionEvent._UID`.

Using only that exact same-graph UID equality recovers:

- Bishop `bishop_rcitezen -> @bishop_get_citezen`.

No display text or fuzzy/name normalization is required.

### 3. Same-graph `Flow_FireEvent(X) -> CustomEvent(X)` is an immediate flow edge

Probe 0.1.2 established two Snake completion routes where the selected answer fires a named event and the same graph's matching `CustomEvent` continues the same immediate interaction.

The accepted local relation is:

`Flow_FireEvent.event == CustomEvent.eventName._value`

within the same weekday-NPC serialized graph.

This recovers:

- `snake_key -> @snake_give_key` (after the exact CustomFunction UID hop);
- `snake_trap -> snake_stone_ready`.

This does **not** admit arbitrary `CustomEvent` roots. It only supplies an exact same-graph edge from a concrete `Flow_FireEvent` sender to matching receiver(s).

## Why the six special cases can be removed

The independent task snapshot was generated with exactly the three topology refinements above and produced:

- 72 owner-local completion nodes;
- 70 mapped selectable completion routes;
- 2 remaining event/non-selectable stages.

No additional semantic heuristic was needed.

The six current answer-backed special cases therefore represent missing graph connectivity, not six exceptional gameplay rules.

Once the owner-task compiler uses those verified local edges:

- the generic task compiler can derive the actual answer ID;
- ordinary `Flow_Answer` / `MultipleAnswerData` gates are compiled by the existing rule builder;
- normal navigation reachability still applies;
- the five `@` completion answers become task-owned and no longer need to survive as standalone topic rules solely for the supplement;
- `snake_trap` can use its authored `GameRes:_rel >= 10` answer gate through the normal game-owned SmartRes path instead of creating a second SmartRes in `VerifiedCompletionReminderRules`.

## Narrow implementation boundary

The production implementation should be deliberately narrower than the research snapshot:

- enhance **owner-local task completion derivation only**;
- keep cross-owner derivation on the already accepted topology unless separately audited;
- keep dialogue-lifecycle derivation unchanged;
- treat numbered `Flow_WaitForFlow` inputs as flow only for the enhanced owner-task reverse map;
- add exact same-graph CustomFunction UID links only to that owner-task reverse map;
- add exact same-graph FireEvent-to-CustomEvent links only to that owner-task reverse map;
- preserve current navigation and SmartRes evaluation;
- keep all graph work loading/bootstrap-only.

This prevents the new proven edges from accidentally broadening unrelated cross-owner or lifecycle semantics.

## Cases that must remain separate

### `npc_inquisitor / inquisitor_talk`

Runtime evidence proves the visible task is completed by a mandatory scripted interaction before a selectable answer menu represents the stage.

### `npc_cultist / snake_back`

Runtime/authored evidence proves the preceding sword hand-in installs a later `Flow_AddInteractionEvent`; once `snake_back` becomes Visible, the next required visit is represented by that later interaction event, not an immediate selectable-answer route.

These are not missing immediate graph edges and should **not** be forced through the selectable task compiler.

## Point 3 conclusion

**The residual answer-backed supplement can be eliminated.**

After the bounded task-topology change, `VerifiedCompletionReminderRules` should retain only the two verified mandatory event-only stages:

- `npc_inquisitor/inquisitor_talk`;
- `npc_cultist/snake_back`.

The five promoted route mappings, the custom `snake_trap` SmartRes construction, and `IsPromotedCompletionTopic` compatibility hook should become unnecessary if static parity confirms the derived owner-task rules cover the exact six expected routes and no unrelated semantic layer changes.

## Validation required before any player DLL

1. interaction-universe validator passes;
2. owner-local task census remains exactly `72 = 70 selectable + 2 event-only`;
3. the six former answer-backed supplement routes are present as generic owner-task rules;
4. exact event-only whitelist remains two;
5. cross-owner and dialogue-lifecycle baselines remain unchanged;
6. navigation and compound-gate baselines remain unchanged;
7. no production reference remains to the five promoted mappings or the special `snake_trap` SmartRes path.

## Plan status

- Point 1 — adversarial negative audit: complete.
- Point 2 — Watchdog 0.3: intentionally cancelled.
- **Point 3 — residual `VerifiedCompletionReminderRules` audit: complete; bounded implementation justified.**
- Point 4 — final coverage audit: pending after implementation/parity validation.
