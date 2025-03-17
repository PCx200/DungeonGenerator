using NUnit.Framework;
using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using NaughtyAttributes;

public class GenerateDungeon : MonoBehaviour
{
    [SerializeField] RectInt dungeon = new RectInt(0, 0, 0, 0);
    public enum MapSize { Small, Medium, Large }

    public MapSize map;

    // variables for room modification
    [SerializeField] int minRoomSize;
    [SerializeField] float splitPercent;
    [SerializeField] bool verticalSplit;

    [SerializeField] int roomCount;
    [SerializeField] int roomHeight;

    // needed for creating wall between intersecting rooms
    [SerializeField] int roomOverlap;

    // what percent of the smallest rooms you want to remove after creating the dungeon
    [SerializeField] int removePercentage;

    [SerializeField] List<RectInt> dungeonRooms;

    [SerializeField] List<RectInt> doors;

    // graph to represent the connection between the rooms
    [SerializeField] Dictionary<RectInt, List<RectInt>> dungeonGraph = new Dictionary<RectInt, List<RectInt>>();



    void Start()
    {
        dungeonRooms = new List<RectInt>();
        ChoseMap();
        dungeonRooms.Add(dungeon);
        roomCount = dungeonRooms.Count;
        AlgorithmsUtils.DebugRectInt(dungeon, Color.blue, 100, true, roomHeight);
        StartCoroutine(RecursiveSplit());
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

        roomCount = dungeonRooms.Count;

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
                if (!visited.Contains(neighbour) && AreRoomsConnected(current, neighbour))
                { 
                    stack.Push(neighbour);
                }
            }
        }

        return visited.Count == rooms.Count;        
    }

    bool AreRoomsConnected(RectInt room1, RectInt room2)
    {
        return (room1.xMin < room2.xMax && room1.xMax > room2.xMin && room1.yMin < room2.yMax && room1.yMax > room2.yMin) || // vertical and horizontal overlap
           (room1.xMax == room2.xMin || room1.xMin == room2.xMax || room1.yMax == room2.yMin || room1.yMin == room2.yMax);  // side by side and top bottom 
    }
    #endregion

    #region Create Doors
    IEnumerator PutDoors()
    {
        yield return new WaitForSeconds(1);

        doors.Clear();
        dungeonGraph.Clear();

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

                    if (xMax - xMin >= 5) // vertical wall
                    {
                        int doorX = (xMin + xMax) / 2 + randomOffset;
                        int doorY = yMin;
                        doorPosition = new Vector2Int(doorX, doorY);
                    }
                    else if (yMax - yMin >= 5) // horizontal wall
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
