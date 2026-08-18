using System;
using UnityEngine;

// One row of an order: this food, this many.
//
// An order used to be a single food and a single count, which made "two burgers
// and a fries" unsayable. A row makes it sayable without making the simple case
// harder -- one row is exactly the old order, and everything that reads an
// order reads rows either way
[Serializable]
public struct OrderLine
{
    public SpawnableFood food;
    public int count;

    public OrderLine(SpawnableFood food, int count)
    {
        this.food = food;
        this.count = Mathf.Max(1, count);
    }

    public bool IsValid => food != null && count > 0;
}
