using UnityEngine;

public class HRManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private CanvasGroup cg;

    public bool IsPanelVisible => cg.blocksRaycasts;

    public void Display()
    {
        cg.Show();
    }

    public void Hide()
    {
        cg.Hide();
    }
}
