using Newtonsoft.Json;

[System.Serializable]
public class BaseMsg
{
    [JsonProperty("msg_type")] public string msg_type;  // "event" or "alert"
    [JsonProperty("timestamp")] public double? timestamp;
}

[System.Serializable]

public class EventMsg : BaseMsg
{
    [JsonProperty("event")] public string event_name;       // "checkin", "conveyor_progress", ...
    [JsonProperty("component")] public string component;    // "CONV_CI1", "STORAGE", "MCS", ...

    [JsonProperty("bag_id")] public string bag_id;
    [JsonProperty("expected_branch")] public string expected_branch;  // diverter
    [JsonProperty("chosen_branch")] public string chosen_branch;      // diverter
    [JsonProperty("queue_length")] public int? queue_length;
    [JsonProperty("throughput")] public double? throughput;
    [JsonProperty("transit_time")] public double? transit_time;
    [JsonProperty("attack_flag")] public string attack_flag;

    
    [JsonProperty("progress")] public double? progress;
}

[System.Serializable]
public class AlertMsg : BaseMsg
{
    [JsonProperty("attack_type")] public string attack_type;          // "dos", "fdi", "spoof", "stopped conveyor"
    [JsonProperty("confidence")] public double? confidence;
    [JsonProperty("affected_component")] public string affected_component;
    [JsonProperty("description")] public string description;
}
