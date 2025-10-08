using UnityEngine;

public class AttackLockSMB : StateMachineBehaviour
{
    [SerializeField] private string lockParam = "AttackLocked";

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.SetBool(lockParam, true);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.SetBool(lockParam, false);
}
