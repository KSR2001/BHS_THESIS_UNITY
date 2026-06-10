using UnityEngine;

public class BHSPersonHandler : MonoBehaviour
{
    public Animator animator;
    public string triggerName = "HandleBag";

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    public void OnBagEvent()
    {
        if (animator && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
}
