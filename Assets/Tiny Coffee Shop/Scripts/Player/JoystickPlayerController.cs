using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class JoystickPlayerController : PlayerController
{
    [Header(" Elements ")]
    [SerializeField] private MobileJoystick joystick;
    private CharacterController characterController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();
        characterController = GetComponent<CharacterController>();
    }

    protected override void UpdateMovement()
    {
        Vector3 correctedJoystickVector = joystick.GetMoveVector();
        correctedJoystickVector.z = correctedJoystickVector.y;
        correctedJoystickVector.y = 0;

        // Take into account the isometric camera
        Vector3 isometricMoveVector = Quaternion.Euler(0, -45, 0) * correctedJoystickVector;

        playerAnimator.ManageAnimations(isometricMoveVector, moveSpeed);
        Vector3 moveVector = isometricMoveVector * moveSpeed * Time.deltaTime;
        characterController.Move(moveVector);
    }

    public override bool IsMoving()
    {
        return joystick.GetMoveVector().magnitude > 0;
    }

}
