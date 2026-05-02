using System.Collections.Generic;
using UnityEngine;

public class AnimationWatchSceneManager : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private List<Animator> characterAnimators = new List<Animator>();

    private void Awake()
    {
        if (characterAnimators == null || characterAnimators.Count == 0)
        {
            characterAnimators = new List<Animator>();
            foreach (var anim in FindObjectsOfType<Animator>(true))
            {
                if (anim.runtimeAnimatorController != null)
                    characterAnimators.Add(anim);
            }
        }
    }

    public void PlayAnimation(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        // The controller's Walk state loops while IsMoving > 0 and exits to
        // Idle otherwise. Set the bool so Walk loops cleanly and Idle holds.
        float isMoving = stateName == "Walk" ? 1f : 0f;
        foreach (var anim in characterAnimators)
        {
            if (anim == null) continue;
            anim.SetFloat("IsMoving", isMoving);
            anim.Play(stateName, 0, 0f);
        }
    }

    public void PlayIdle()  { PlayAnimation("Idle"); }
    public void PlayWalk()  { PlayAnimation("Walk"); }
    public void PlayHit()   { PlayAnimation("Hit"); }
    public void PlayDie()   { PlayAnimation("Die"); }
    public void PlaySteal() { PlayAnimation("Steal"); }
    public void PlayBirth() { PlayAnimation("Birth"); }
}
