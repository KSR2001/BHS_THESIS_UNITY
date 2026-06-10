using UnityEngine;

public class BagAgent : MonoBehaviour
{
    [Header("Motion")]
    public float speed = 2.5f;
    public float reachThreshold = 0.01f;

    [Header("Visuals")]
    public Renderer[] renderers;

    public Color normalColor = Color.white;
    public Color spoofColor = Color.green;     
    public Color misrouteColor = Color.red;    
    public Color phantomColor = Color.cyan;    

    [Tooltip("How long to keep an attack colour if no further updates occur. Set very high to keep forever.")]
    public float attackColorHoldSeconds = 999999f;

    private bool _hasTarget;
    private Vector3 _target;

    private string _lastFlag = null;
    private float _lastFlagWallTime = -999f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");        
    private static readonly int ColorId = Shader.PropertyToID("_Color");               
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");    

    private MaterialPropertyBlock _mpb;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        _mpb = new MaterialPropertyBlock();
    }

    public void SetAttackFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;

        flag = flag.Trim().ToLowerInvariant();

        
        if (_lastFlag == "spoof_rfid" && flag == "spoof_sensor")
            return;

        _lastFlag = flag;
        _lastFlagWallTime = Time.time;

        ApplyColor(FlagToColor(flag));
    }

    void Update()
    {
        // Movement
        if (_hasTarget)
        {
            var pos = transform.position;
            var step = speed * Time.deltaTime;

            if (Vector3.Distance(pos, _target) <= reachThreshold)
            {
                transform.position = _target;
                _hasTarget = false;
            }
            else
            {
                transform.position = Vector3.MoveTowards(pos, _target, step);
            }
        }

        
        if (!string.IsNullOrEmpty(_lastFlag))
        {
            if (Time.time - _lastFlagWallTime > Mathf.Max(0.1f, attackColorHoldSeconds))
            {
                _lastFlag = null;
                ApplyColor(normalColor);
            }
        }
    }

    private Color FlagToColor(string flag)
    {
        if (flag == "spoof_sensor") return phantomColor;
        if (flag == "fdi_diverter") return misrouteColor;
        if (flag == "spoof_rfid") return spoofColor;
        return normalColor;
    }

    private void ApplyColor(Color c)
    {
        if (renderers == null || renderers.Length == 0) return;

        foreach (var r in renderers)
        {
            if (!r) continue;

            var mat = r.sharedMaterial;
            if (mat == null) continue;

            
            _mpb.Clear();

           
            if (mat.HasProperty(BaseColorId)) _mpb.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId)) _mpb.SetColor(ColorId, c);

            
            if (mat.HasProperty(EmissionId))
            {
                
                mat.EnableKeyword("_EMISSION");
                _mpb.SetColor(EmissionId, c * 2.0f);
            }

            r.SetPropertyBlock(_mpb);
        }
    }

    public void SetTarget(Vector3 worldPos)
    {
        _target = worldPos;
        _hasTarget = true;
    }
}
