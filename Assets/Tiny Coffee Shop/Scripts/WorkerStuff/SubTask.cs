public abstract class SubTask
{
    public bool IsComplete { get; protected set; }

    public abstract void Start(Worker worker);
    public abstract void Update(Worker worker);
}
