using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MobileJoystick : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private RectTransform joystickOutline;
    [SerializeField] private RectTransform joystickKnob;

    [Header(" Settings ")]
    [SerializeField] private float moveFactor;
    private Vector3 clickedPosition;
    private Vector3 move;
    private bool canControl;

    // Start is called before the first frame update
    void Start()
    {
        HideJoystick();
    }

    // Update is called once per frame
    void Update()
    {
        if(canControl)
            ControlJoystick();
    }

    public void ClickedOnJoystickZoneCallback()
    {
        clickedPosition = GetPointerPosition();
        joystickOutline.position = clickedPosition;

        ShowJoystick();
    }

    private void ShowJoystick()
    {
        joystickOutline.gameObject.SetActive(true);
        canControl = true;
    }

    private void HideJoystick()
    {
        joystickOutline.gameObject.SetActive(false);
        canControl = false;

        move = Vector3.zero;
    }

    private void ControlJoystick()
    {
        Vector3 currentPosition = GetPointerPosition();
        Vector3 direction = currentPosition - clickedPosition;

        float canvasScale = GetComponentInParent<Canvas>().GetComponent<RectTransform>().localScale.x;

        float moveMagnitude = direction.magnitude;

        float absoluteWidth = joystickOutline.rect.width / 2;
        float realWidth = absoluteWidth * canvasScale;

        moveMagnitude = Mathf.Min(moveMagnitude, realWidth);

        move = direction.normalized;
        Vector3 knobMove = move * moveMagnitude;

        Vector3 targetPosition = clickedPosition + knobMove;

        joystickKnob.position = targetPosition;

        if (IsPointerReleased())
            HideJoystick();
    }

    private Vector3 GetPointerPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.value;        

        if (Mouse.current != null)
            return Mouse.current.position.value;

        return Vector3.zero;
    }

    private bool IsPointerReleased()
    {
        if (Touchscreen.current != null)
            return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        
        if (Mouse.current != null)
            return Mouse.current.leftButton.wasReleasedThisFrame;
        
        return true;
    }

    public Vector3 GetMoveVector() => move;
}
