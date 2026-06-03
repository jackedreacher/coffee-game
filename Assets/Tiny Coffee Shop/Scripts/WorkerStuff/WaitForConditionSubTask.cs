using System;

public class WaitForConditionSubTask : SubTask
{
    private Func<bool> condition;

    public WaitForConditionSubTask(Func<bool> condition)
    {
        this.condition = condition;
    }

    public override void Start(Worker worker)
    {
        worker.MarkAsBusy();
    }

    public override void Update(Worker worker)
    {
        if (condition())
            IsComplete = true;
    }
}
