using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SoulsWeekdayAnswerDataAudit : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.daywheel.souls-weekday-answerdata-audit";
        public const string PluginName = "Day Wheel Quest Markers - Souls weekday AnswerData audit";
        public const string PluginVersion = "0.1.0";

        private sealed class Target
        {
            internal string NpcId;
            internal string TaskId;
            internal string AnswerId;
        }

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

        private sealed class Alternative
        {
            internal string NodeId;
            internal string Price;
            internal string Lock;
            internal bool Supported = true;

            public override string ToString()
            {
                return "answerNode=" + Safe(NodeId) +
                       " price={" + Safe(Price) + "}" +
                       " lock={" + Safe(Lock) + "}" +
                       " supported=" + Supported;
            }
        }

        private static readonly Target[] Targets =
        {
            new Target { NpcId = "npc_astrologer", TaskId = "dlc_souls_s29_1", AnswerId = "@souls_s_s30_ask" },
            new Target { NpcId = "npc_inquisitor", TaskId = "dlc_souls_s21_2", AnswerId = "@souls_s_s22_ask" },
            new Target { NpcId = "npc_cultist", TaskId = "dlc_souls_s29_3", AnswerId = "@souls_s_s33_ask" },
            new Target { NpcId = "npc_merchant", TaskId = "dlc_souls_s23_2", AnswerId = "@souls_s_s24_ask" },
            new Target { NpcId = "npc_actress", TaskId = "dlc_souls_s29_2", AnswerId = "@souls_s_s31_ask" },
            new Target { NpcId = "npc_bishop", TaskId = "dlc_souls_s12_1", AnswerId = "@souls_s_s15_ask" }
        };

        private Type _mainGameType;
        private Type _worldMapType;
        private Type _controllerType;
        private MethodInfo _worldObjectGetter;
        private object _mainGame;
        private object _save;
        private bool _done;
        private float _nextAttempt;

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _worldMapType = ReflectionUtil.FindType("WorldMap");
            _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
            _worldObjectGetter = FindWorldObjectGetter();
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            Logger.LogInfo(PluginName + " " + PluginVersion +
                           " loaded. Read-only one-shot audit of six Better Save Soul weekday completion routes.");
        }

        private void Update()
        {
            if (_done || Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            if (!RuntimeReady()) return;

            try
            {
                AuditAll();
            }
            catch (Exception ex)
            {
                Logger.LogError("SOULS6_FATAL " + ex);
            }
            finally
            {
                _done = true;
                enabled = false;
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
            if (_mainGame == null) return false;
            return ReflectionUtil.TryRead(_mainGame, "save", out _save) && _save != null;
        }

        private void AuditAll()
        {
            Logger.LogInfo("SOULS6_BEGIN targets=" + Targets.Length);
            var complete = 0;
            for (var i = 0; i < Targets.Length; i++)
            {
                if (AuditTarget(Targets[i])) complete++;
            }
            Logger.LogInfo("SOULS6_DONE completeTargets=" + complete + "/" + Targets.Length);
        }

        private bool AuditTarget(Target target)
        {
            var serialized = ReadSerializedGraph(target.NpcId);
            if (string.IsNullOrEmpty(serialized))
            {
                Logger.LogError("SOULS6_TARGET npc=" + target.NpcId + " task=" + target.TaskId +
                                " answer=" + target.AnswerId + " graph=<missing>");
                return false;
            }

            var nodes = BuildNodeIndex(serialized);
            var connections = ParseConnections(serialized);
            var occurrences = 0;
            var supportedOccurrences = 0;

            foreach (var node in nodes.Values)
            {
                if (node == null || node.Type == null || !node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    continue;

                var answers = ReadMultiAnswers(serialized, node);
                for (var index = 0; index < answers.Count; index++)
                {
                    if (!string.Equals(answers[index], target.AnswerId, StringComparison.Ordinal)) continue;
                    occurrences++;
                    if (AuditOccurrence(target, serialized, nodes, connections, node, index))
                        supportedOccurrences++;
                }
            }

            Logger.LogInfo("SOULS6_TARGET_SUMMARY npc=" + target.NpcId +
                           " task=" + target.TaskId +
                           " answer=" + target.AnswerId +
                           " occurrences=" + occurrences +
                           " structurallyResolved=" + supportedOccurrences);
            return occurrences > 0 && occurrences == supportedOccurrences;
        }

        private bool AuditOccurrence(Target target, string serialized, Dictionary<string, Node> nodes,
            List<Connection> connections, Node multi, int answerIndex)
        {
            var producers = new List<Connection>();
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, multi.Id, StringComparison.Ordinal)) continue;
                if (ParseAnswerPortIndex(c.TargetPort) != answerIndex) continue;
                producers.Add(c);
            }

            Logger.LogInfo("SOULS6_OCCURRENCE npc=" + target.NpcId +
                           " answer=" + target.AnswerId +
                           " multi=" + multi.Id +
                           " index=" + answerIndex +
                           " producerConnections=" + producers.Count);

            if (producers.Count == 0)
            {
                Logger.LogInfo("SOULS6_SHAPE npc=" + target.NpcId + " answer=" + target.AnswerId +
                               " multi=" + multi.Id + " index=" + answerIndex +
                               " shape=NO_ANSWERDATA alternatives=1 supported=True");
                return true;
            }

            var allAlternatives = new List<Alternative>();
            var allSupported = true;
            for (var i = 0; i < producers.Count; i++)
            {
                Node producer;
                if (!nodes.TryGetValue(producers[i].SourceNode, out producer) || producer == null)
                {
                    allSupported = false;
                    Logger.LogWarning("SOULS6_PRODUCER missing=True source=" + Safe(producers[i].SourceNode));
                    continue;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var alternatives = new List<Alternative>();
                var supported = TraceProducer(serialized, nodes, connections, producer, seen, alternatives, 0,
                    target.NpcId + "/" + target.AnswerId + "/" + multi.Id + "#" + answerIndex);
                if (!supported) allSupported = false;
                allAlternatives.AddRange(alternatives);
            }

            for (var i = 0; i < allAlternatives.Count; i++)
                Logger.LogInfo("SOULS6_ALTERNATIVE npc=" + target.NpcId +
                               " answer=" + target.AnswerId +
                               " multi=" + multi.Id +
                               " index=" + answerIndex +
                               " alt=" + i + " " + allAlternatives[i]);

            Logger.LogInfo("SOULS6_SHAPE npc=" + target.NpcId +
                           " answer=" + target.AnswerId +
                           " multi=" + multi.Id +
                           " index=" + answerIndex +
                           " shape=" + DescribeTopShape(nodes, producers) +
                           " alternatives=" + allAlternatives.Count +
                           " supported=" + allSupported);
            return allSupported && allAlternatives.Count > 0;
        }

        private bool TraceProducer(string serialized, Dictionary<string, Node> nodes, List<Connection> connections,
            Node node, HashSet<string> seen, List<Alternative> alternatives, int depth, string path)
        {
            if (node == null || depth > 10 || !seen.Add(node.Id))
            {
                Logger.LogWarning("SOULS6_TRACE path=" + path + " depth=" + depth +
                                  " node=" + (node == null ? "<null>" : node.Id) +
                                  " stopped=cycle_or_depth");
                return false;
            }

            Logger.LogInfo("SOULS6_TRACE path=" + path +
                           " depth=" + depth +
                           " node=" + node.Id +
                           " type=" + Safe(node.Type) +
                           " fields=" + DescribeNodeFields(serialized, node));

            if (node.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) >= 0 &&
                node.Type.IndexOf("Flow_AnswersArray", StringComparison.Ordinal) < 0)
            {
                alternatives.Add(ParseAnswerAlternative(serialized, nodes, connections, node));
                return alternatives[alternatives.Count - 1].Supported;
            }

            if (node.Type.IndexOf("RelayValueOutput", StringComparison.Ordinal) >= 0)
            {
                var uid = ReadNodeDirectString(serialized, node, "_sourceInputUID", 1200);
                if (string.IsNullOrEmpty(uid))
                {
                    Logger.LogWarning("SOULS6_RELAY_OUTPUT node=" + node.Id + " uid=<missing>");
                    return false;
                }

                Node relayInput = null;
                foreach (var pair in nodes)
                {
                    var candidate = pair.Value;
                    if (candidate == null || candidate.Type == null ||
                        candidate.Type.IndexOf("RelayValueInput", StringComparison.Ordinal) < 0) continue;
                    var candidateUid = ReadNodeDirectString(serialized, candidate, "_UID", 1200);
                    if (!string.Equals(candidateUid, uid, StringComparison.Ordinal)) continue;
                    if (relayInput != null)
                    {
                        Logger.LogWarning("SOULS6_RELAY uid=" + uid + " ambiguousInputs=True");
                        return false;
                    }
                    relayInput = candidate;
                }

                if (relayInput == null)
                {
                    Logger.LogWarning("SOULS6_RELAY uid=" + uid + " input=<missing>");
                    return false;
                }

                Logger.LogInfo("SOULS6_RELAY output=" + node.Id +
                               " uid=" + uid +
                               " input=" + relayInput.Id +
                               " identifier=" + Safe(ReadNodeDirectString(serialized, relayInput, "identifier", 1600)));
                return TraceIncoming(serialized, nodes, connections, relayInput, seen, alternatives, depth + 1, path + "->relay");
            }

            if (node.Type.IndexOf("Flow_MultipleAnswer", StringComparison.Ordinal) >= 0)
            {
                return TraceIncomingPort(serialized, nodes, connections, node, "datas", seen, alternatives, depth + 1,
                    path + "->multiple");
            }

            if (node.Type.IndexOf("Flow_AnswersArray", StringComparison.Ordinal) >= 0)
            {
                return TraceIncoming(serialized, nodes, connections, node, seen, alternatives, depth + 1,
                    path + "->array");
            }

            if (node.Type.IndexOf("RelayValueInput", StringComparison.Ordinal) >= 0)
            {
                return TraceIncoming(serialized, nodes, connections, node, seen, alternatives, depth + 1,
                    path + "->relayInput");
            }

            Logger.LogWarning("SOULS6_UNSUPPORTED_PRODUCER node=" + node.Id + " type=" + Safe(node.Type));
            return false;
        }

        private bool TraceIncomingPort(string serialized, Dictionary<string, Node> nodes, List<Connection> connections,
            Node target, string port, HashSet<string> seen, List<Alternative> alternatives, int depth, string path)
        {
            var sources = new List<Node>();
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, target.Id, StringComparison.Ordinal)) continue;
                if (!string.Equals(c.TargetPort, port, StringComparison.OrdinalIgnoreCase)) continue;
                Node source;
                if (nodes.TryGetValue(c.SourceNode, out source) && source != null) sources.Add(source);
            }

            Logger.LogInfo("SOULS6_INCOMING_PORT target=" + target.Id + " port=" + port + " sources=" + sources.Count);
            if (sources.Count == 0) return false;
            var ok = true;
            for (var i = 0; i < sources.Count; i++)
            {
                var childSeen = new HashSet<string>(seen, StringComparer.Ordinal);
                if (!TraceProducer(serialized, nodes, connections, sources[i], childSeen, alternatives, depth, path))
                    ok = false;
            }
            return ok;
        }

        private bool TraceIncoming(string serialized, Dictionary<string, Node> nodes, List<Connection> connections,
            Node target, HashSet<string> seen, List<Alternative> alternatives, int depth, string path)
        {
            var sources = new List<Node>();
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, target.Id, StringComparison.Ordinal)) continue;
                if (IsFlowConnection(c)) continue;
                Node source;
                if (nodes.TryGetValue(c.SourceNode, out source) && source != null) sources.Add(source);
            }

            Logger.LogInfo("SOULS6_INCOMING target=" + target.Id + " sources=" + sources.Count);
            if (sources.Count == 0) return false;
            var ok = true;
            for (var i = 0; i < sources.Count; i++)
            {
                var childSeen = new HashSet<string>(seen, StringComparer.Ordinal);
                if (!TraceProducer(serialized, nodes, connections, sources[i], childSeen, alternatives, depth, path))
                    ok = false;
            }
            return ok;
        }

        private Alternative ParseAnswerAlternative(string serialized, Dictionary<string, Node> nodes,
            List<Connection> connections, Node answer)
        {
            var result = new Alternative
            {
                NodeId = answer.Id,
                Price = "<none>",
                Lock = "<none>"
            };

            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, answer.Id, StringComparison.Ordinal)) continue;
                if (!string.Equals(c.TargetPort, "price", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.TargetPort, "lock", StringComparison.OrdinalIgnoreCase)) continue;

                Node source;
                if (!nodes.TryGetValue(c.SourceNode, out source) || source == null ||
                    source.Type == null || source.Type.IndexOf("Flow_SmartRes", StringComparison.Ordinal) < 0)
                {
                    result.Supported = false;
                    var unsupported = "unsupported:" + Safe(c.SourceNode);
                    if (string.Equals(c.TargetPort, "price", StringComparison.OrdinalIgnoreCase)) result.Price = unsupported;
                    else result.Lock = unsupported;
                    continue;
                }

                var requirement = DescribeSmartResNode(serialized, source);
                if (string.IsNullOrEmpty(requirement))
                {
                    result.Supported = false;
                    requirement = "unparsed:" + source.Id;
                }

                if (string.Equals(c.TargetPort, "price", StringComparison.OrdinalIgnoreCase)) result.Price = requirement;
                else result.Lock = requirement;
            }

            return result;
        }

        private static string DescribeTopShape(Dictionary<string, Node> nodes, List<Connection> producers)
        {
            if (producers == null || producers.Count == 0) return "NO_ANSWERDATA";
            var parts = new List<string>();
            for (var i = 0; i < producers.Count; i++)
            {
                Node node;
                if (!nodes.TryGetValue(producers[i].SourceNode, out node) || node == null)
                    parts.Add("<missing>");
                else if (node.Type.IndexOf("RelayValueOutput", StringComparison.Ordinal) >= 0)
                    parts.Add("RELAY");
                else if (node.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) >= 0)
                    parts.Add("DIRECT_ANSWER");
                else
                    parts.Add(node.Type);
            }
            return string.Join("+", parts.ToArray());
        }

        private static string DescribeSmartResNode(string serialized, Node node)
        {
            var resType = ReadNodeContent(serialized, node, "res_type") ??
                          ReadNodeContent(serialized, node, "Res type");
            var id = ReadNodeContent(serialized, node, "id") ??
                     ReadNodeContent(serialized, node, "Id");
            float value;
            if (string.IsNullOrEmpty(resType) || string.IsNullOrEmpty(id) || !TryReadNodeNumber(serialized, node, out value))
                return null;
            return resType + ":" + id + "=" + value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string DescribeNodeFields(string serialized, Node node)
        {
            var sb = new StringBuilder();
            AppendField(sb, "identifier", ReadNodeDirectString(serialized, node, "identifier", 1600));
            AppendField(sb, "_UID", ReadNodeDirectString(serialized, node, "_UID", 1200));
            AppendField(sb, "_sourceInputUID", ReadNodeDirectString(serialized, node, "_sourceInputUID", 1200));
            AppendField(sb, "res_type", ReadNodeContent(serialized, node, "res_type") ?? ReadNodeContent(serialized, node, "Res type"));
            AppendField(sb, "id", ReadNodeContent(serialized, node, "id") ?? ReadNodeContent(serialized, node, "Id"));
            float value;
            if (TryReadNodeNumber(serialized, node, out value))
                AppendField(sb, "v", value.ToString("0.####", CultureInfo.InvariantCulture));
            return sb.Length == 0 ? "<none>" : sb.ToString();
        }

        private static void AppendField(StringBuilder sb, string name, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (sb.Length > 0) sb.Append(',');
            sb.Append(name).Append('=').Append(Safe(value));
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

        private static bool IsFlowConnection(Connection c)
        {
            return c != null &&
                   (string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(c.TargetPort));
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

        private static string ReadNodeContent(string serialized, Node node, string key)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 3500);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static bool TryReadNodeNumber(string serialized, Node node, out float value)
        {
            return TryReadNodeNumber(serialized, node, "v", out value) ||
                   TryReadNodeNumber(serialized, node, "V", out value);
        }

        private static bool TryReadNodeNumber(string serialized, Node node, string key, out float value)
        {
            value = 0f;
            if (node == null) return false;
            var begin = Math.Max(0, node.TypePosition - 3500);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return false;
            pos += marker.Length;
            while (pos < window.Length && char.IsWhiteSpace(window[pos])) pos++;
            var end = pos;
            while (end < window.Length &&
                   (char.IsDigit(window[end]) || window[end] == '-' || window[end] == '+' ||
                    window[end] == '.' || window[end] == 'e' || window[end] == 'E')) end++;
            return end > pos &&
                   float.TryParse(window.Substring(pos, end - pos), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out value);
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
