using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [Header(" Elements ")]
    private Transform mainCamera;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (mainCamera == null)
            mainCamera = Camera.main.transform;

        if (mainCamera == null)
            return;

        // The camera's whole orientation, not only where it points.
        //
        // Setting forward alone leaves the roll to be guessed from world up, so
        // a camera with any twist in it lands the card at an angle to the screen
        transform.rotation = mainCamera.rotation;
    }
}
