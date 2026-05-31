using UnityEngine;

public class CustomerAnimator : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject plateau;

    public void ManageAnimations(Vector3 moveVector, float moveSpeed)
    {
        if (moveVector.magnitude > 0)
        {
            animator.SetFloat("moveSpeed", moveSpeed / 1.5f);
            PlayWalkAnimation();

            animator.transform.forward = moveVector.normalized;
        }
        else
        {
            PlayIdleAnimation();
        }
    }

    private void PlayWalkAnimation()
    {
        if (plateau == null)
            animator.Play("Walk");
        else
        {
            if (plateau.gameObject.activeInHierarchy)
                animator.Play("WalkWithPlateau");
            else
                animator.Play("Walk");
        }
    }

    private void PlayIdleAnimation()
    {
        if (plateau == null)
            animator.Play("Idle");
        else
        {
            if (plateau.gameObject.activeInHierarchy)
                animator.Play("IdleWithPlateau");
            else
                animator.Play("Idle");
        }
    }
}
