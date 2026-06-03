[System.Serializable]
public class CleanTableRequest : TaskRequest
{
    private TableSet table;
    public TableSet Table => table;

    public CleanTableRequest(string guid, TableSet table)
    {
        this.guid = guid;
        this.table = table;
        this.priority = 50;
    }
}
