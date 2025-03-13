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
        splitPercent = Random.Range(0.3f, 0.6f);

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

        AlgorithmsUtils.DebugRectInt(room1, Color.yellow, 100, true, roomHeight);
        AlgorithmsUtils.DebugRectInt(room2, Color.yellow, 100, true, roomHeight);

        return (room1, room2);
    }

    [Button()]
    IEnumerator RecursiveSplit()
    {
        bool hasSplit = false;
        List<RectInt> currentRooms = new List<RectInt>(dungeonRooms); // Copy of dungeonRooms

        foreach (var room in currentRooms)
        {
            if (room.width > minRoomSize * 2 || room.height > minRoomSize * 2)
            {
                (RectInt room1, RectInt room2) = Split(room);
                hasSplit = true;

                yield return new WaitForSeconds(0.05f); // Wait to visualize each split
            }
        }

        // Continue recursion if any room was split
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
