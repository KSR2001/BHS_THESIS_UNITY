using System.Collections;
using UnityEngine;

public class BHSRobotArm : MonoBehaviour
{
    [Header("Robot poses (local rotation only)")]
    public Transform idlePose;
    public Transform pickPose;
    public Transform placePose;

    [Header("Timings")]
    public float moveTime = 0.7f;
    public float pauseAtPick = 0.3f;
    public float pauseAtPlace = 0.3f;

    private bool _busy;

    public void HandleBagEvent()
    {
        if (_busy) return;
        Debug.Log($"[BHSRobotArm] {name} triggered");
        StartCoroutine(CoPickPlace());
    }

    IEnumerator CoPickPlace()
    {
        _busy = true;

        if (idlePose && pickPose)
            yield return RotateLocal(idlePose.localRotation, pickPose.localRotation, moveTime);

        yield return new WaitForSeconds(pauseAtPick);

        if (pickPose && placePose)
            yield return RotateLocal(pickPose.localRotation, placePose.localRotation, moveTime);

        yield return new WaitForSeconds(pauseAtPlace);

        if (placePose && idlePose)
            yield return RotateLocal(placePose.localRotation, idlePose.localRotation, moveTime);

        _busy = false;
    }

    IEnumerator RotateLocal(Quaternion from, Quaternion to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            transform.localRotation = Quaternion.Slerp(from, to, u);
            yield return null;
        }
        transform.localRotation = to;
    }
}
