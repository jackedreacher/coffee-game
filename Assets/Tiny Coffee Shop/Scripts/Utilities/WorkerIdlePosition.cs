using UnityEngine;

[RequireComponent(typeof(TaskRequester))]
[RequireComponent(typeof(GuidGenerator))]
public class WorkerIdlePosition : MonoBehaviour
{
    [Header(" Components ")]
    private TaskRequester taskRequester;
    private GuidGenerator guidGenerator;

    [Header(" Settings ")]
    private float timer;
    private const float requestDelay = 2f;

    private void Awake()
    {
        taskRequester = GetComponent<TaskRequester>();
        guidGenerator = GetComponent<GuidGenerator>();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer < requestDelay)
            return;

        timer = 0;
        taskRequester.CreateTaskRequest(new IdleRequest(guidGenerator.GUID, transform.position));
    }
}
