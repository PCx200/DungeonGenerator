using NUnit.Framework;
using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using NaughtyAttributes;
using System.Linq;
using UnityEditor.Rendering;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UI;
using System.Xml.Linq;
using System.Threading;
using Unity.Properties;

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


    //Dictionary<RectInt, RectInt> graphToDoors = new Dictionary<RectInt, RectInt>();
    [SerializeField] List<RectInt> dungeonRooms;

    [SerializeField] List<RectInt> doors;

    // graph to represent the connection between the rooms
    [SerializeField] Graph<RectInt> graph = new Graph<RectInt>();

    System.Random rand;


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
            rand = new System.Random(seed);
            Debug.Log(seed);
        }
        else
        {
           int randomSeed = Random.Range(1, 100000);
            rand = new System.Random();
            Debug.Log(randomSeed);
        }
    }

    #region Split
    (RectInt, RectInt) Split(RectInt pRoom)
    {
        RectInt room1 = pRoom;
        RectInt room2 = pRoom;


        verticalSplit = rand.Next(0, 2) >= 1;
        //splitPercent = Mathf.Round(Random.Range(0.3f, 0.7f) * 10f) / 10f;

        splitPercent = rand.Next(3, 8) / 10f;


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
            yield return StartCoroutine(PutDoors());

            if (removePercentage != 0)
            { 
                yield return StartCoroutine(RemoveRoomsAndDoors());
                foreach (var door in doors)
                {
                    AlgorithmsUtils.DebugRectInt(door, Color.green, 100, true, roomHeight);
                }
            }

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
    IEnumerator RemoveRoomsAndDoors()
    {
        int roomsToRemove = Mathf.FloorToInt(dungeonRooms.Count * removePercentage / 100);

        dungeonRooms.Sort((a, b) => (a.width * a.height).CompareTo(b.width * b.height));

        List<RectInt> removedDoors = new List<RectInt>();

        for (int i = 0; i < roomsToRemove;)
        {
            // removes the smallest room
            RectInt roomToRemove = dungeonRooms[0];
            dungeonRooms.RemoveAt(0);

            List<RectInt> intersectingDoors = doors.Where(door => AlgorithmsUtils.Intersects(roomToRemove, door)).ToList();
            //removes all doors attached to that room
            foreach (RectInt door in intersectingDoors)
            {
                removedDoors.Add(door);
                doors.Remove(door);
            }
            // checks if the dungeon is split into two
            if (!IsDungeonConnected(dungeonRooms))
            {
                // adds the last room that has been removed
                dungeonRooms.Add(roomToRemove);

                // adds the removed doors that are still connected to a room
                foreach (RectInt removedDoor in removedDoors.Where(door => dungeonRooms.Any(room => AlgorithmsUtils.Intersects(room, door))))
                {
                    doors.Add(removedDoor);
                }

                // if a door intersect only once remove it
                doors.RemoveAll(door => dungeonRooms.Count(room => AlgorithmsUtils.Intersects(room, door)) <= 1);

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
        RectInt sharedDoor;
        stack.Push(rooms[0]);

        while (stack.Count > 0)
        {
            RectInt current = stack.Pop();  
            if (!visited.Add(current)) continue;

            foreach (var neighbour in rooms)
            {
                if (!visited.Contains(neighbour) && AlgorithmsUtils.Intersects(current, neighbour) && ShareDoor(current, neighbour, out sharedDoor))
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
                    AlgorithmsUtils.DebugRectInt(door, Color.red, 10, true, roomHeight);

                }
            }
        }
    }
    #endregion

    IEnumerator CreateGraph()
    {
        RectInt topRightRoom = GetTopRightRoom(dungeonRooms);
        graph.AddNode(topRightRoom);

        foreach (RectInt room in dungeonRooms)
        { 
            graph.AddNode(room);
        }
        for (int i = 0; i < graph.GetNodeCount(); i++)
        {
            for (int j = i + 1; j < graph.GetNodeCount(); j++)
            {
                RectInt sharedDoor;
                if (AlgorithmsUtils.Intersects(graph.GetNode(i), graph.GetNode(j)) && ShareDoor(graph.GetNode(i), graph.GetNode(j), out sharedDoor))
                {
                    Vector3 pos1 = new Vector3(graph.GetNode(i).x + graph.GetNode(i).width / 2, 0, graph.GetNode(i).y + graph.GetNode(i).height / 2);
                    Vector3 pos2 = new Vector3(graph.GetNode(j).x + graph.GetNode(j).width / 2, 0, graph.GetNode(j).y + graph.GetNode(j).height / 2);
                    Vector3 doorPos = new Vector3(sharedDoor.x + sharedDoor.width, 0, sharedDoor.y + sharedDoor.height);

                    graph.AddEdge(graph.GetNode(i), graph.GetNode(j));

                    DebugExtension.DebugWireSphere(pos1, 1f, 100);
                    Debug.DrawLine(pos1, doorPos, Color.cyan, 100);

                    DebugExtension.DebugWireSphere(pos2, 1f, 100);
                    Debug.DrawLine(doorPos, pos2,Color.cyan, 100);

                    yield return new WaitForSeconds(0.025f);
                }
            }
        }
        yield return StartCoroutine(DF(topRightRoom));
    }
    IEnumerator DF(RectInt startnode)
    {
        HashSet<RectInt> visitedNodes = new HashSet<RectInt>();
        Stack<RectInt> stack = new Stack<RectInt>();

        stack.Push(startnode);

        while (stack.Count > 0)
        {
            RectInt node = stack.Pop();

            Vector3 pos = new Vector3(node.x + node.width / 2, 0, node.y + node.height / 2);
            DebugExtension.DebugWireSphere(pos, Color.green, 1f, 100);

            if (!visitedNodes.Contains(node))
            {
                visitedNodes.Add(node);

                yield return new WaitForSeconds(0.1f);

                if (dungeonRooms.Count == visitedNodes.Count) 
                {
                    Debug.Log("DFS completed, all nodes visited.");
                    foreach (RectInt nod in visitedNodes)
                    {
                        Debug.Log(nod);
                    }
                    yield break;
                }

                foreach (RectInt neighbour in graph.GetNeighbors(node))
                {                       
                    if (!visitedNodes.Contains(neighbour))
                    {                      
                        stack.Push(neighbour);                       
                    }
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
                minRoomSize = 12;
            break;

            case MapSize.Medium:
                dungeon = new RectInt(0, 0, 150, 150);
                minRoomSize = 18;
            break;

            case MapSize.Large:
                dungeon = new RectInt(0, 0, 250, 250);
                minRoomSize = 24;
            break;

            default:
                dungeon = new RectInt();
            break;
        }
    }
    #endregion

}
