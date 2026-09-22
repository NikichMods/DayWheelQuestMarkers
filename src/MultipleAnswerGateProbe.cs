using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;
using CalendarQuestsPins;

namespace CalendarQuestsPinsResearch
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MultipleAnswerGateProbe : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.daywheel.multiple-answer-gate-probe";
        public const string PluginName = "Day Wheel Quest Markers - MultipleAnswer gate probe";
        public const string PluginVersion = "0.1.0";

        private const string NpcId = "npc_cultist";
        private const int MultiId = 106;
        private const int AnswerIndex = 29;
        private const string AnswerId = "@souls_s_s33_ask";
        private const int ExpectedSourceId = 2644;
        private const string TaskId = "dlc_souls_s29_3";

        private Type _mainGameType;
        private Type _worldMapType;
        private Type _controllerType;
        private MethodInfo _worldObjectGetter;
        private object _mainGame;
        private bool _finished;
        private float _nextAttempt;

        private sealed class Connection
        {
            internal string SourcePort;
            internal string TargetPort;
            internal string SourceNode;
            internal string TargetNode;
        }

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _worldMapType = ReflectionUtil.FindType("WorldMap");
            _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
            _worldObjectGetter = FindWorldObjectGetter();
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            Logger.LogInfo(PluginName + " " + PluginVersion +
                           " loaded. Read-only, one-shot, exact target=" + NpcId + "/" + MultiId +
                           "/" + AnswerIndex + "/" + AnswerId + ".");
        }

        private void Update()
        {
            if (_finished || Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            if (!RuntimeReady()) return;

            try { RunProbe(); }
            catch (Exception ex) { Logger.LogError("MULTI_GATE_FATAL " + ex); }
            finally
            {
                _finished = true;
                enabled = false;
                Logger.LogInfo("MULTI_GATE_END probe disabled.");
            }
        }

        private bool RuntimeReady()
        {
            if (_mainGameType == null || _worldMapType == null || _controllerType == null || _worldObjectGetter == null)
                return false;

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) ||
                !(started is bool) || !(bool)started) return false;

            if (_mainGame == null || !ReflectionUtil.IsUnityAlive(_mainGame))
                _mainGame = ReflectionUtil.FindActiveUnityInstance(_mainGameType);
            if (_mainGame == null) return false;

            object save;
            if (!ReflectionUtil.TryRead(_mainGame, "save", out save) || save == null) return false;
            return GetNpcWorldObject() != null;
        }

        private void RunProbe()
        {
            Logger.LogInfo("MULTI_GATE_BEGIN game=1.407 readOnly=True targetNpc=" + NpcId +
                           " multi=" + MultiId + " index=" + AnswerIndex + " answer=" + AnswerId);

            var wgo = GetNpcWorldObject();
            var component = wgo as Component;
            if (component == null)
            {
                Logger.LogError("MULTI_GATE_ABORT npc WGO is not a Component.");
                return;
            }

            var controller = component.GetComponent(_controllerType);
            object graph;
            object serializedObj;
            if (controller == null ||
                !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null ||
                !ReflectionUtil.TryRead(graph, "_serializedGraph", out serializedObj) ||
                !(serializedObj is string))
            {
                Logger.LogError("MULTI_GATE_ABORT graph/serialized graph unavailable.");
                return;
            }

            var serialized = (string)serializedObj;
            LogTaskState();
            LogSerializedProvenance(serialized);

            object allNodesObj;
            if (!ReflectionUtil.TryRead(graph, "allNodes", out allNodesObj) || !(allNodesObj is IEnumerable))
            {
                Logger.LogError("MULTI_GATE_ABORT graph.allNodes unavailable.");
                return;
            }

            object multi = null;
            object expectedSource = null;
            var nodeCount = 0;
            foreach (var node in (IEnumerable)allNodesObj)
            {
                if (node == null) continue;
                nodeCount++;
                var id = ReadInt(node, "_ID");
                if (id == MultiId) multi = node;
                if (id == ExpectedSourceId) expectedSource = node;
            }

            Logger.LogInfo("MULTI_GATE_RUNTIME_GRAPH nodes=" + nodeCount +
                           " multiFound=" + (multi != null) +
                           " source2644Found=" + (expectedSource != null));

            if (multi == null)
            {
                Logger.LogError("MULTI_GATE_ABORT runtime multi node 106 unavailable.");
                return;
            }

            Logger.LogInfo("MULTI_GATE_MULTI runtimeType=" + multi.GetType().FullName +
                           " runtimeId=" + ReadInt(multi, "_ID") +
                           " wgo=" + DescribeObject(ReadMember(multi, "wgo")));

            var answers = ReadMember(multi, "answers") as IList;
            if (answers == null)
            {
                Logger.LogError("MULTI_GATE_ABORT runtime answers list unavailable.");
                return;
            }

            var observedAnswer = AnswerIndex < answers.Count && answers[AnswerIndex] != null
                ? answers[AnswerIndex].ToString()
                : null;
            Logger.LogInfo("MULTI_GATE_ANSWER_LIST count=" + answers.Count +
                           " index29=" + Safe(observedAnswer) +
                           " exact=" + string.Equals(observedAnswer, AnswerId, StringComparison.Ordinal));
            if (!string.Equals(observedAnswer, AnswerId, StringComparison.Ordinal))
            {
                Logger.LogError("MULTI_GATE_ABORT exact answer identity mismatch.");
                return;
            }

            if (expectedSource != null)
            {
                Logger.LogInfo("MULTI_GATE_SOURCE_NODE id=" + ExpectedSourceId +
                               " runtimeType=" + expectedSource.GetType().FullName);
                DumpFields("SOURCE", expectedSource, 0, 2, new HashSet<object>(ReferenceEqualityComparer.Instance));
            }

            object inputPortsObj;
            if (!ReflectionUtil.TryRead(multi, "inputPorts", out inputPortsObj) || !(inputPortsObj is IDictionary))
            {
                Logger.LogError("MULTI_GATE_ABORT runtime inputPorts unavailable.");
                return;
            }

            var inputPorts = (IDictionary)inputPortsObj;
            Logger.LogInfo("MULTI_GATE_PORTS count=" + inputPorts.Count);
            object targetPort = null;
            string targetKey = null;
            foreach (DictionaryEntry entry in inputPorts)
            {
                var key = entry.Key == null ? null : entry.Key.ToString();
                if (key != null && key.IndexOf("#" + AnswerIndex, StringComparison.Ordinal) >= 0)
                {
                    targetPort = entry.Value;
                    targetKey = key;
                }
            }

            Logger.LogInfo("MULTI_GATE_TARGET_PORT key=" + Safe(targetKey) +
                           " portType=" + (targetPort == null ? "<null>" : targetPort.GetType().FullName));
            if (targetPort == null)
            {
                Logger.LogError("MULTI_GATE_ABORT exact input port not found.");
                return;
            }

            var connectedPort = ReadMember(targetPort, "connectedPort");
            var connectedNode = connectedPort == null ? null : ReadMember(connectedPort, "parent");
            if (connectedNode == null && connectedPort != null) connectedNode = ReadMember(connectedPort, "parentNode");
            Logger.LogInfo("MULTI_GATE_RUNTIME_CONNECTION connectedPort=" + DescribeObject(connectedPort) +
                           " connectedNode=" + DescribeNode(connectedNode));

            object value;
            if (!TryReadMember(targetPort, "value", out value))
            {
                Logger.LogError("MULTI_GATE_ABORT ValueInput.value getter unavailable or threw.");
                return;
            }

            Logger.LogInfo("MULTI_GATE_VALUE runtimeType=" +
                           (value == null ? "<null>" : value.GetType().FullName));
            if (value == null)
            {
                Logger.LogError("MULTI_GATE_ABORT resolved AnswerData value is null.");
                return;
            }

            DumpFields("VALUE", value, 0, 4, new HashSet<object>(ReferenceEqualityComparer.Instance));

            var multiAnswerDataType = ReflectionUtil.FindType("MultipleAnswerData");
            if (multiAnswerDataType == null || !multiAnswerDataType.IsInstanceOfType(value))
            {
                Logger.LogError("MULTI_GATE_RESULT isMultipleAnswerData=False");
                return;
            }

            Logger.LogInfo("MULTI_GATE_RESULT isMultipleAnswerData=True");
            EvaluateNative(value, wgo);
        }

        private void EvaluateNative(object value, object wgo)
        {
            var type = value.GetType();
            MethodInfo fill = null;
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(method.Name, "FillVisualData", StringComparison.Ordinal)) continue;
                var p = method.GetParameters();
                if (p.Length != 2 || !p[0].ParameterType.IsByRef) continue;
                if (p[0].ParameterType.GetElementType() == null ||
                    !string.Equals(p[0].ParameterType.GetElementType().Name, "MultipleAnswerVisualData", StringComparison.Ordinal))
                    continue;
                fill = method;
                break;
            }

            if (fill == null)
            {
                Logger.LogError("MULTI_GATE_NATIVE FillVisualData(ref MultipleAnswerVisualData, WGO) not found.");
                return;
            }

            var visualType = fill.GetParameters()[0].ParameterType.GetElementType();
            var visual = Activator.CreateInstance(visualType);
            WriteMember(visual, "id", AnswerId);

            var listField = FindField(visualType, "answer_visual_datas");
            if (listField != null && listField.GetValue(visual) == null)
                listField.SetValue(visual, Activator.CreateInstance(listField.FieldType));

            var before = FingerprintFields(value, 3);
            var args = new object[] { visual, wgo };
            try
            {
                fill.Invoke(value, args);
                visual = args[0];
            }
            catch (TargetInvocationException ex)
            {
                Logger.LogError("MULTI_GATE_NATIVE invocation threw " +
                                (ex.InnerException == null ? ex.GetType().Name : ex.InnerException.GetType().Name) +
                                ": " + (ex.InnerException == null ? ex.Message : ex.InnerException.Message));
                return;
            }

            var after = FingerprintFields(value, 3);
            Logger.LogInfo("MULTI_GATE_NATIVE evaluated=True authoredObjectFingerprintStable=" +
                           string.Equals(before, after, StringComparison.Ordinal));
            DumpFields("VISUAL", visual, 0, 5, new HashSet<object>(ReferenceEqualityComparer.Instance));

            object pickable;
            if (TryReadMember(visual, "can_be_picked", out pickable))
                Logger.LogInfo("MULTI_GATE_NATIVE_SUMMARY can_be_picked=" + SafeObject(pickable));
            else
                Logger.LogInfo("MULTI_GATE_NATIVE_SUMMARY can_be_picked=<unavailable>");
        }

        private void LogTaskState()
        {
            object save;
            object known;
            if (!ReflectionUtil.TryRead(_mainGame, "save", out save) || save == null ||
                !ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null)
            {
                Logger.LogInfo("MULTI_GATE_TASK task=" + TaskId + " state=<known_npcs unavailable>");
                return;
            }

            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null)
            {
                Logger.LogInfo("MULTI_GATE_TASK task=" + TaskId + " state=<npcs unavailable>");
                return;
            }

            foreach (var npc in npcs)
            {
                if (!string.Equals(ReflectionUtil.ReadString(npc, "npc_id"), NpcId, StringComparison.Ordinal)) continue;
                var tasks = ReflectionUtil.EnumerateMember(npc, "tasks");
                if (tasks == null) break;
                foreach (var task in tasks)
                {
                    if (!string.Equals(ReflectionUtil.ReadString(task, "id"), TaskId, StringComparison.Ordinal)) continue;
                    object state;
                    ReflectionUtil.TryRead(task, "state", out state);
                    Logger.LogInfo("MULTI_GATE_TASK task=" + TaskId + " state=" + SafeObject(state));
                    return;
                }
            }

            Logger.LogInfo("MULTI_GATE_TASK task=" + TaskId + " state=<not found>");
        }

        private void LogSerializedProvenance(string serialized)
        {
            var connections = ParseConnections(serialized);
            var incoming = new List<Connection>();
            for (var i = 0; i < connections.Count; i++)
            {
                if (!string.Equals(connections[i].TargetNode, MultiId.ToString(), StringComparison.Ordinal)) continue;
                if (connections[i].TargetPort == null ||
                    connections[i].TargetPort.IndexOf("#" + AnswerIndex, StringComparison.Ordinal) < 0) continue;
                incoming.Add(connections[i]);
            }

            Logger.LogInfo("MULTI_GATE_SERIALIZED incomingExact=" + incoming.Count);
            for (var i = 0; i < incoming.Count; i++)
            {
                Logger.LogInfo("MULTI_GATE_SERIALIZED_CONNECTION source=" + Safe(incoming[i].SourceNode) +
                               " sourcePort=" + Safe(incoming[i].SourcePort) +
                               " target=" + Safe(incoming[i].TargetNode) +
                               " targetPort=" + Safe(incoming[i].TargetPort));
            }

            var context = ExtractNodeContext(serialized, ExpectedSourceId.ToString(), 1200);
            Logger.LogInfo("MULTI_GATE_SOURCE_CONTEXT " + Safe(context));
        }

        private static string ExtractNodeContext(string serialized, string nodeId, int radius)
        {
            var marker = "\"$id\":\"" + nodeId + "\"";
            var p = serialized.IndexOf(marker, StringComparison.Ordinal);
            if (p < 0) return "<node not found>";
            var start = Math.Max(0, p - radius);
            var end = Math.Min(serialized.Length, p + marker.Length + radius);
            return serialized.Substring(start, end - start)
                .Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }

        private static List<Connection> ParseConnections(string serialized)
        {
            var result = new List<Connection>();
            const string spMarker = "\"_sourcePortName\":\"";
            const string tpMarker = "\"_targetPortName\":\"";
            const string srcMarker = "\"_sourceNode\":{\"$ref\":\"";
            const string dstMarker = "\"_targetNode\":{\"$ref\":\"";
            var start = 0;
            while (start < serialized.Length)
            {
                var spPos = serialized.IndexOf(spMarker, start, StringComparison.Ordinal);
                if (spPos < 0) break;
                int spEnd;
                var sp = ReadJsonString(serialized, spPos + spMarker.Length, out spEnd);
                var tpPos = serialized.IndexOf(tpMarker, spEnd, StringComparison.Ordinal);
                if (tpPos < 0 || tpPos - spEnd > 300) { start = spEnd + 1; continue; }
                int tpEnd;
                var tp = ReadJsonString(serialized, tpPos + tpMarker.Length, out tpEnd);
                var srcPos = serialized.IndexOf(srcMarker, tpEnd, StringComparison.Ordinal);
                if (srcPos < 0 || srcPos - tpEnd > 300) { start = tpEnd + 1; continue; }
                int srcEnd;
                var src = ReadJsonString(serialized, srcPos + srcMarker.Length, out srcEnd);
                var dstPos = serialized.IndexOf(dstMarker, srcEnd, StringComparison.Ordinal);
                if (dstPos < 0 || dstPos - srcEnd > 300) { start = srcEnd + 1; continue; }
                int dstEnd;
                var dst = ReadJsonString(serialized, dstPos + dstMarker.Length, out dstEnd);
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst))
                    result.Add(new Connection { SourcePort = sp, TargetPort = tp, SourceNode = src, TargetNode = dst });
                start = dstEnd + 1;
            }
            return result;
        }

        private static string ReadJsonString(string text, int start, out int end)
        {
            end = start;
            var escaped = false;
            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (escaped) { escaped = false; continue; }
                if (ch == '\\') { escaped = true; continue; }
                if (ch != '\"') continue;
                end = i;
                return text.Substring(start, i - start)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\/", "/");
            }
            return null;
        }

        private void DumpFields(string label, object value, int depth, int maxDepth, HashSet<object> seen)
        {
            if (value == null)
            {
                Logger.LogInfo("MULTI_GATE_" + label + " depth=" + depth + " <null>");
                return;
            }

            var type = value.GetType();
            Logger.LogInfo("MULTI_GATE_" + label + " depth=" + depth +
                           " type=" + type.FullName + " value=" + SafeObject(value));
            if (depth >= maxDepth || IsLeaf(type)) return;
            if (!type.IsValueType && !seen.Add(value)) return;

            if (value is IEnumerable && !(value is string))
            {
                var index = 0;
                foreach (var item in (IEnumerable)value)
                {
                    if (index >= 12)
                    {
                        Logger.LogInfo("MULTI_GATE_" + label + " depth=" + depth + " listTruncated=True");
                        break;
                    }
                    DumpFields(label + "[" + index + "]", item, depth + 1, maxDepth, seen);
                    index++;
                }
                return;
            }

            foreach (var field in EnumerateFields(type))
            {
                if (field.IsStatic) continue;
                if (ShouldSkipField(field.Name, field.FieldType)) continue;
                object child;
                try { child = field.GetValue(value); }
                catch { continue; }
                Logger.LogInfo("MULTI_GATE_" + label + "_FIELD depth=" + depth +
                               " " + field.DeclaringType.Name + "." + field.Name + "=" + SafeObject(child));
                if (child != null && ShouldRecurse(child.GetType()))
                    DumpFields(label + "." + field.Name, child, depth + 1, maxDepth, seen);
            }
        }

        private static string FingerprintFields(object value, int maxDepth)
        {
            var sb = new StringBuilder();
            BuildFingerprint(sb, value, 0, maxDepth, new HashSet<object>(ReferenceEqualityComparer.Instance));
            return sb.ToString();
        }

        private static void BuildFingerprint(StringBuilder sb, object value, int depth, int maxDepth, HashSet<object> seen)
        {
            if (value == null) { sb.Append("<null>"); return; }
            var type = value.GetType();
            sb.Append(type.FullName).Append('{');
            if (depth >= maxDepth || IsLeaf(type)) { sb.Append(SafeObject(value)).Append('}'); return; }
            if (!type.IsValueType && !seen.Add(value)) { sb.Append("<seen>}"); return; }

            if (value is IEnumerable && !(value is string))
            {
                var i = 0;
                foreach (var item in (IEnumerable)value)
                {
                    if (i++ >= 12) break;
                    BuildFingerprint(sb, item, depth + 1, maxDepth, seen);
                }
                sb.Append('}');
                return;
            }

            foreach (var field in EnumerateFields(type))
            {
                if (field.IsStatic || ShouldSkipField(field.Name, field.FieldType)) continue;
                object child;
                try { child = field.GetValue(value); }
                catch { continue; }
                sb.Append(field.Name).Append('=');
                if (child == null || IsLeaf(child.GetType())) sb.Append(SafeObject(child));
                else if (ShouldRecurse(child.GetType())) BuildFingerprint(sb, child, depth + 1, maxDepth, seen);
                else sb.Append(child.GetType().FullName);
                sb.Append(';');
            }
            sb.Append('}');
        }

        private static IEnumerable<FieldInfo> EnumerateFields(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.DeclaredOnly);
                for (var i = 0; i < fields.Length; i++) yield return fields[i];
            }
        }

        private static bool ShouldSkipField(string name, Type type)
        {
            if (name == "_graph" || name == "_inConnections" || name == "_outConnections" ||
                name == "inputPorts" || name == "outputPorts") return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            return false;
        }

        private static bool ShouldRecurse(Type type)
        {
            if (type == null || IsLeaf(type)) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            var name = type.FullName ?? type.Name;
            return name.IndexOf("Answer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("SmartRes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("GameRes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static bool IsLeaf(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                   type == typeof(decimal) || type == typeof(DateTime);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                                            BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        private static bool WriteMember(object target, string name, object value)
        {
            if (target == null) return false;
            var field = FindField(target.GetType(), name);
            if (field != null)
            {
                try { field.SetValue(target, value); return true; } catch { return false; }
            }
            return false;
        }

        private static object ReadMember(object target, string name)
        {
            object value;
            return TryReadMember(target, name, out value) ? value : null;
        }

        private static bool TryReadMember(object target, string name, out object value)
        {
            value = null;
            if (target == null) return false;
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                                            BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    try { value = field.GetValue(target); return true; } catch { return false; }
                }

                var property = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public |
                                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    try { value = property.GetValue(target, null); return true; } catch { return false; }
                }
            }
            return false;
        }

        private static int ReadInt(object target, string name)
        {
            var value = ReadMember(target, name);
            if (value == null) return int.MinValue;
            try { return Convert.ToInt32(value); } catch { return int.MinValue; }
        }

        private static string DescribeNode(object node)
        {
            if (node == null) return "<null>";
            return node.GetType().FullName + "#" + ReadInt(node, "_ID");
        }

        private static string DescribeObject(object value)
        {
            if (value == null) return "<null>";
            var unity = value as UnityEngine.Object;
            if (unity != null) return value.GetType().FullName + "#" + unity.GetInstanceID() + " name=" + Safe(unity.name);
            return value.GetType().FullName;
        }

        private static string SafeObject(object value)
        {
            if (value == null) return "<null>";
            if (value is string) return Safe((string)value);
            var unity = value as UnityEngine.Object;
            if (unity != null) return DescribeObject(unity);
            if (value is IEnumerable && !(value is string)) return value.GetType().FullName;
            try
            {
                var text = value.ToString();
                return Safe(text != null && text.Length > 240 ? text.Substring(0, 240) + "...<truncated>" : text);
            }
            catch { return "<ToString failed>"; }
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "<none>";
            return value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Replace("|", "/");
        }

        private object GetNpcWorldObject()
        {
            try { return _worldObjectGetter.Invoke(null, new object[] { NpcId, true }); }
            catch { return null; }
        }

        private MethodInfo FindWorldObjectGetter()
        {
            if (_worldMapType == null) return null;
            foreach (var method in _worldMapType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(method.Name, "GetWorldGameObjectByObjId", StringComparison.Ordinal)) continue;
                var p = method.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(bool))
                    return method;
            }
            return null;
        }

        private static bool TryReadStatic(Type type, string name, out object value)
        {
            value = null;
            if (type == null) return false;
            try
            {
                var field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) { value = field.GetValue(null); return true; }
                var property = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    value = property.GetValue(null, null);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object obj) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
        }
    }
}
