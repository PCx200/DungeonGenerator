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

    [SerializeField] int minRoomSize;
    [SerializeField] float splitPercent;
    [SerializeField] bool verticalSplit;

    [SerializeField] int roomCount;
    [SerializeField] int roomHeight;

    [SerializeField] int roomOverlap;

    [SerializeField] List<RectInt> dungeonRooms;

    [SerializeField] List<RectInt> doors;

    void Start()
    {
        ChoseMap();
        dungeonRooms.Add(dungeon);
        roomCount = dungeonRooms.Count;
        AlgorithmsUtils.DebugRectInt(dungeon, Color.blue, 100, true, roomHeight);
        StartCoroutine(RecursiveSplit());
    }


    (RectInt, RectInt) Split(RectInt pRoom)
    {
        RectInt room1 = pRoom;
        RectInt room2 = pRoom;

        verticalSplit = Random.value > 0.5f;
        splitPercent = Random.Range(0.375f, 0.725f);

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
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < dungeonRooms.Count; i++)
            {  
                RectInt roomToDraw = dungeonRooms[i];
                DebugDrawingBatcher.BatchCall(() => AlgorithmsUtils.DebugRectInt(roomToDraw, Color.white, 1, true, roomHeight));
            }
            StartCoroutine(PutDoors());
        }
    }

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
                    RectInt roomA = intersectingRooms[i];
                    RectInt roomB = intersectingRooms[j];

                    int xMin = Mathf.Max(roomA.xMin, roomB.xMin);
                    int xMax = Mathf.Min(roomA.xMax, roomB.xMax);
                    int yMin = Mathf.Max(roomA.yMin, roomB.yMin);
                    int yMax = Mathf.Min(roomA.yMax, roomB.yMax);

                    Vector2Int doorPosition;

                    int randomOffset = Random.Range(-1, 2);

                    if (xMax - xMin > 5) // vertical wall
                    {
                        int doorX = (xMin + xMax) / 2 - randomOffset;
                        int doorY = yMin;
                        doorPosition = new Vector2Int(doorX, doorY);
                    }
                    else if (yMax - yMin > 5) // horizontal wall
                    {
                        int doorX = xMin;
                        int doorY = (yMin + yMax) / 2 - randomOffset;
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


    void ChoseMap()
    {
        switch (map)
        {
            case MapSize.Small:
                dungeon = new RectInt(0, 0, 100, 100);
                minRoomSize = 8;
                break;
            case MapSize.Medium:
                dungeon = new RectInt(0, 0, 150, 150);
                minRoomSize = 12;
                break;
            case MapSize.Large:
                dungeon = new RectInt(0, 0, 200, 200);
                minRoomSize = 15;
                break;
            default:
                dungeon = new RectInt();
                break;
        }
    }
}
