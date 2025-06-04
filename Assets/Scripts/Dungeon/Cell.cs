using UnityEngine;

public class Cell
{
    public (int, int, int, int) cell;

    public int value;

    /// <summary>
    /// Calculates a unique integer value based on a 4-bit cell tuple.
    /// Uses bitwise weighting: Item1 + Item2 * 2 + Item3 * 4 + Item4 * 8.
    /// </summary>
    public int GetCellValue()
    {
        return value = cell.Item1 + cell.Item2 * 2 + cell.Item3 * 4 + cell.Item4 * 8;
    }
}
