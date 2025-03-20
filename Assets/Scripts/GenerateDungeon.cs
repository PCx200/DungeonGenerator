using NUnit.Framework;
using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using NaughtyAttributes;
using System.Linq;
using UnityEditor.Rendering;

public class GenerateDungeon : MonoBehaviour
{
    [SerializeField] RectInt dungeon = new RectInt(0, 0, 0, 0);
    public enum MapSize { Small, Medium, Large }

    public MapSize map;

    [SerializeField] int seed;
    [SerializeField] bool useRandomSeed;

    // variables for room modification
    [SerializeField] int minRoomSize;
    [SerializeField] float splitPercent;
    [SerializeField] bool verticalSplit;

    [SerializeField] int roomHeight;

    // needed for creating wall between intersecting rooms
    [SerializeField] int roomOverlap;

    // what percent of the smallest rooms you want to remove after creating the dungeon
    [SerializeField] int removePercentage;

    [SerializeField] List<RectInt> dungeonRooms;

    [SerializeField] List<RectInt> doors;

    // graph to represent the connection between the rooms
    [SerializeField] Graph<RectInt> graph = new Graph<RectInt>();



    void Start()
    {
        GenerateSeed();
        dungeonRooms = new List<RectInt>();
        ChoseMap();
        dungeonRooms.Add(dungeon);
        AlgorithmsUtils.DebugRectInt(dungeon, Color.blue, 100, true, roomHeight);
        StartCoroutine(RecursiveSplit());
    }

    void GenerateSeed()
    {
        if (!useRandomSeed)
        {
            Random.InitState(seed);
            Debug.Log(seed);
        }
        else
        {
           int randomSeed = Random.Range(1, 100000);
            Random.InitState(randomSeed);

            Debug.Log(randomSeed);
        }
    }

    #region Split
    (RectInt, RectInt) Split(RectInt pRoom)
    {
        RectInt room1 = pRoom;
        RectInt room2 = pRoom;

        verticalSplit = Random.value > 0.5f;
        splitPercent = Mathf.Round(Random.Range(0.3f, 0.7f) * 10f) / 10f;

        if (verticalSplit)
        {
            if (pRoom.width < minRoomSize * 2)
            {
                return (pRoom, pRoom);
            }
            int splitPoint = Mathf.Max(minRoomSize, (int)(pRoom.width * splitPercent));

            room1.width = splitPoint + (roomOverlap / 2);
            room2.width = (pRoom.width - splitPoint) + (roomOverlap / 2) + 1;

            room2.x = pRoom.x + splitPoint - roomOverlap;
        }
        else
        {
            if (pRoom.height < minRoomSize * 2)
            {
                return (pRoom, pRoom);
            }

            int splitPoint = Mathf.Max(minRoomSize, (int)(pRoom.height * splitPercent));

            room1.height = splitPoint + (roomOverlap / 2);
            room2.height = (pRoom.height - splitPoint) + (roomOverlap / 2) + 1;

            room2.y = pRoom.y + splitPoint - roomOverlap;
        }


        int index = dungeonRooms.FindIndex(room => room.Equals(pRoom));
        if (index != -1)
        {
            dungeonRooms.RemoveAt(index);
        }

        dungeonRooms.Add(room1);
        dungeonRooms.Add(room2);

        AlgorithmsUtils.DebugRectInt(room1, Color.yellow, 15, true, roomHeight);
        AlgorithmsUtils.DebugRectInt(room2, Color.yellow, 15, true, roomHeight);
        return (room1, room2);
    }

    [Button()]
    IEnumerator RecursiveSplit()
    {
        bool hasSplit = false;
        List<RectInt> currentRooms = new List<RectInt>(dungeonRooms);

        foreach (var room in currentRooms)
        {
            if (room.width > minRoomSize * 2 || room.height > minRoomSize * 2)
            {
                (RectInt room1, RectInt room2) = Split(room);
                hasSplit = true;

                yield return new WaitForSeconds(0.05f); 
            }
        }

        if (hasSplit)
        {
            yield return StartCoroutine(RecursiveSplit());
        }
        else
        {
            if(removePercentage != 0)
            { 
                yield return StartCoroutine(RemoveRooms());
            }

            yield return StartCoroutine(PutDoors());

            for (int i = 0; i < dungeonRooms.Count; i++)
            {
                RectInt roomToDraw = dungeonRooms[i];
                DebugDrawingBatcher.BatchCall(() => AlgorithmsUtils.DebugRectInt(roomToDraw, Color.white, 1, true, roomHeight));
            }
            yield return StartCoroutine(CreateGraph());
        }
    }
    #endregion

    #region Rooms Removal
    IEnumerator RemoveRooms()
    {
        int roomsToRemove = Mathf.FloorToInt(dungeonRooms.Count * removePercentage / 100);

        dungeonRooms.Sort((a, b) => (a.width * a.height).CompareTo(b.width * b.height));

        for (int i = 0; i < roomsToRemove;)
        {
            RectInt roomToRemove = dungeonRooms[0];
            dungeonRooms.RemoveAt(0);

            if (!IsDungeonConnected(dungeonRooms))
            {
                dungeonRooms.Add(roomToRemove);
                yield break;
            }
            else
            {
                i++; // increase only when a room is removed
            }

            AlgorithmsUtils.DebugRectInt(roomToRemove, Color.red, 10, true, roomHeight);
            yield return new WaitForSeconds(0.2f);
        }
    }
    #endregion

    #region Check Conectivity
    //dfs checking if every room is connected
    bool IsDungeonConnected(List<RectInt> rooms)
    { 
        HashSet<RectInt> visited = new HashSet<RectInt>();
        Stack<RectInt> stack = new Stack<RectInt>();

        stack.Push(rooms[0]);

        while (stack.Count > 0)
        {
            RectInt current = stack.Pop();  
            if (!visited.Add(current)) continue;

            foreach (var neighbour in rooms)
            {
                if (!visited.Contains(neighbour) && AlgorithmsUtils.Intersects(current, neighbour))
                { 
                    stack.Push(neighbour);
                }
            }
        }

        return visited.Count == rooms.Count;        
    }

    bool ShareDoor(RectInt room, RectInt neighbour, out RectInt sharedDoor)
    {
        foreach (var door in doors)
        {
            if (AlgorithmsUtils.Intersects(room, door) && AlgorithmsUtils.Intersects(neighbour, door))
            {
                sharedDoor = door;  
                return true;
            }
        }
        sharedDoor = default(RectInt);
        return false;

    }
    #endregion

    #region Create Doors
    IEnumerator PutDoors()
    {
        yield return new WaitForSeconds(1);

        doors.Clear();

        List<RectInt> intersectingRooms = new List<RectInt>(dungeonRooms);

        for (int i = 0; i < intersectingRooms.Count; i++)
        {
            for (int j = i + 1; j < intersectingRooms.Count; j++)
            {
                if (AlgorithmsUtils.Intersects(intersectingRooms[i], intersectingRooms[j]))
                {
                    RectInt room1 = intersectingRooms[i];
                    RectInt room2 = intersectingRooms[j];

                    int xMin = Mathf.Max(room1.xMin, room2.xMin);
                    int xMax = Mathf.Min(room1.xMax, room2.xMax);
                    int yMin = Mathf.Max(room1.yMin, room2.yMin);
                    int yMax = Mathf.Min(room1.yMax, room2.yMax);

                    Vector2Int doorPosition;

                    int randomOffset = Random.Range(-1, 2);

                    if (xMax - xMin > 5) // vertical wall
                    {
                        int doorX = (xMin + xMax) / 2 + randomOffset;
                        int doorY = yMin;
                        doorPosition = new Vector2Int(doorX, doorY);
                    }
                    else if (yMax - yMin > 5) // horizontal wall
                    {
                        int doorX = xMin;
                        int doorY = (yMin + yMax) / 2 + randomOffset;
                        doorPosition = new Vector2Int(doorX, doorY);
                    }
                    else
                    {
                        continue; // no wall found
                    }

                        
                    RectInt door = new RectInt(doorPosition.x, doorPosition.y, 1, 1);
                    doors.Add(door);
                    yield return new WaitForSeconds(0.01f);
                    AlgorithmsUtils.DebugRectInt(door, Color.red, 100, true, roomHeight);

                }
            }
        }
    }
    #endregion

    IEnumerator CreateGraph()
    {
        RectInt topRightRoom = GetTopRightRoom(dungeonRooms);
        graph.AddNode(topRightRoom);

        List<RectInt> graphRooms = new List<RectInt>();

        foreach (RectInt room in dungeonRooms)
        { 
            graphRooms.Add(room);
            graph.AddNode(room);
        }

        for (int i = 0; i < graphRooms.Count; i++)
        {
            for (int j = i + 1; j < graphRooms.Count; j++)
            {
                RectInt sharedDoor;
                if (AlgorithmsUtils.Intersects(graphRooms[i], graphRooms[j]) && ShareDoor(graphRooms[i], graphRooms[j], out sharedDoor))
                {
                    graph.AddEdge(graphRooms[i], sharedDoor);
                    graph.AddEdge(sharedDoor, graphRooms[j]);
                }

            }
        }

        yield return StartCoroutine(graph.DFS(topRightRoom));

        foreach (var node in graph.GetNodes())
        {
            Vector3 pos1 = new Vector3(node.x + node.width / 2, 0, node.y + node.height / 2);
            DebugExtension.DebugWireSphere(pos1, 1.5f, 100, false);

            foreach (var neighbour in graph.GetNeighbors(node))
            {
                Vector3 pos2 = new Vector3(neighbour.x + neighbour.width / 2, 0, neighbour.y + neighbour.height / 2);

                yield return new WaitForSeconds(0.35f);
                DebugExtension.DebugWireSphere(pos2, 1.5f, 100, false);

                RectInt sharedDoor;
                if (ShareDoor(node, neighbour, out sharedDoor))
                {
                    Vector3 doorPos = new Vector3(sharedDoor.x + sharedDoor.width / 2, 0, sharedDoor.y + sharedDoor.height / 2);
                    Debug.DrawLine(pos1, doorPos, Color.cyan, 100);
                    Debug.DrawLine(doorPos, pos2, Color.cyan, 100);
                }
                
            }
        }
    }

    RectInt GetTopRightRoom(List<RectInt> rooms)
    {
        var topRooms = rooms.Where(r => r.yMax == rooms.Max(r => r.yMax));

        return topRooms.OrderByDescending(r => r.xMax).FirstOrDefault();
    }

    #region Map Settings
    void ChoseMap()
    {
        switch (map)
        {
            case MapSize.Small:
                dungeon = new RectInt(0, 0, 100, 100);
                minRoomSize = 10;
            break;

            case MapSize.Medium:
                dungeon = new RectInt(0, 0, 150, 150);
                minRoomSize = 15;
            break;

            case MapSize.Large:
                dungeon = new RectInt(0, 0, 200, 200);
                minRoomSize = 20;
            break;

            default:
                dungeon = new RectInt();
            break;
        }
    }
    #endregion

}
