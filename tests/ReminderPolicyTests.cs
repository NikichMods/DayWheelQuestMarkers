// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

internal static class ReminderPolicyTests
{
    private static int Main(string[] args)
    {
        try
        {
            var assemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(args[0]));
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                var name = new AssemblyName(e.Name).Name + ".dll";
                var directories = new List<string> { assemblyDirectory };
                for (var i = 2; i < args.Length; i++) directories.Add(args[i]);
                foreach (var directory in directories)
                {
                    var path = Path.Combine(directory, name);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var type = assembly.GetType("CalendarQuestsPins.CalendarQuestsPinsPlugin", true);
            var policy = type.GetMethod("ShouldRemindDialogueTopic", BindingFlags.NonPublic | BindingFlags.Static);
            if (policy == null) throw new Exception("Compiled reminder policy is missing");

            // Independent accepted lifecycle census: retain every owner except the agreed loan offer.
            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(args[1]))
            {
                if (line.Length == 0 || line[0] == '#' || line.StartsWith("npc\t", StringComparison.Ordinal)) continue;
                var row = line.Split('\t');
                if (row[12] == "ADMIT") owners.Add(row[0] + "\t" + row[10]);
            }
            if (owners.Count != 60) throw new Exception("Unexpected accepted lifecycle owner universe: " + owners.Count);
            var removed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var owner in owners)
            {
                var pair = owner.Split('\t');
                if (!Allows(policy, pair[0], pair[1])) removed.Add(owner);
            }
            if (!removed.SetEquals(new[] { "npc_merchant\t@merchant_2e_1e" }))
                throw new Exception("Policy changed interactions outside the loan offer: " + string.Join(",", removed));

            // Repayment and unrelated services/conversations must not inherit the exclusion.
            foreach (var answer in new[] { "@merchant_2e_1f", "@merchant_favore", "@merchant_favore_done", "@merchant_2e", "@merchant_2e_1e_extra" })
                if (!Allows(policy, "npc_merchant", answer)) throw new Exception("Unexpected suppression: " + answer);
            if (!Allows(policy, "npc_bishop", "@merchant_2e_1e")) throw new Exception("Exclusion leaked across NPCs");
            Console.WriteLine("Reminder policy: PASS; 60 accepted dialogue owners -> 59 reminders; only Merchant loan offer excluded; repayment preserved.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static bool Allows(MethodInfo policy, string npc, string answer)
    {
        return (bool)policy.Invoke(null, new object[] { npc, answer });
    }
}
