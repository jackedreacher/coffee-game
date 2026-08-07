using UnityEngine;

public class DeskStation : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private HRManager hrManager;

    [Header(" Settings ")]
    private bool hasEntered;

    private void OnTriggerStay(Collider other)
    {
        if (hrManager.IsPanelVisible)
            return;

        if (hasEntered)
            return;

        if (!other.TryGetComponent(out PlayerController playerController))
            return;

        if (playerController.IsMoving())
            return;

        hasEntered = true;
        hrManager.Display();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController playerController))
            return;

        hasEntered = false;
    }
}
