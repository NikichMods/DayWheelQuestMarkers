using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SoulsS33AnswerDataProbe : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.daywheel.souls-s33-answerdata-probe";
        public const string PluginName = "Day Wheel Quest Markers - souls s33 AnswerData probe";
        public const string PluginVersion = "0.1.1";

        private const string NpcId = "npc_cultist";
        private const string TaskId = "dlc_souls_s29_3";
        private const string AnswerId = "@souls_s_s33_ask";
        private const string MultiNodeId = "106";
        private const int AnswerIndex = 29;

        private static SoulsS33AnswerDataProbe _active;

        private Type _mainGameType;
        private Type _worldMapType;
        private Type _controllerType;
        private MethodInfo _worldObjectGetter;
        private object _mainGame;
        private object _save;
        private Harmony _harmony;
        private bool _snapshotDone;
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
            _active = this;
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _worldMapType = ReflectionUtil.FindType("WorldMap");
            _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
            _worldObjectGetter = FindWorldObjectGetter();
            _nextAttempt = Time.realtimeSinceStartup + 1f;

            InstallLiveHook();
            Logger.LogInfo(PluginName + " " + PluginVersion +
                           " loaded. Read-only: graph/save inspection plus target option observation; no save/UI mutations.");
        }

        private void Update()
        {
            if (_snapshotDone || Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            if (!RuntimeReady()) return;

            try
            {
                DumpStaticAndSaveState();
                _snapshotDone = true;
                enabled = false;
                Logger.LogInfo("S33_PROBE_STATIC_DONE live hook remains armed for " + AnswerId + ".");
            }
            catch (Exception ex)
            {
                Logger.LogError("S33_PROBE_FATAL " + ex);
                _snapshotDone = true;
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_active, this)) _active = null;
            if (_harmony != null)
            {
                try { _harmony.UnpatchSelf(); } catch { }
                _harmony = null;
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

            if (!ReflectionUtil.TryRead(_mainGame, "save", out _save) || _save == null) return false;
            return ReadSerializedGraph() != null;
        }

        private void InstallLiveHook()
        {
            try
            {
                var optionType = ReflectionUtil.FindType("MultiAnswerOptionGUI");
                if (optionType == null)
                {
                    Logger.LogError("S33_PROBE_HOOK_FAIL MultiAnswerOptionGUI type not found.");
                    return;
                }

                MethodInfo show = null;
                foreach (var method in optionType.GetMethods(ReflectionUtil.AnyInstance))
                {
                    if (!string.Equals(method.Name, "Show", StringComparison.Ordinal)) continue;
                    var p = method.GetParameters();
                    if (p.Length != 7) continue;
                    if (!string.Equals(p[0].ParameterType.Name, "AnswerVisualData", StringComparison.Ordinal)) continue;
                    show = method;
                    break;
                }

                if (show == null)
                {
                    Logger.LogError("S33_PROBE_HOOK_FAIL MultiAnswerOptionGUI.Show(AnswerVisualData,...) not found.");
                    return;
                }

                _harmony = new Harmony(PluginGuid + ".live");
                _harmony.Patch(show, prefix: new HarmonyMethod(typeof(SoulsS33AnswerDataProbe), nameof(OptionShowPrefix)));
                Logger.LogInfo("S33_PROBE_LIVE_READY hook=MultiAnswerOptionGUI.Show target=" + AnswerId);
            }
            catch (Exception ex)
            {
                Logger.LogError("S33_PROBE_HOOK_FAIL " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void OptionShowPrefix(object __0)
        {
            var active = _active;
            if (active == null || __0 == null) return;
            var id = ReadString(__0, "id");
            if (!string.Equals(id, AnswerId, StringComparison.Ordinal)) return;
            active.DumpLiveAnswer(__0);
        }

        private void DumpLiveAnswer(object visual)
        {
            try
            {
                var canPick = ReadBool(visual, "can_be_picked");
                var iconLock = ReadString(visual, "icon_lock");
                var iconLockQuality = ReadString(visual, "icon_lock_quality");
                var iconPrice = ReadString(visual, "icon_price");
                var nLock = ReadInt(visual, "n_lock");
                var nPrice = ReadInt(visual, "n_price");
                var answerData = ReadMember(visual, "link_to_answer_data");

                Logger.LogInfo("S33_LIVE_OPTION answer=" + AnswerId +
                               " canBePicked=" + BoolText(canPick) +
                               " iconLock=" + Safe(iconLock) +
                               " iconLockQuality=" + Safe(iconLockQuality) +
                               " nLock=" + IntText(nLock) +
                               " iconPrice=" + Safe(iconPrice) +
                               " nPrice=" + IntText(nPrice) +
                               " answerDataType=" + (answerData == null ? "<null>" : answerData.GetType().FullName));

                DumpNestedVisuals(visual);

                if (answerData == null)
                {
                    Logger.LogWarning("S33_LIVE_ANSWERDATA missing=True");
                    return;
                }

                var price = ReadMember(answerData, "d_price");
                var gate = ReadMember(answerData, "d_lock");
                var reward = ReadMember(answerData, "d_reward");
                Logger.LogInfo("S33_LIVE_PRICE " + DescribeSmartRes(price) + " enough=" + EnoughText(price));
                Logger.LogInfo("S33_LIVE_LOCK " + DescribeSmartRes(gate) + " enough=" + EnoughText(gate));
                Logger.LogInfo("S33_LIVE_REWARD " + DescribeSmartRes(reward));
            }
            catch (Exception ex)
            {
                Logger.LogError("S33_LIVE_FATAL " + ex);
            }
        }

        private void DumpNestedVisuals(object visual)
        {
            var nested = ReadMember(visual, "answer_visual_datas") as IEnumerable;
            if (nested == null)
            {
                Logger.LogInfo("S33_LIVE_CHILDREN count=0 collection=<null>");
                return;
            }

            var index = 0;
            foreach (var child in nested)
            {
                if (child == null)
                {
                    Logger.LogInfo("S33_LIVE_CHILD index=" + index + " value=<null>");
                    index++;
                    continue;
                }

                var childData = ReadMember(child, "link_to_answer_data");
                var price = childData == null ? null : ReadMember(childData, "d_price");
                var gate = childData == null ? null : ReadMember(childData, "d_lock");
                Logger.LogInfo("S33_LIVE_CHILD index=" + index +
                               " id=" + Safe(ReadString(child, "id")) +
                               " translation=" + Safe(ReadString(child, "translation")) +
                               " canBePicked=" + BoolText(ReadBool(child, "can_be_picked")) +
                               " insidePriceIsRed=" + BoolText(ReadBool(child, "inside_price_is_red")) +
                               " iconLock=" + Safe(ReadString(child, "icon_lock")) +
                               " nLock=" + IntText(ReadInt(child, "n_lock")) +
                               " iconPrice=" + Safe(ReadString(child, "icon_price")) +
                               " nPrice=" + IntText(ReadInt(child, "n_price")) +
                               " answerDataType=" + (childData == null ? "<null>" : childData.GetType().FullName));
                Logger.LogInfo("S33_LIVE_CHILD_GATE index=" + index +
                               " price={" + DescribeSmartRes(price) + "} priceEnough=" + EnoughText(price) +
                               " lock={" + DescribeSmartRes(gate) + "} lockEnough=" + EnoughText(gate));
                index++;
            }
            Logger.LogInfo("S33_LIVE_CHILDREN count=" + index);
        }

        private void DumpStaticAndSaveState()
        {
            var serialized = ReadSerializedGraph();
            if (serialized == null) throw new InvalidOperationException("npc_cultist serialized graph unavailable.");

            var nodes = BuildNodeIndex(serialized);
            var connections = ParseConnections(serialized);

            Node multi;
            if (!nodes.TryGetValue(MultiNodeId, out multi))
                throw new InvalidOperationException("Flow_MultiAnswer node 106 missing.");

            var answers = ReadMultiAnswers(serialized, multi);
            var authored = AnswerIndex >= 0 && AnswerIndex < answers.Count ? answers[AnswerIndex] : null;
            Logger.LogInfo("S33_STATIC_MENU multi=" + MultiNodeId +
                           " index=" + AnswerIndex +
                           " authored=" + Safe(authored) +
                           " answers=" + answers.Count);

            Connection answerDataConnection = null;
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, MultiNodeId, StringComparison.Ordinal)) continue;
                if (c.TargetPort == null || c.TargetPort.IndexOf("#" + AnswerIndex, StringComparison.Ordinal) < 0) continue;
                answerDataConnection = c;
                break;
            }

            if (answerDataConnection == null)
            {
                Logger.LogError("S33_STATIC_ANSWERDATA_CONNECTION missing=True target=106/#29");
            }
            else
            {
                Node producer;
                nodes.TryGetValue(answerDataConnection.SourceNode, out producer);
                Logger.LogInfo("S33_STATIC_ANSWERDATA_CONNECTION source=" + Safe(answerDataConnection.SourceNode) +
                               " sourcePort=" + Safe(answerDataConnection.SourcePort) +
                               " targetPort=" + Safe(answerDataConnection.TargetPort) +
                               " sourceType=" + (producer == null ? "<unknown>" : Safe(producer.Type)));

                if (producer != null)
                {
                    Logger.LogInfo("S33_STATIC_PRODUCER node=" + producer.Id +
                                   " type=" + Safe(producer.Type) +
                                   " fields=" + DescribeNodeFields(serialized, producer));

                    for (var i = 0; i < connections.Count; i++)
                    {
                        var incoming = connections[i];
                        if (!string.Equals(incoming.TargetNode, producer.Id, StringComparison.Ordinal)) continue;
                        Node source;
                        nodes.TryGetValue(incoming.SourceNode, out source);
                        Logger.LogInfo("S33_STATIC_PRODUCER_INPUT targetPort=" + Safe(incoming.TargetPort) +
                                       " source=" + Safe(incoming.SourceNode) +
                                       " sourcePort=" + Safe(incoming.SourcePort) +
                                       " sourceType=" + (source == null ? "<unknown>" : Safe(source.Type)) +
                                       " sourceFields=" + (source == null ? "<unknown>" : DescribeNodeFields(serialized, source)));
                    }

                    Logger.LogInfo("S33_STATIC_PRODUCER_RAW " + Safe(NodeRawWindow(serialized, producer)));

                    DumpRelayProducer(serialized, nodes, connections, producer);
                }
            }

            DumpTaskState();
            DumpPhraseState();
        }


        private void DumpRelayProducer(string serialized, Dictionary<string, Node> nodes,
            List<Connection> connections, Node producer)
        {
            if (producer == null || producer.Type == null ||
                producer.Type.IndexOf("RelayValueOutput", StringComparison.Ordinal) < 0) return;

            var uid = ReadNodeDirectString(serialized, producer, "_sourceInputUID", 900);
            Logger.LogInfo("S33_STATIC_RELAY_OUTPUT node=" + producer.Id + " sourceInputUID=" + Safe(uid));
            if (string.IsNullOrEmpty(uid)) return;

            Node relayInput = null;
            foreach (var pair in nodes)
            {
                var node = pair.Value;
                if (node == null || node.Type == null ||
                    node.Type.IndexOf("RelayValueInput", StringComparison.Ordinal) < 0) continue;
                var nodeUid = ReadNodeDirectString(serialized, node, "_UID", 900);
                if (!string.Equals(nodeUid, uid, StringComparison.Ordinal)) continue;
                relayInput = node;
                break;
            }

            if (relayInput == null)
            {
                Logger.LogWarning("S33_STATIC_RELAY_INPUT missing=True uid=" + Safe(uid));
                return;
            }

            Logger.LogInfo("S33_STATIC_RELAY_INPUT node=" + relayInput.Id +
                           " type=" + Safe(relayInput.Type) +
                           " identifier=" + Safe(ReadNodeDirectString(serialized, relayInput, "identifier", 1200)));

            var incomingCount = 0;
            for (var i = 0; i < connections.Count; i++)
            {
                var incoming = connections[i];
                if (!string.Equals(incoming.TargetNode, relayInput.Id, StringComparison.Ordinal)) continue;
                incomingCount++;
                Node source;
                nodes.TryGetValue(incoming.SourceNode, out source);
                Logger.LogInfo("S33_STATIC_RELAY_INPUT_SOURCE targetPort=" + Safe(incoming.TargetPort) +
                               " source=" + Safe(incoming.SourceNode) +
                               " sourcePort=" + Safe(incoming.SourcePort) +
                               " sourceType=" + (source == null ? "<unknown>" : Safe(source.Type)) +
                               " sourceFields=" + (source == null ? "<unknown>" : DescribeNodeFields(serialized, source)));
            }
            Logger.LogInfo("S33_STATIC_RELAY_INPUT_SOURCES count=" + incomingCount);
        }

        private void DumpTaskState()
        {
            object known;
            if (_save == null || !ReflectionUtil.TryRead(_save, "known_npcs", out known) || known == null)
            {
                Logger.LogWarning("S33_TASK_STATE known_npcs unavailable.");
                return;
            }

            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null)
            {
                Logger.LogWarning("S33_TASK_STATE known_npcs.npcs unavailable.");
                return;
            }

            foreach (var npc in npcs)
            {
                if (!string.Equals(ReflectionUtil.ReadString(npc, "npc_id"), NpcId, StringComparison.Ordinal)) continue;
                var tasks = ReflectionUtil.EnumerateMember(npc, "tasks");
                if (tasks == null)
                {
                    Logger.LogWarning("S33_TASK_STATE npc found but tasks unavailable.");
                    return;
                }

                foreach (var task in tasks)
                {
                    if (!string.Equals(ReflectionUtil.ReadString(task, "id"), TaskId, StringComparison.Ordinal)) continue;
                    object state;
                    ReflectionUtil.TryRead(task, "state", out state);
                    Logger.LogInfo("S33_TASK_STATE npc=" + NpcId + " task=" + TaskId +
                                   " stateRaw=" + (state == null ? "<null>" : state.ToString()) +
                                   " visibleByProductionRule=" + IsZero(state));
                    return;
                }

                Logger.LogWarning("S33_TASK_STATE task=" + TaskId + " not found under npc_cultist.");
                return;
            }

            Logger.LogWarning("S33_TASK_STATE npc_cultist not found in known_npcs.");
        }

        private void DumpPhraseState()
        {
            object unlocked;
            object blacklisted;
            ReflectionUtil.TryRead(_save, "unlocked_phrases", out unlocked);
            ReflectionUtil.TryRead(_save, "black_list_of_phrases", out blacklisted);
            var isUnlocked = ContainsString(unlocked, AnswerId);
            var isBlacklisted = ContainsString(blacklisted, AnswerId);
            Logger.LogInfo("S33_PHRASE_STATE answer=" + AnswerId +
                           " unlocked=" + isUnlocked +
                           " blacklisted=" + isBlacklisted +
                           " openByProductionRule=" + (!isBlacklisted && isUnlocked));
        }

        private string EnoughText(object smartRes)
        {
            if (smartRes == null) return "<null>";
            if (_mainGame == null) return "<no-main-game>";
            object player;
            if (!ReflectionUtil.TryRead(_mainGame, "player", out player) || player == null) return "<no-player>";

            try
            {
                foreach (var method in player.GetType().GetMethods(ReflectionUtil.AnyInstance))
                {
                    if (!string.Equals(method.Name, "IsEnough", StringComparison.Ordinal)) continue;
                    var p = method.GetParameters();
                    if (p.Length != 1 || !p[0].ParameterType.IsAssignableFrom(smartRes.GetType())) continue;
                    var result = method.Invoke(player, new[] { smartRes });
                    return result is bool ? ((bool)result).ToString() : "<non-bool>";
                }
            }
            catch (Exception ex)
            {
                return "<error:" + ex.GetType().Name + ">";
            }
            return "<method-missing>";
        }

        private static string DescribeSmartRes(object smartRes)
        {
            if (smartRes == null) return "smartRes=<null>";
            var resType = ReadMember(smartRes, "res_type");
            var item = ReadMember(smartRes, "item");
            var gameRes = ReadMember(smartRes, "_res");
            var linked = ReadMember(smartRes, "_linked_wgo");
            var v = ReadMember(smartRes, "v");

            var sb = new StringBuilder();
            sb.Append("type=").Append(resType == null ? "<null>" : Safe(resType.ToString()));
            sb.Append(" v=").Append(v == null ? "<null>" : Safe(v.ToString()));

            if (item != null)
            {
                sb.Append(" itemType=").Append(item.GetType().FullName);
                sb.Append(" itemId=").Append(Safe(ReadString(item, "id")));
                var itemValue = ReadMember(item, "value");
                if (itemValue != null) sb.Append(" itemValue=").Append(Safe(itemValue.ToString()));
            }
            else sb.Append(" item=<null>");

            if (gameRes != null)
            {
                sb.Append(" gameResType=").Append(gameRes.GetType().FullName);
                sb.Append(" gameResAtomType=").Append(Safe(ReadString(gameRes, "type")));
                var value = ReadMember(gameRes, "value");
                if (value != null) sb.Append(" gameResValue=").Append(Safe(value.ToString()));
            }
            else sb.Append(" gameRes=<null>");

            if (linked != null) sb.Append(" linked=").Append(Safe(linked.ToString()));
            return sb.ToString();
        }

        private static string DescribeNodeFields(string serialized, Node node)
        {
            var keys = new[]
            {
                "res_type", "id", "v", "price", "lock", "reward",
                "Text", "Task", "State", "NPC id", "Phrase", "Phrase ID"
            };
            var sb = new StringBuilder();
            for (var i = 0; i < keys.Length; i++)
            {
                var value = ReadNodeContent(serialized, node, keys[i]);
                if (string.IsNullOrEmpty(value)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(keys[i].Replace(' ', '_')).Append('=').Append(Safe(value));
            }
            return sb.Length == 0 ? "<none>" : sb.ToString();
        }

        private static string NodeRawWindow(string serialized, Node node)
        {
            if (serialized == null || node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 4500);
            var length = Math.Min(serialized.Length - begin, node.TypePosition - begin + 350);
            return serialized.Substring(begin, length);
        }

        private string ReadSerializedGraph()
        {
            try
            {
                var wgo = _worldObjectGetter.Invoke(null, new object[] { NpcId, true });
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
                if (window[p] != '"') break;
                int end;
                var value = ReadJsonString(window, p + 1, out end);
                if (value == null) break;
                result.Add(value);
                p = end + 1;
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
                if (ch != '"') continue;
                end = i;
                var raw = text.Substring(start, i - start);
                try { return Regex.Unescape(raw.Replace("\\/", "/")); }
                catch { return raw; }
            }
            return null;
        }

        private static object ReadMember(object owner, string name)
        {
            if (owner == null || string.IsNullOrEmpty(name)) return null;
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    try { return field.GetValue(owner); } catch { return null; }
                }

                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    try { return property.GetValue(owner, null); } catch { return null; }
                }
            }
            return null;
        }

        private static string ReadString(object owner, string name)
        {
            var value = ReadMember(owner, name);
            return value == null ? null : value.ToString();
        }

        private static bool? ReadBool(object owner, string name)
        {
            var value = ReadMember(owner, name);
            return value is bool ? (bool?)value : null;
        }

        private static int? ReadInt(object owner, string name)
        {
            var value = ReadMember(owner, name);
            if (value == null) return null;
            try { return Convert.ToInt32(value); } catch { return null; }
        }

        private static bool ContainsString(object collection, string value)
        {
            var enumerable = collection as IEnumerable;
            if (enumerable == null) return false;
            foreach (var item in enumerable)
                if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsZero(object value)
        {
            if (value == null) return false;
            try { return Convert.ToInt32(value) == 0; } catch { return false; }
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

        private static string BoolText(bool? value)
        {
            return value.HasValue ? value.Value.ToString() : "<null>";
        }

        private static string IntText(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "<null>";
        }
    }
}
