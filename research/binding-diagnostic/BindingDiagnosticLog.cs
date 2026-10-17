// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Logging;

namespace CalendarQuestsPins
{
    // Research only. Every rejection is emitted at the actual original false-return boundary.
    // Observations never select a return value, call a world-object factory, or alter host state.
    internal static class BindingDiagnosticLog
    {
        private static ManualLogSource _log;

        internal static bool Fail(string method, string check, string detail = "")
        {
            Write("event=reject method=" + method + " check=" + check + " " + detail);
            return false;
        }

        internal static string Describe(object value)
        {
            try
            {
                return ReferenceEquals(value, null) ? "ref=null alive=False" :
                    "ref=" + RuntimeHelpers.GetHashCode(value) + " alive=" + ReflectionUtil.IsUnityAlive(value);
            }
            catch { return "observation_failed=True"; }
        }

        internal static void Snapshot(string stage, object save, object mainGame,
            IEnumerable<WeekdayInteractionRuleCache.TargetRules> targets)
        {
            try
            {
                object player;
                ReflectionUtil.TryRead(mainGame, "player", out player);
                Write("event=snapshot stage=" + stage + " save_ref=" +
                    (ReferenceEquals(save, null) ? "null" : RuntimeHelpers.GetHashCode(save).ToString()) +
                    " started=" + Flag(mainGame, "game_started") +
                    " starting=" + Flag(mainGame, "game_starting") + " player=" + Describe(player));
                var text = new StringBuilder();
                if (targets != null)
                    foreach (var target in targets)
                    {
                        if (text.Length > 0) text.Append(" | ");
                        if (target == null) text.Append("target=null");
                        else text.Append(target.NpcId).Append(':').Append(Describe(target.WorldObject));
                    }
                Write("event=targets stage=" + stage + " objects=" + text);
            }
            catch (Exception ex) { Write("event=observation_failed stage=" + stage + " type=" + ex.GetType().Name); }
        }

        private static string Flag(object mainGame, string name)
        {
            if (ReferenceEquals(mainGame, null)) return "unknown";
            var field = mainGame.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return field == null ? "unknown" : Convert.ToString(field.GetValue(null));
        }

        private static void Write(string fields)
        {
            try
            {
                if (_log == null) _log = Logger.CreateLogSource("DWQM Binding Diagnostic");
                _log.LogInfo("DWQM_BIND_DIAG " + fields);
            }
            catch { /* A diagnostic failure must not change the original recovery behavior. */ }
        }
    }
}
