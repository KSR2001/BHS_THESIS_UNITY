using System.Collections;
using UnityEngine;

public class Highlighter : MonoBehaviour
{
    [Header("Renderers")]
    public Renderer[] renderers;

    [Header("Normal Flash")]
    public float normalFlashSeconds = 0.35f;
    public float normalFlashFreq = 10f;
    public Color normalFlashColor = Color.magenta;

    [Header("Anomaly Flash")]
    public float anomalyFlashSeconds = 1.2f;
    public float anomalyFlashFreq = 12f;
    public Color anomalyFlashColor = Color.red;

    private Coroutine _co;

    void Reset()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    public void FlashNormal()
    {
        StartFlash(normalFlashSeconds, normalFlashFreq, normalFlashColor);
    }

    public void FlashAnomaly()
    {
        StartFlash(anomalyFlashSeconds, anomalyFlashFreq, anomalyFlashColor);
    }

    private void StartFlash(float seconds, float freq, Color color)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFlash(seconds, freq, color));
    }

    IEnumerator CoFlash(float flashSeconds, float flashFreq, Color flashColor)
    {
        float t = 0f;
        while (t < flashSeconds)
        {
            float s = (Mathf.Sin(Time.time * flashFreq) * 0.5f + 0.5f) * 0.7f + 0.3f;
            foreach (var r in renderers)
            {
                if (!r) continue;
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", flashColor * s);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        mat.color = Color.Lerp(Color.white, flashColor, s);
                    }
                }
            }
            t += Time.deltaTime;
            yield return null;
        }

        // reset
        foreach (var r in renderers)
        {
            if (!r) continue;
            foreach (var mat in r.materials)
            {
                if (mat == null) continue;

                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", Color.black);
                else if (mat.HasProperty("_Color"))
                    mat.color = Color.white;
            }
        }
        _co = null;
    }
}
