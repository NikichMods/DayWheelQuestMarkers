// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using System.Reflection;

internal static class SceneReadinessTests
{
    private sealed class Save
    {
        private readonly object _inventory;
        internal Save(object inventory) { _inventory = inventory; }
        internal object Inventory { get { return _inventory; } }
    }

    private sealed class Player
    {
        private object _data;
        internal Player(object data) { _data = data; }
        internal void Install(object data) { _data = data; }
        internal object Data { get { return _data; } }
    }

    private static int Main(string[] args)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(args[0]));
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                var name = new AssemblyName(e.Name).Name + ".dll";
                var path = Path.Combine(directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
                for (var i = 2; i < args.Length; i++)
                {
                    path = Path.Combine(args[i], name);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            var type = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("CalendarQuestsPins.LoadingCachePrewarmGate", true);
            var method = type.GetMethod("HasPlayerInventoryForSave", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new Exception("Compiled readiness predicate is missing");
            Func<object, object, bool> ready = (inputSave, inputPlayer) => (bool)method.Invoke(null, new[] { inputSave, inputPlayer });
            var oldInventory = new object();
            var currentInventory = new object();
            var saveA = new Save(currentInventory);
            var player = new Player(oldInventory);
            Check(!ready(saveA, player), "Old scene player was admitted for a newly deserialized save");
            Check(!ready(new Save(null), new Player(null)), "Two null inventories were treated as ownership");
            Check(!ready(null, player), "Null save was admitted");
            Check(!ready(saveA, null), "Null player was admitted");
            Check(!ready(new object(), player), "Missing save inventory field was admitted");
            Check(!ready(saveA, new object()), "Missing player data field was admitted");
            player.Install(currentInventory); // The verified native RestoreSavedInventory assignment.
            Check(ready(saveA, player), "Current save's native inventory binding was rejected");
            var reloadA = new Save(new object());
            Check(!ready(reloadA, player), "Same-save deserialization reused the previous player's binding");
            var saveB = new Save(new object());
            Check(!ready(saveB, player), "Cross-save transition reused the previous player's binding");
            player.Install(saveB.Inventory);
            Check(ready(saveB, player), "New scene player for B was rejected");
            Check(ReferenceEquals(saveA.Inventory, currentInventory) && ReferenceEquals(player.Data, saveB.Inventory),
                "Readiness observation mutated the input state");
            Console.WriteLine("Scene readiness: PASS; 11 ownership/transition/read-only checks. Native scene timing still requires installed A -> A -> B -> A.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Check(bool value, string failure)
    {
        if (!value) throw new Exception(failure);
    }
}
