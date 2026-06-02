public abstract class TaskRequest
{
    public TaskRequester Sender;

    protected string guid;
    public string Guid => guid;

    protected int priority;
    public int Priority => priority;

    public TaskRequest() { }
    public TaskRequest(TaskRequester sender) => this.Sender = sender;
}
