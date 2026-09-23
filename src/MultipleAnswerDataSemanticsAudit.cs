using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MultipleAnswerDataSemanticsAudit : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.daywheel.multiple-answerdata-semantics-audit";
        public const string PluginName = "Day Wheel Quest Markers - MultipleAnswerData semantics audit";
        public const string PluginVersion = "0.1.0";

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        private static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpcodeMap();

        private Type _mainGameType;
        private Type _worldMapType;
        private Type _controllerType;
        private MethodInfo _worldObjectGetter;
        private object _mainGame;
        private bool _done;
        private float _nextAttempt;

        private sealed class Node
        {
            internal string Id;
            internal string Type;
            internal int TypePosition;
        }

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
                           " loaded. Read-only one-shot IL + six-NPC MultipleAnswerData usage audit.");
        }

        private void Update()
        {
            if (_done || Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            if (!RuntimeReady()) return;

            try
            {
                DumpTypeSemantics("MultipleAnswerData");
                DumpTypeSemantics("AnswerData");
                DumpUsageUniverse();
            }
            catch (Exception ex)
            {
                Logger.LogError("MAD_FATAL " + ex);
            }
            finally
            {
                _done = true;
                enabled = false;
                Logger.LogInfo("MAD_DONE");
            }
        }

        private bool RuntimeReady()
        {
            if (_mainGameType == null || _worldMapType == null || _controllerType == null || _worldObjectGetter == null)
                return false;

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started)
                return false;

            if (_mainGame == null || !ReflectionUtil.IsUnityAlive(_mainGame))
                _mainGame = ReflectionUtil.FindActiveUnityInstance(_mainGameType);
            return _mainGame != null;
        }

        private void DumpTypeSemantics(string typeName)
        {
            var type = ReflectionUtil.FindType(typeName);
            if (type == null)
            {
                Logger.LogError("MAD_TYPE missing=" + typeName);
                return;
            }

            Logger.LogInfo("MAD_TYPE name=" + type.FullName + " base=" + (type.BaseType == null ? "<null>" : type.BaseType.FullName));
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                 BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Logger.LogInfo("MAD_FIELD type=" + type.Name +
                               " name=" + field.Name +
                               " fieldType=" + field.FieldType.FullName +
                               " static=" + field.IsStatic);
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!string.Equals(method.Name, "FillVisualData", StringComparison.Ordinal) &&
                    !string.Equals(method.Name, "get_can_be_picked", StringComparison.Ordinal) &&
                    !string.Equals(method.Name, "CanBePicked", StringComparison.Ordinal))
                    continue;
                DumpMethod(method);
            }
        }

        private void DumpMethod(MethodInfo method)
        {
            Logger.LogInfo("MAD_METHOD begin=" + MethodLabel(method));
            MethodBody body;
            try { body = method.GetMethodBody(); }
            catch (Exception ex)
            {
                Logger.LogError("MAD_METHOD_BODY_FAIL method=" + MethodLabel(method) + " error=" + ex.GetType().Name + ":" + ex.Message);
                return;
            }
            if (body == null)
            {
                Logger.LogInfo("MAD_METHOD noBody=" + MethodLabel(method));
                return;
            }

            var il = body.GetILAsByteArray();
            if (il == null)
            {
                Logger.LogInfo("MAD_METHOD noIL=" + MethodLabel(method));
                return;
            }

            var module = method.Module;
            var typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType
                ? method.DeclaringType.GetGenericArguments() : Type.EmptyTypes;
            var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;

            var p = 0;
            while (p < il.Length)
            {
                var offset = p;
                OpCode op;
                var first = il[p++];
                short value;
                if (first == 0xFE)
                {
                    if (p >= il.Length) break;
                    value = (short)(0xFE00 | il[p++]);
                }
                else value = first;

                if (!OpCodesByValue.TryGetValue(value, out op))
                {
                    Logger.LogWarning("MAD_IL method=" + method.DeclaringType.Name + "." + method.Name +
                                      " offset=" + offset.ToString("X4") + " opcode=<unknown:" + value.ToString("X4") + ">");
                    break;
                }

                string operand;
                try { operand = ReadOperand(il, ref p, op, module, typeArgs, methodArgs); }
                catch (Exception ex)
                {
                    operand = "<operand-error:" + ex.GetType().Name + ">";
                    p = Math.Min(il.Length, p);
                }

                Logger.LogInfo("MAD_IL method=" + method.DeclaringType.Name + "." + method.Name +
                               " offset=" + offset.ToString("X4") +
                               " op=" + op.Name +
                               (string.IsNullOrEmpty(operand) ? "" : " operand=" + Safe(operand)));
            }
            Logger.LogInfo("MAD_METHOD end=" + MethodLabel(method));
        }

        private static string ReadOperand(byte[] il, ref int p, OpCode op, Module module, Type[] typeArgs, Type[] methodArgs)
        {
            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    return null;
                case OperandType.ShortInlineI:
                    return ((sbyte)il[p++]).ToString(CultureInfo.InvariantCulture);
                case OperandType.InlineI:
                    return ReadInt32(il, ref p).ToString(CultureInfo.InvariantCulture);
                case OperandType.InlineI8:
                    return ReadInt64(il, ref p).ToString(CultureInfo.InvariantCulture);
                case OperandType.ShortInlineR:
                    return ReadSingle(il, ref p).ToString("R", CultureInfo.InvariantCulture);
                case OperandType.InlineR:
                    return ReadDouble(il, ref p).ToString("R", CultureInfo.InvariantCulture);
                case OperandType.ShortInlineVar:
                    return il[p++].ToString(CultureInfo.InvariantCulture);
                case OperandType.InlineVar:
                    return ReadUInt16(il, ref p).ToString(CultureInfo.InvariantCulture);
                case OperandType.ShortInlineBrTarget:
                {
                    var delta = (sbyte)il[p++];
                    return "IL_" + (p + delta).ToString("X4");
                }
                case OperandType.InlineBrTarget:
                {
                    var delta = ReadInt32(il, ref p);
                    return "IL_" + (p + delta).ToString("X4");
                }
                case OperandType.InlineSwitch:
                {
                    var count = ReadInt32(il, ref p);
                    var basePos = p + count * 4;
                    var sb = new StringBuilder();
                    for (var i = 0; i < count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        var delta = ReadInt32(il, ref p);
                        sb.Append("IL_").Append((basePos + delta).ToString("X4"));
                    }
                    return sb.ToString();
                }
                case OperandType.InlineString:
                {
                    var token = ReadInt32(il, ref p);
                    try { return "\"" + module.ResolveString(token) + "\""; }
                    catch { return "stringToken=0x" + token.ToString("X8"); }
                }
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                {
                    var token = ReadInt32(il, ref p);
                    try
                    {
                        var member = module.ResolveMember(token, typeArgs, methodArgs);
                        return MemberLabel(member);
                    }
                    catch { return "token=0x" + token.ToString("X8"); }
                }
                case OperandType.InlineSig:
                {
                    var token = ReadInt32(il, ref p);
                    return "sigToken=0x" + token.ToString("X8");
                }
                default:
                    return "<unsupported-operand:" + op.OperandType + ">";
            }
        }

        private void DumpUsageUniverse()
        {
            var totalRelayNodes = 0;
            var totalMenuUses = 0;
            for (var n = 0; n < NpcIds.Length; n++)
            {
                var npcId = NpcIds[n];
                var serialized = ReadSerializedGraph(npcId);
                if (string.IsNullOrEmpty(serialized))
                {
                    Logger.LogWarning("MAD_UNIVERSE npc=" + npcId + " graph=<missing>");
                    continue;
                }

                var nodes = BuildNodeIndex(serialized);
                var connections = ParseConnections(serialized);
                var npcRelayNodes = 0;
                var npcMenuUses = 0;

                foreach (var node in nodes.Values)
                {
                    if (node == null || string.IsNullOrEmpty(node.Type) ||
                        node.Type.IndexOf("RelayValueOutput", StringComparison.Ordinal) < 0 ||
                        node.Type.IndexOf("MultipleAnswerData", StringComparison.Ordinal) < 0)
                        continue;

                    totalRelayNodes++;
                    npcRelayNodes++;
                    var uid = ReadNodeDirectString(serialized, node, "_sourceInputUID", 1400);
                    var relayInput = FindRelayInput(serialized, nodes, uid);
                    var producer = relayInput == null ? null : FindSingleValueSource(nodes, connections, relayInput.Id);
                    Logger.LogInfo("MAD_RELAY npc=" + npcId +
                                   " output=" + node.Id +
                                   " uid=" + Safe(uid) +
                                   " input=" + (relayInput == null ? "<missing>" : relayInput.Id) +
                                   " identifier=" + (relayInput == null ? "<missing>" : Safe(ReadNodeDirectString(serialized, relayInput, "identifier", 1800))) +
                                   " producer=" + (producer == null ? "<missing-or-ambiguous>" : producer.Id) +
                                   " producerType=" + (producer == null ? "<missing-or-ambiguous>" : Safe(producer.Type)));

                    for (var c = 0; c < connections.Count; c++)
                    {
                        var connection = connections[c];
                        if (!string.Equals(connection.SourceNode, node.Id, StringComparison.Ordinal)) continue;
                        Node target;
                        if (!nodes.TryGetValue(connection.TargetNode, out target) || target == null ||
                            !target.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                        var index = ParseAnswerPortIndex(connection.TargetPort);
                        var answers = ReadMultiAnswers(serialized, target);
                        var answer = index >= 0 && index < answers.Count ? answers[index] : "<index-out-of-range>";
                        npcMenuUses++;
                        totalMenuUses++;
                        Logger.LogInfo("MAD_MENU_USE npc=" + npcId +
                                       " relay=" + node.Id +
                                       " multi=" + target.Id +
                                       " index=" + index +
                                       " answer=" + Safe(answer));
                    }
                }

                Logger.LogInfo("MAD_UNIVERSE npc=" + npcId +
                               " relayNodes=" + npcRelayNodes +
                               " menuUses=" + npcMenuUses);
            }
            Logger.LogInfo("MAD_UNIVERSE_TOTAL relayNodes=" + totalRelayNodes + " menuUses=" + totalMenuUses);
        }

        private Node FindRelayInput(string serialized, Dictionary<string, Node> nodes, string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            Node result = null;
            foreach (var pair in nodes)
            {
                var node = pair.Value;
                if (node == null || string.IsNullOrEmpty(node.Type) ||
                    node.Type.IndexOf("RelayValueInput", StringComparison.Ordinal) < 0 ||
                    node.Type.IndexOf("MultipleAnswerData", StringComparison.Ordinal) < 0)
                    continue;
                var candidate = ReadNodeDirectString(serialized, node, "_UID", 1400);
                if (!string.Equals(candidate, uid, StringComparison.Ordinal)) continue;
                if (result != null) return null;
                result = node;
            }
            return result;
        }

        private static Node FindSingleValueSource(Dictionary<string, Node> nodes, List<Connection> connections, string targetId)
        {
            Node result = null;
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, targetId, StringComparison.Ordinal)) continue;
                if (string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase)) continue;
                Node source;
                if (!nodes.TryGetValue(c.SourceNode, out source) || source == null) continue;
                if (result != null && !string.Equals(result.Id, source.Id, StringComparison.Ordinal)) return null;
                result = source;
            }
            return result;
        }

        private string ReadSerializedGraph(string npcId)
        {
            try
            {
                var wgo = _worldObjectGetter.Invoke(null, new object[] { npcId, true });
                var component = wgo as Component;
                if (component == null) return null;
                var controller = component.GetComponent(_controllerType);
                object graph;
                object serialized;
                if (controller == null || !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) return null;
                if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out serialized)) return null;
                return serialized as string;
            }
            catch { return null; }
        }

        private MethodInfo FindWorldObjectGetter()
        {
            if (_worldMapType == null) return null;
            foreach (var method in _worldMapType.GetMethods(ReflectionUtil.AnyStatic))
            {
                if (!string.Equals(method.Name, "GetWorldGameObjectByObjId", StringComparison.Ordinal)) continue;
                var p = method.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(bool))
                    return method;
            }
            return null;
        }

        private static Dictionary<string, Node> BuildNodeIndex(string serialized)
        {
            var result = new Dictionary<string, Node>(StringComparer.Ordinal);
            const string typeMarker = "\"$type\":\"";
            const string idMarker = "\"$id\":\"";
            var start = 0;
            while (start < serialized.Length)
            {
                var typePos = serialized.IndexOf(typeMarker, start, StringComparison.Ordinal);
                if (typePos < 0) break;
                int typeEnd;
                var type = ReadJsonString(serialized, typePos + typeMarker.Length, out typeEnd);
                if (type == null) break;
                var nextType = serialized.IndexOf(typeMarker, typeEnd, StringComparison.Ordinal);
                var idPos = serialized.IndexOf(idMarker, typeEnd, StringComparison.Ordinal);
                if (idPos >= 0 && (nextType < 0 || idPos < nextType) && idPos - typeEnd < 240)
                {
                    int idEnd;
                    var id = ReadJsonString(serialized, idPos + idMarker.Length, out idEnd);
                    if (!string.IsNullOrEmpty(id))
                        result[id] = new Node { Id = id, Type = type, TypePosition = typePos };
                }
                start = typeEnd + 1;
            }
            return result;
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

        private static List<string> ReadMultiAnswers(string serialized, Node node)
        {
            var result = new List<string>();
            var begin = Math.Max(0, node.TypePosition - 18000);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            const string marker = "\"answers\":[";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return result;
            var p = pos + marker.Length;
            while (p < window.Length)
            {
                while (p < window.Length && (char.IsWhiteSpace(window[p]) || window[p] == ',')) p++;
                if (p >= window.Length || window[p] == ']') break;
                if (window[p] != '\"') break;
                int end;
                var value = ReadJsonString(window, p + 1, out end);
                if (value == null) break;
                result.Add(value);
                p = end + 1;
            }
            return result;
        }

        private static int ParseAnswerPortIndex(string port)
        {
            if (string.IsNullOrEmpty(port)) return -1;
            var hash = port.LastIndexOf('#');
            if (hash < 0 || hash + 1 >= port.Length) return -1;
            var i = hash + 1;
            var value = 0;
            var digits = 0;
            while (i < port.Length && char.IsDigit(port[i]))
            {
                value = value * 10 + (port[i] - '0');
                digits++;
                i++;
            }
            return digits == 0 ? -1 : value;
        }

        private static string ReadNodeDirectString(string serialized, Node node, string key, int lookBehind)
        {
            if (serialized == null || node == null || string.IsNullOrEmpty(key)) return null;
            var begin = Math.Max(0, node.TypePosition - Math.Max(100, lookBehind));
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
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
                var raw = text.Substring(start, i - start);
                try { return Regex.Unescape(raw.Replace("\\/", "/")); }
                catch { return raw; }
            }
            return null;
        }

        private static Dictionary<short, OpCode> BuildOpcodeMap()
        {
            var map = new Dictionary<short, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(OpCode)) continue;
                var op = (OpCode)field.GetValue(null);
                map[op.Value] = op;
            }
            return map;
        }

        private static int ReadInt32(byte[] b, ref int p)
        {
            var v = BitConverter.ToInt32(b, p);
            p += 4;
            return v;
        }

        private static long ReadInt64(byte[] b, ref int p)
        {
            var v = BitConverter.ToInt64(b, p);
            p += 8;
            return v;
        }

        private static ushort ReadUInt16(byte[] b, ref int p)
        {
            var v = BitConverter.ToUInt16(b, p);
            p += 2;
            return v;
        }

        private static float ReadSingle(byte[] b, ref int p)
        {
            var v = BitConverter.ToSingle(b, p);
            p += 4;
            return v;
        }

        private static double ReadDouble(byte[] b, ref int p)
        {
            var v = BitConverter.ToDouble(b, p);
            p += 8;
            return v;
        }

        private static string MemberLabel(MemberInfo member)
        {
            if (member == null) return "<null-member>";
            var declaring = member.DeclaringType == null ? "<no-type>" : member.DeclaringType.FullName;
            return declaring + "." + member.Name;
        }

        private static string MethodLabel(MethodInfo method)
        {
            if (method == null) return "<null>";
            var sb = new StringBuilder();
            sb.Append(method.DeclaringType == null ? "<no-type>" : method.DeclaringType.FullName);
            sb.Append('.').Append(method.Name).Append('(');
            var p = method.GetParameters();
            for (var i = 0; i < p.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(p[i].ParameterType.FullName);
            }
            sb.Append(')');
            return sb.ToString();
        }

        private static bool TryReadStatic(Type type, string name, out object value)
        {
            value = null;
            if (type == null) return false;
            try
            {
                var field = type.GetField(name, ReflectionUtil.AnyStatic);
                if (field != null) { value = field.GetValue(null); return true; }
                var property = type.GetProperty(name, ReflectionUtil.AnyStatic);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    value = property.GetValue(null, null);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "<null>";
            return value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Replace("|", "/");
        }
    }
}
