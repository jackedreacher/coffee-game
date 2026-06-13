using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PayAbility : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private GameObject cashPrefab;

    [Header(" Components ")]
    private CharacterController characterController;

    public bool IsMoving => characterController.velocity.magnitude > 0.1f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public void Pay(int cashPerFrame, LockedElement lockedElement)
    {
        int toPay = Mathf.Min(cashPerFrame, CurrencyManager.instance.Currency);

        if (toPay <= 0)
            return;

        CurrencyManager.instance.AddCurrency(-toPay);
        lockedElement.CollectCash(toPay);
        AnimateCashTo(lockedElement.transform);
    }

    private void AnimateCashTo(Transform target)
    {
        GameObject cashObject = Instantiate(cashPrefab, transform.position, Quaternion.identity);

        if (cashObject.TryGetComponent(out Collider col))
            col.enabled = false;

        ArcAnimator.Animate(cashObject.transform, target, 0.2f, 0f, 2f,
            (go) => Destroy(go));
    }
}
