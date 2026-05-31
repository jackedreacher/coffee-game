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

        transform.forward = mainCamera.forward;
    }
}
