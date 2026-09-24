using System;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Exact GK 1.407 fallback for the two verified task stages whose required NPC visit
    /// has no selectable answer representing the completion interaction.
    ///
    /// All answer-backed task completions are derived by WeekdayInteractionRuleCache.
    /// </summary>
    internal sealed class VerifiedCompletionReminderRules
    {
        internal bool IsOwnerTaskActionable(WeekdayInteractionRuleCache.TargetRules target, string taskId)
        {
            if (target == null || string.IsNullOrEmpty(target.NpcId) || string.IsNullOrEmpty(taskId))
                return false;

            if (string.Equals(target.NpcId, "npc_inquisitor", StringComparison.Ordinal) &&
                string.Equals(taskId, "inquisitor_talk", StringComparison.Ordinal)) return true;

            if (string.Equals(target.NpcId, "npc_cultist", StringComparison.Ordinal) &&
                string.Equals(taskId, "snake_back", StringComparison.Ordinal)) return true;

            return false;
        }

        internal void Clear()
        {
        }
    }
}
