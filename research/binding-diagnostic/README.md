# Runtime binding diagnostic 0.1.0

Research only; not a production candidate or a new stable version. Based on the accepted 1.1.15 runtime (tested source `2dee8941d2fb6a8257dd7f6b048d6b256e7354d4`), with documentation base `915c09bf03368b1a4c242c239c6989d5a36f3f35`.

## Question and method checkpoint

Determine the exact false-return condition in `PersistentRuleManifest.Bind` or `IsRuntimeValid` on repeat loads. The accepted log proves early manifest preparation before RestoreScene and successful recovery, but does not identify the failed condition. Existing source/history does not resolve that live object identity. A normal repeat load with the stable build would produce the same uninformative warning.

This temporary build adds logging at the original rejection branches and snapshots of existing references at prewarm, final bind and successful restore. It executes the actual existing bind/recovery path, with the same check order and return values. It does not emulate the classifier, add Harmony hooks, look up/create fresh world objects for comparison, suppress warnings or attempt a fix.

## Boundaries

- Same plugin GUID as stable: this temporarily replaces the stable DLL; do not load both.
- Diagnostic plugin/assembly version: **0.1.0**; plugin name explicitly identifies the binding diagnostic.
- Marker/actionability logic, manifest schema, accepted structural counts and cache recovery remain unchanged.
- No additional save/cache writes, synthetic state, background worker, recurring scan, input shortcut or game control is introduced.
- Successful steady-gameplay checks do not log. Snapshots occur only at existing load/recovery boundaries; rejections identify their actual condition.
- `ref` is a managed identity hash used only to correlate object references within this process; `alive` uses the original Unity-liveness test. Hash equality alone is not proof of native identity.
- Diagnostic failures are caught so observation does not change bind results.

## Minimal installed test

1. Start the game and load the preserved save; wait for the HUD.
2. Return to the main menu and load the same save again; wait for the HUD.
3. Report that the reload is complete. The local log is collected by the agent; no upload or command is needed.

No conversation, loan acceptance, inventory change or new game is needed. Ordinary native saving behavior is unchanged. After collecting the log, close the game so the agent can restore the exact stable 1.1.15 DLL. Do not terminate the game automatically.

The decisive evidence is a `DWQM_BIND_DIAG event=reject` record plus surrounding snapshots/recovery. Interpret the observed condition before proposing a fix. Preserve verified findings in `docs/VERIFIED_RUNTIME_DATA.md`; keep raw logs private.
