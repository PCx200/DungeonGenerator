using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

public class TileMapGenerator : MonoBehaviour
{
    [SerializeField]
    private UnityEvent onGenerateTileMap;
    [SerializeField] UnityEvent onPlacedAssets;

    private int[,] _tileMap;


    List<Cell> cells = new List<Cell>();

    [SerializeField]
    private GameObject[] tilePrefabs;

    bool isFloorBuilt;

    private void Start()
    {
        
    }
    void ClearData()
    {
        // Clear previous tile objects
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        _tileMap = null;
        cells.Clear();
    }

    [Button]
    /// <summary>
    /// Generates a tile map representation of the dungeon layout.
    /// Rooms are outlined with 1s, doors with 0s, and stored in _tileMap.
    /// </summary>
    public void GenerateTileMap()
    {
        ClearData();

        int[,] tileMap = new int[GenerateDungeon.Instance.dungeon.height, GenerateDungeon.Instance.dungeon.width];
        int rows = tileMap.GetLength(0);
        int cols = tileMap.GetLength(1);

        //Fill the map with empty spaces
        foreach (RectInt room in GenerateDungeon.Instance.dungeonRooms)
        {
            AlgorithmsUtils.FillRectangleOutline(tileMap, room, 1);
        }
        foreach (RectInt door in GenerateDungeon.Instance.doors)
        {
            AlgorithmsUtils.FillRectangleOutline(tileMap, door, 0);
        }


        _tileMap = tileMap;

        onGenerateTileMap.Invoke();
    }

    public void SpawnAssets()
    {
        if (!GenerateDungeon.Instance.useSimpleAssets)
        {
            StartCoroutine(BuildWalls());
            StartCoroutine(FloorFloodFill(GenerateDungeon.Instance.GetStartNode()));
        }
        else
        {
            GenerateDungeon.Instance.SpawnSimpleAssets();
            onPlacedAssets.Invoke();
        }
    }

    /// <summary>
    /// Builds wall tiles based on marching squares logic from the _tileMap.
    /// </summary>
    public IEnumerator BuildWalls()
    {
        int width = _tileMap.GetLength(1);
        int height = _tileMap.GetLength(0);

        int counter = 0;

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int topLeft = _tileMap[y, x];
                int topRight = _tileMap[y, x + 1];
                int botLeft = _tileMap[y + 1, x];
                int botRight = _tileMap[y + 1, x + 1];

                Cell cell = new Cell
                {
                    cell = (botRight, topRight, topLeft, botLeft)
                };
                cell.value = cell.GetCellValue(); 
                cells.Add(cell);

                int value = cell.value;

                if (value != 0 && value < tilePrefabs.Length && tilePrefabs[value] != null)
                {
                    Vector3 position = new Vector3(x + 0.5f, 0, y + 0.5f);
                    Instantiate(tilePrefabs[value], position, Quaternion.identity, transform);
                    counter++;  
                }

                if (!GenerateDungeon.Instance.createImmediately && counter >= 50)
                {
                    counter = 0;
                    yield return null;
                }   
            }
        }
    }

    /// <summary>
    /// Performs a flood fill from a start node across floor tiles in _tileMap.
    /// </summary>
    public IEnumerator FloorFloodFill(Node startNode)
    {
        int width = _tileMap.GetLength(1);
        int height = _tileMap.GetLength(0);
        HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();

        Vector2Int startPos = new Vector2Int(startNode.node.position.x + 2, startNode.node.position.y + 2);

        if (_tileMap[startPos.y, startPos.x] != 0)
        {
            Debug.LogWarning("Start position is not on a floor tile!");
            yield break;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startPos);
        visitedPositions.Add(startPos);

        Vector2Int[] directions = new Vector2Int[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
            //8-way
            //Vector2Int.up + Vector2Int.right,
            //Vector2Int.up + Vector2Int.left,
            //Vector2Int.down + Vector2Int.right,
            //Vector2Int.down + Vector2Int.left
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            visitedPositions.Add(current);

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (!visitedPositions.Contains(neighbor) && _tileMap[neighbor.y, neighbor.x] == 0)
                {
                    visitedPositions.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        int counter = 0;
        foreach (Vector2Int pos in visitedPositions) 
        {
            int x = pos.x;
            int y = pos.y;

            Vector3 position = new Vector3(x, 0, y);
            Instantiate(tilePrefabs[0], position, Quaternion.identity, transform);
            counter++;
            if (!GenerateDungeon.Instance.createImmediately && counter >= 50)
            {
                counter = 0;
                yield return null;
            }
        }
        onPlacedAssets.Invoke();
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
