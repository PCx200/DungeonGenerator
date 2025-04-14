using NaughtyAttributes;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

public class TileMapGenerator : MonoBehaviour
{
    [SerializeField]
    private UnityEvent onGenerateTileMap;

    [SerializeField]
    GenerateDungeon dungeonGenerator;

    private int[,] _tileMap;


    List<Cell> cells = new List<Cell>();

    [SerializeField]
    private GameObject[] tilePrefabs;



    private void Start()
    {
        dungeonGenerator = GetComponent<GenerateDungeon>();
    }

    [Button]
    public void GenerateTileMap()
    {
        int[,] tileMap = new int[dungeonGenerator.dungeon.height, dungeonGenerator.dungeon.width];
        int rows = tileMap.GetLength(0);
        int cols = tileMap.GetLength(1);

        //Fill the map with empty spaces
        foreach (RectInt room in dungeonGenerator.dungeonRooms)
        {
            AlgorithmsUtils.FillRectangleOutline(tileMap, room, 1);
        }
        foreach (RectInt door in dungeonGenerator.doors)
        {
            AlgorithmsUtils.FillRectangleOutline(tileMap, door, 0);
        }


        _tileMap = tileMap;

        PutAssets();
        onGenerateTileMap.Invoke();
    }

    public void PutAssets()
    {
        for (int y = 0; y < _tileMap.GetLength(0) - 1; y++)
        {
            for (int x = 0; x < _tileMap.GetLength(1) - 1; x++)
            {
                int topLeft = _tileMap[y, x];
                int topRight = _tileMap[y, x + 1];
                int botLeft = _tileMap[y + 1, x];
                int botRight = _tileMap[y + 1, x + 1];

                Cell tempCell = new Cell();
                tempCell.cell = (botRight, topRight, topLeft, botLeft);
                tempCell.value = tempCell.GetCellValue();

                cells.Add(tempCell);

                // Instantiate the prefab if one exists
                int value = tempCell.value;
                if (value >= 0 && value < tilePrefabs.Length && tilePrefabs[value] != null)
                {

                    Vector3 position = new Vector3(x + 1, 0, y + 1);
                    Instantiate(tilePrefabs[value], position, Quaternion.identity, transform);
                }
            }
        }
    }
    public string ToString(bool flip)
    {
        if (_tileMap == null) return "Tile map not generated yet.";

        int rows = _tileMap.GetLength(0);
        int cols = _tileMap.GetLength(1);

        var sb = new StringBuilder();

        int start = flip ? rows - 1 : 0;
        int end = flip ? -1 : rows;
        int step = flip ? -1 : 1;

        for (int i = start; i != end; i += step)
        {
            for (int j = 0; j < cols; j++)
            {
                sb.Append((_tileMap[i, j] == 0 ? '□' : '■')); //Replaces 1 with '#' making it easier to visualize
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public int[,] GetTileMap()
    {
        return _tileMap.Clone() as int[,];
    }

    [Button]
    public void PrintTileMap()
    {
        Debug.Log(ToString(true));
    }


}
