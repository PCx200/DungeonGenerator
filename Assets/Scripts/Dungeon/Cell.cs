using UnityEngine;

public class Cell
{
    public (int, int, int, int) cell;

    public int value;

    public int GetCellValue()
    {
        return value = cell.Item1 + cell.Item2 * 2 + cell.Item3 * 4 + cell.Item4 * 8;
    }
}
