using UnityEngine;

public class CustomerAnimator : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject plateau;

    [Header(" Settings ")]
    private bool isSitting;
    private Vector3 lastVelocity;

    private void Update()
    {
        if (!isSitting)
            HandleAnimations();
    }

    public void ManageAnimations(Vector3 velocity)
    {
        lastVelocity = velocity;
    }

    public void StartWalking()
    {
        isSitting = false;
    }

    public void Stop()
    {
        lastVelocity = Vector3.zero;
    }

    private void HandleAnimations()
    {
        if (lastVelocity.magnitude > 0)
        {
            animator.SetFloat("moveSpeed", lastVelocity.magnitude);
            PlayWalkAnimation();

            animator.transform.forward = Vector3.Lerp(
                animator.transform.forward,
                lastVelocity.normalized,
                Time.deltaTime * 60 * .2f);
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
