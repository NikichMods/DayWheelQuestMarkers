# MultipleAnswer gate probe 0.1.0

Status: **research-only / read-only / one-shot**.

## Trigger

Player runtime on 2026-09-22 showed Snake's root dialogue rendering the Better Save Soul answer `@souls_s_s33_ask` ("Рассказать о слухах...") while Day Wheel Quest Markers 1.1.6 rendered no Snake marker for that interaction.

Accepted 1.1.6 already knows the exact route:

- NPC: `npc_cultist`
- task: `dlc_souls_s29_3`
- answer: `@souls_s_s33_ask`
- root multi-answer node: `106`
- answer index: `29`
- lifecycle owner: self
- route kind: selectable task-owned
- navigation: supported

The remaining known gap is its final AnswerData gate: source node `2644` is the sole unsupported persisted topic variant in the accepted 1.1.6 universe.

## Question

Determine, without guessing or mutating gameplay state:

1. the exact runtime type of source node `2644`;
2. the exact runtime value connected to multi `106`, answer index `29`;
3. whether it is `MultipleAnswerData`;
4. its nested authored gate structure;
5. whether the game's own `MultipleAnswerData.FillVisualData(ref MultipleAnswerVisualData, WorldGameObject)` can evaluate the current gate outside an actual dialogue invocation;
6. whether that evaluation leaves the authored gate object structurally unchanged.

## Probe behavior

The probe runs once after a save has fully loaded. It targets only `npc_cultist / 106 / 29 / @souls_s_s33_ask`.

It logs:

- current `dlc_souls_s29_3` task state;
- exact serialized connection into answer input #29;
- a bounded serialized context around source node `2644`;
- runtime graph node count and exact runtime types for nodes `106` and `2644`;
- exact authored answer identity at index 29;
- exact input-port connection and resolved `ValueInput.value`;
- a bounded field dump of the resolved AnswerData object;
- whether the value is `MultipleAnswerData`;
- one game-native `FillVisualData` evaluation into a temporary visual-data object;
- resulting visual data including `can_be_picked` if exposed;
- a before/after structural fingerprint of the authored gate object.

The probe does not set task state, phrase state, inventory, NPC/world parameters, save fields, or UI state. It disables itself immediately after the one snapshot.

## Frozen build

- exact source: `0d95db252aa2dca7427faac858ab7d48cac5341c`
- frozen ref: `frozen/multiple-answer-gate-probe-0.1.0`
- CI run: `35776671552`
- job: `106911742067`
- result: **success, 0 warnings / 0 errors**
- artifact: `MultipleAnswerGateProbe-0.1.0`, ID `10715998365`
- raw DLL: **24,576 bytes**
- SHA-256: `375aae6e9c6b0a38057588614a0fbad483a4768865281aeb8fc4ae579fbdd0d2`

Production 1.1.6 is unchanged.

## Requested runtime evidence

Keep accepted production 1.1.6, install this probe beside it, load the same save, wait until normal gameplay appears, then exit and return `BepInEx/LogOutput.log`.

No Snake dialogue needs to be opened: the probe performs the exact read-only snapshot automatically after load.
