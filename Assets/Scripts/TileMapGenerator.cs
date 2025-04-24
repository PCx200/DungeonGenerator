using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

public class TileMapGenerator : MonoBehaviour
{
    [SerializeField] bool createImmediately = false;

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

        StartCoroutine(FloorFloodFill(dungeonGenerator.GetStartNode()));
        StartCoroutine(BuildWalls());
        onGenerateTileMap.Invoke();
    }
    public IEnumerator BuildWalls()
    {
        int width = _tileMap.GetLength(1);
        int height = _tileMap.GetLength(0);

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
                }

                if (!createImmediately)
                {
                    yield return null;
                }   
            }
        }
    }
    public IEnumerator FloorFloodFill(Node startNode)
    {
        int width = _tileMap.GetLength(1);
        int height = _tileMap.GetLength(0);
        bool[,] visited = new bool[height, width];

        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();

        Vector2Int startPos = new Vector2Int(startNode.nodeLocation.position.x + 2, startNode.nodeLocation.position.y + 2);

        if (_tileMap[startPos.y, startPos.x] != 0)
        {
            Debug.LogWarning("Start position is not on a floor tile!");
            yield break;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startPos);
        visited[startPos.y, startPos.x] = true;

        Vector2Int[] directions = new Vector2Int[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up + Vector2Int.right,
            Vector2Int.up + Vector2Int.left,
            Vector2Int.down + Vector2Int.right,
            Vector2Int.down + Vector2Int.left
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            floorPositions.Add(current);

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (neighbor.x >= 0 && neighbor.x < width &&
                    neighbor.y >= 0 && neighbor.y < height &&
                    !visited[neighbor.y, neighbor.x] &&
                    _tileMap[neighbor.y, neighbor.x] == 0)
                {
                    visited[neighbor.y, neighbor.x] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }
        foreach (Vector2Int pos in floorPositions) 
        {
                int x = pos.x;
                int y = pos.y;

                
                if (!floorPositions.Contains(new Vector2Int(x, y)) &&
                    !floorPositions.Contains(new Vector2Int(x + 1, y)) &&
                    !floorPositions.Contains(new Vector2Int(x, y + 1)) &&
                    !floorPositions.Contains(new Vector2Int(x + 1, y + 1)))
                {
                    continue;
                }

                Vector3 position = new Vector3(x, 0, y);
                Instantiate(tilePrefabs[0], position, Quaternion.identity, transform);
            if (!createImmediately)
            {
                yield return null;
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
