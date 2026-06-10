using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ConveyorPath
{
    public string conveyorName;
    public Transform[] waypoints;
}

public class BHSManager : MonoBehaviour
{
    [Header("Network")]
    public TcpJsonClient client;

    [Header("Bag")]
    public GameObject bagPrefab;

    [Tooltip("Optional: use same prefab for phantom hits (will auto-destroy).")]
    public bool spawnPhantomBags = true;

    public float phantomLifetimeSeconds = 1.5f;
    public float phantomScale = 0.7f;

    [Header("Spawn nodes")]
    public Transform CHECKIN1;
    public Transform CHECKIN2;

    [Header("Conveyor geometry (must mirror bhs_sim)")]
    public ConveyorPath[] conveyorPaths;

    [Header("Highlighters (assign root objects)")]
    public Highlighter H_CHECKIN1;
    public Highlighter H_CHECKIN2;

    public Highlighter H_CONV_CI1;
    public Highlighter H_CONV_CI2;
    public Highlighter H_CONV_X1;
    public Highlighter H_CONV_X2;

    public Highlighter H_CONV_TO_MCS;
    public Highlighter H_CONV_MCS_TO_MAIN;
    public Highlighter H_CONV_TO_STORAGE;
    public Highlighter H_CONV_STORAGE_TO_MAIN;

    public Highlighter H_CONV_MAIN;
    public Highlighter H_CONV_MAIN_D1;
    public Highlighter H_CONV_MAIN_D2;
    public Highlighter H_CONV_MAIN_D3;
    public Highlighter H_CONV_TO_A;
    public Highlighter H_CONV_TO_B;
    public Highlighter H_CONV_TO_C;
    public Highlighter H_CONV_TO_D;

    public Highlighter H_S_MAIN;
    public Highlighter H_D1;
    public Highlighter H_D2;
    public Highlighter H_D3;
    public Highlighter H_D4;
    public Highlighter H_BUILD_A;
    public Highlighter H_BUILD_B;
    public Highlighter H_BUILD_C;
    public Highlighter H_BUILD_D;
    public Highlighter H_MCS;
    public Highlighter H_STORAGE;

    [Header("Humans (optional)")]
    public BHSPersonHandler personCheckin1;
    public BHSPersonHandler personCheckin2;
    public BHSPersonHandler personMCS;

    [Header("Robots (optional)")]
    public BHSRobotArm robotStorage;
    public BHSRobotArm robotBuildA;
    public BHSRobotArm robotBuildB;
    public BHSRobotArm robotBuildC;
    public BHSRobotArm robotBuildD;

    // Internal state
    private readonly Dictionary<string, BagAgent> _bags = new Dictionary<string, BagAgent>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ConveyorPath> _pathByName;
    private Dictionary<string, Highlighter> _highByName;

    
    private readonly Dictionary<string, string> _bagLastFlag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _lastQueueLen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private double _lastThroughput = 0.0;

    void Awake()
    {
        if (!client) client = GetComponent<TcpJsonClient>();

        _pathByName = new Dictionary<string, ConveyorPath>(StringComparer.OrdinalIgnoreCase);
        if (conveyorPaths != null)
        {
            foreach (var cp in conveyorPaths)
            {
                if (cp == null || string.IsNullOrEmpty(cp.conveyorName)) continue;
                if (!_pathByName.ContainsKey(cp.conveyorName))
                    _pathByName.Add(cp.conveyorName, cp);
            }
        }

        _highByName = new Dictionary<string, Highlighter>(StringComparer.OrdinalIgnoreCase)
        {
            { "CHECKIN1", H_CHECKIN1 }, { "CHECKIN2", H_CHECKIN2 },

            { "CONV_CI1", H_CONV_CI1 }, { "CONV_CI2", H_CONV_CI2 },
            { "CONV_X1", H_CONV_X1 },   { "CONV_X2", H_CONV_X2 },

            { "CONV_TO_MCS", H_CONV_TO_MCS },
            { "CONV_MCS_TO_MAIN", H_CONV_MCS_TO_MAIN },
            { "CONV_TO_STORAGE", H_CONV_TO_STORAGE },
            { "CONV_STORAGE_TO_MAIN", H_CONV_STORAGE_TO_MAIN },

            { "CONV_MAIN", H_CONV_MAIN },
            { "CONV_MAIN_D1", H_CONV_MAIN_D1 },
            { "CONV_MAIN_D2", H_CONV_MAIN_D2 },
            { "CONV_MAIN_D3", H_CONV_MAIN_D3 },

            { "CONV_TO_A", H_CONV_TO_A }, { "CONV_TO_B", H_CONV_TO_B },
            { "CONV_TO_C", H_CONV_TO_C }, { "CONV_TO_D", H_CONV_TO_D },

            { "S_MAIN", H_S_MAIN },
            { "D1", H_D1 }, { "D2", H_D2 }, { "D3", H_D3 }, { "D4", H_D4 },

            { "BUILD_A", H_BUILD_A }, { "BUILD_B", H_BUILD_B },
            { "BUILD_C", H_BUILD_C }, { "BUILD_D", H_BUILD_D },

            { "MCS", H_MCS },
            { "STORAGE", H_STORAGE },
        };
    }

    void Update()
    {
        if (client == null) return;

        int processed = 0;
        while (processed < 300 && client.IncomingLines.TryDequeue(out string line))
        {
            processed++;
            HandleLine(line);
        }
    }

    void HandleLine(string line)
    {
        try
        {
            var baseMsg = JsonConvert.DeserializeObject<BaseMsg>(line);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.msg_type)) return;

            if (string.Equals(baseMsg.msg_type, "event", StringComparison.OrdinalIgnoreCase))
            {
                var ev = JsonConvert.DeserializeObject<EventMsg>(line);
                OnEvent(ev);
            }
            else if (string.Equals(baseMsg.msg_type, "alert", StringComparison.OrdinalIgnoreCase))
            {
                var al = JsonConvert.DeserializeObject<AlertMsg>(line);
                OnAlert(al);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BHSManager] Bad JSON: {e.Message}\n{line}");
        }
    }

    void OnEvent(EventMsg ev)
    {
        if (ev == null) return;

        string et = ev.event_name;
        string comp = ev.component;
        string bagId = ev.bag_id;

        string flag = NormalizeFlag(ev.attack_flag);

        bool hasBagId = !string.IsNullOrEmpty(bagId);
        bool isPhantom = hasBagId && IsPhantomBagId(bagId);

        
        if (isPhantom)
        {
            if (spawnPhantomBags && !string.IsNullOrEmpty(flag))
                EnsurePhantomBag(bagId, comp, flag);

            // Flash sensor anomaly for spoof_sensor
            if (et == "sensor_trigger") Flash(comp, true);
            return;
        }

        
        if (hasBagId && !string.IsNullOrEmpty(flag) && flag != "spoof_sensor")
        {
            _bagLastFlag[bagId] = flag;
        }

        switch (et)
        {
            case "checkin":
                SpawnBagAtCheckin(bagId, comp);
                ApplyCachedFlag(bagId);

                Flash(comp, isAnomaly: _bagLastFlag.ContainsKey(bagId));
                TriggerPersonAtCheckin(comp);
                break;

            case "conveyor_progress":
                if (hasBagId && ev.progress.HasValue)
                    UpdateBagPosition(bagId, comp, (float)ev.progress.Value);

                ApplyCachedFlag(bagId);
                Flash(comp, isAnomaly: _bagLastFlag.ContainsKey(bagId));
                break;

            case "sensor_trigger":
                
                Flash(comp, isAnomaly: !string.IsNullOrEmpty(flag));
                ApplyCachedFlag(bagId);
                break;

            case "diverter_decision":
                bool misroute =
                    !string.IsNullOrEmpty(ev.expected_branch) &&
                    !string.IsNullOrEmpty(ev.chosen_branch) &&
                    !ev.expected_branch.Equals(ev.chosen_branch, StringComparison.OrdinalIgnoreCase);

                if (misroute)
                    Debug.Log($"[BHS] Misroute at {comp} for bag {bagId}");

                ApplyCachedFlag(bagId);
                Flash(comp, isAnomaly: misroute || _bagLastFlag.ContainsKey(bagId));
                break;

            case "build_arrival":
                Flash(comp, isAnomaly: _bagLastFlag.ContainsKey(bagId));
                TriggerRobotAtBuild(comp);

                if (hasBagId) _bagLastFlag.Remove(bagId);
                CompleteBag(bagId);
                break;

            case "storage_enter":
            case "storage_exit":
                Flash("STORAGE", isAnomaly: _bagLastFlag.ContainsKey(bagId));
                TriggerRobotStorage();
                break;

            case "mcs_enter":
            case "mcs_exit":
                Flash("MCS", isAnomaly: _bagLastFlag.ContainsKey(bagId));
                TriggerPersonMCS();
                break;

            case "queue_len":
                if (!string.IsNullOrEmpty(comp) && ev.queue_length.HasValue)
                    _lastQueueLen[comp] = ev.queue_length.Value;
                break;

            case "throughput":
                if (ev.throughput.HasValue)
                    _lastThroughput = ev.throughput.Value;
                break;

            default:
                ApplyCachedFlag(bagId);
                break;
        }
    }

    void OnAlert(AlertMsg alert)
    {
        if (alert == null) return;

        if (!string.IsNullOrEmpty(alert.affected_component))
            Flash(alert.affected_component, true);

        string type = alert.attack_type ?? "attack";
        string comp = alert.affected_component ?? "unknown";
        string desc = alert.description ?? "";
        string conf = alert.confidence.HasValue ? alert.confidence.Value.ToString("P0") : "?";

        Debug.Log($"[ALERT] {type} ({conf}) on {comp} :: {desc}");
    }

    void SpawnBagAtCheckin(string bagId, string checkinComponent)
    {
        if (string.IsNullOrEmpty(bagId)) return;
        if (_bags.ContainsKey(bagId)) return;

        Transform spawn = null;
        if (string.Equals(checkinComponent, "CHECKIN1", StringComparison.OrdinalIgnoreCase)) spawn = CHECKIN1;
        else if (string.Equals(checkinComponent, "CHECKIN2", StringComparison.OrdinalIgnoreCase)) spawn = CHECKIN2;

        if (!spawn) spawn = CHECKIN1;

        if (!bagPrefab)
        {
            Debug.LogError("[BHS] Bag prefab not assigned.");
            return;
        }

        var go = Instantiate(bagPrefab, spawn.position, Quaternion.identity, transform);
        go.name = bagId;

        var agent = go.GetComponent<BagAgent>();
        if (!agent) agent = go.AddComponent<BagAgent>();

        _bags[bagId] = agent;

        
        ApplyCachedFlag(bagId);
    }

    void EnsurePhantomBag(string bagId, string componentName, string flag)
    {
        if (string.IsNullOrEmpty(bagId) || _bags.ContainsKey(bagId)) return;
        if (!bagPrefab) return;

        Transform spawn = GetComponentTransform(componentName);
        if (!spawn) spawn = (H_S_MAIN != null) ? H_S_MAIN.transform : CHECKIN1;

        var go = Instantiate(bagPrefab, spawn.position, Quaternion.identity, transform);
        go.name = bagId;
        go.transform.localScale *= phantomScale;

        var agent = go.GetComponent<BagAgent>();
        if (!agent) agent = go.AddComponent<BagAgent>();

        agent.SetAttackFlag(flag);
        _bags[bagId] = agent;

        Destroy(go, Mathf.Max(0.2f, phantomLifetimeSeconds));
        Invoke(nameof(PruneDestroyedBags), phantomLifetimeSeconds + 0.1f);
    }

    void PruneDestroyedBags()
    {
        var toRemove = new List<string>();
        foreach (var kv in _bags)
            if (kv.Value == null) toRemove.Add(kv.Key);

        foreach (var k in toRemove) _bags.Remove(k);
    }

    void UpdateBagPosition(string bagId, string conveyorName, float progress)
    {
        if (!_bags.TryGetValue(bagId, out var agent) || agent == null) return;

        if (!_pathByName.TryGetValue(conveyorName, out var path)) return;
        if (path.waypoints == null || path.waypoints.Length == 0) return;

        Vector3 pos = GetPointOnPath(path.waypoints, Mathf.Clamp01(progress));
        agent.SetTarget(pos);
    }

    void CompleteBag(string bagId)
    {
        if (_bags.TryGetValue(bagId, out var agent) && agent != null)
            Destroy(agent.gameObject, 0.2f);

        _bags.Remove(bagId);
    }

    // ---------------- helpers ----------------

    string NormalizeFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return null;
        return flag.Trim().ToLowerInvariant();
    }

    void ApplyCachedFlag(string bagId)
    {
        if (string.IsNullOrEmpty(bagId)) return;

        if (_bags.TryGetValue(bagId, out var agent) && agent != null)
        {
            if (_bagLastFlag.TryGetValue(bagId, out var last) && !string.IsNullOrEmpty(last))
                agent.SetAttackFlag(last);
        }
    }

    bool IsPhantomBagId(string bagId)
    {
        return !string.IsNullOrEmpty(bagId) &&
               bagId.StartsWith("PHANTOM_", StringComparison.OrdinalIgnoreCase);
    }

    Transform GetComponentTransform(string componentName)
    {
        if (string.IsNullOrEmpty(componentName)) return null;

        if (_highByName != null && _highByName.TryGetValue(componentName, out var h) && h != null)
            return h.transform;

        if (string.Equals(componentName, "CHECKIN1", StringComparison.OrdinalIgnoreCase)) return CHECKIN1;
        if (string.Equals(componentName, "CHECKIN2", StringComparison.OrdinalIgnoreCase)) return CHECKIN2;

        return null;
    }

    Vector3 GetPointOnPath(Transform[] wps, float t)
    {
        if (wps == null || wps.Length == 0) return Vector3.zero;
        if (t <= 0f) return wps[0].position;
        if (t >= 1f) return wps[wps.Length - 1].position;

        float total = 0f;
        float[] segLen = new float[wps.Length - 1];
        for (int i = 0; i < wps.Length - 1; i++)
        {
            float l = Vector3.Distance(wps[i].position, wps[i + 1].position);
            segLen[i] = l;
            total += l;
        }

        float d = t * total;
        for (int i = 0; i < segLen.Length; i++)
        {
            if (d > segLen[i]) d -= segLen[i];
            else
            {
                float r = segLen[i] > 1e-6f ? d / segLen[i] : 0f;
                return Vector3.Lerp(wps[i].position, wps[i + 1].position, r);
            }
        }
        return wps[wps.Length - 1].position;
    }

    void Flash(string componentName, bool isAnomaly)
    {
        if (string.IsNullOrEmpty(componentName)) return;

        if (_highByName != null && _highByName.TryGetValue(componentName, out var h) && h != null)
        {
            if (isAnomaly) h.FlashAnomaly();
            else h.FlashNormal();
        }
    }

    void TriggerPersonAtCheckin(string comp)
    {
        if (string.Equals(comp, "CHECKIN1", StringComparison.OrdinalIgnoreCase)) personCheckin1?.OnBagEvent();
        else if (string.Equals(comp, "CHECKIN2", StringComparison.OrdinalIgnoreCase)) personCheckin2?.OnBagEvent();
    }

    void TriggerPersonMCS() => personMCS?.OnBagEvent();

    void TriggerRobotStorage() => robotStorage?.HandleBagEvent();

    void TriggerRobotAtBuild(string comp)
    {
        if (string.Equals(comp, "BUILD_A", StringComparison.OrdinalIgnoreCase)) robotBuildA?.HandleBagEvent();
        else if (string.Equals(comp, "BUILD_B", StringComparison.OrdinalIgnoreCase)) robotBuildB?.HandleBagEvent();
        else if (string.Equals(comp, "BUILD_C", StringComparison.OrdinalIgnoreCase)) robotBuildC?.HandleBagEvent();
        else if (string.Equals(comp, "BUILD_D", StringComparison.OrdinalIgnoreCase)) robotBuildD?.HandleBagEvent();
    }
}
