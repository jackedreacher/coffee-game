using System;
using System.Collections.Generic;
using UnityEngine;

public class TableManager : MonoBehaviour
{
    [Header(" Elements ")]
    private TableSet[] tables;

    [Header(" Components ")]
    [SerializeField] private TaskRequester taskRequester;

    [Header(" Settings ")]
    private List<TableSet> dirtyTables = new List<TableSet>();

    [Header(" Actions ")]
    public static Action<TableSet, HoldDishAbility> TableCleaned;

    private void Awake()
    {
        tables = GetComponentsInChildren<TableSet>();
    }

    public void PushDirtyTable(TableSet table)
    {
        dirtyTables.Add(table);
        taskRequester.CreateTaskRequest(new CleanTableRequest(table.GUID, table));
    }

    public void RemoveDirtyTable(TableSet table, HoldDishAbility holdDishAbility)
    {
        dirtyTables.Remove(table);
        taskRequester.ClearRequest(new CleanTableRequest(table.GUID, table));
        TableCleaned?.Invoke(table, holdDishAbility);
    }

    public bool IsAnyTableAvailable()
    {
        return GetFirstCleanEmptyTable() != null;
    }

    public TableSet GetFirstCleanEmptyTable()
    {
        for (int i = 0; i < tables.Length; i++)
        {
            if (tables[i].IsFull)
                continue;

            if (tables[i].IsDirty)
                continue;

            return tables[i];
        }

        return null;
    }

    public void HandleCustomerServed(Customer customer)
    {
        TableSet table = GetFirstCleanEmptyTable();

        if (table == null)
        {
            Debug.LogError("TableManager: No clean table found. This should not happen.");
            return;
        }

        table.AcceptCustomer(customer, this);
    }
}
