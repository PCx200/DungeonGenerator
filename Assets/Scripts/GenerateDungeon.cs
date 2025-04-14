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
using UnityEngine.Analytics;
using UnityEditor;
using System.Text;
using UnityEngine.Events;

public class GenerateDungeon : MonoBehaviour
{
    public RectInt dungeon = new RectInt(0, 0, 0, 0);

    [SerializeField]
    private UnityEvent onGenerateDungeon;

    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject floorPrefab;
    public enum MapSize { Tiny, Small, Medium, Large, Huge }

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
    [SerializeField] public List<RectInt> dungeonRooms;

    [SerializeField] public List<RectInt> doors;

    // graph to represent the connection between the rooms

    [SerializeField] Graph<GraphNodes> graphNodes = new Graph<GraphNodes>();

    [SerializeField] Dictionary<(GraphNodes, GraphNodes), int> edgeWeights = new Dictionary<(GraphNodes, GraphNodes), int>();

    List<GraphNodes> visitedGraphNodes = new List<GraphNodes>();

    System.Random rand;

    int[,] _tileMap;


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
            seed = Random.Range(0, 1000000);
            rand = new System.Random(seed);
            Debug.Log(seed);
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
                    AlgorithmsUtils.DebugRectInt(door, Color.green, 10, true, roomHeight);
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
        foreach (RectInt door in doors)
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

                    int randomOffset = rand.Next(-1, 2);

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

    #region Graph and Traversal
    IEnumerator CreateGraph()
    {
        int graphLenght;
        GraphNodes firstRoom = new GraphNodes();

        RectInt topRightRoom = GetTopRightRoom(dungeonRooms);

        firstRoom.node = topRightRoom;

        foreach (RectInt room in dungeonRooms)
        {
            GraphNodes roomNode = new GraphNodes();
            roomNode.node = room;
            graphNodes.AddNode(roomNode);
        }
        graphLenght = graphNodes.GetNodeCount();
        Debug.Log(graphNodes.GetNodeCount());

        for (int i = 0; i < graphLenght; i++)
        {
            for (int j = i + 1; j < graphLenght; j++)
            {
                RectInt sharedDoor;

                GraphNodes nodeA = graphNodes.GetNode(i);
                GraphNodes nodeB = graphNodes.GetNode(j);

                if (AlgorithmsUtils.Intersects(nodeA.node, nodeB.node))
                {
                    if (ShareDoor(nodeA.node, nodeB.node, out sharedDoor))
                    {
                        GraphNodes doorNode = new GraphNodes();
                        doorNode.isDoor = true;
                        doorNode.node = sharedDoor;

                        Vector3 pos1 = new Vector3(nodeA.node.x + nodeA.node.width / 2, 0, nodeA.node.y + nodeA.node.height / 2);
                        Vector3 pos2 = new Vector3(nodeB.node.x + nodeB.node.width / 2, 0, nodeB.node.y + nodeB.node.height / 2);
                        Vector3 doorPos = new Vector3(doorNode.node.x + doorNode.node.width, 0, doorNode.node.y + doorNode.node.height);

                        graphNodes.AddNode(doorNode);

                        graphNodes.AddEdge(nodeA, doorNode);
                        //doorNode.edgeCount++;
                        graphNodes.AddEdge(doorNode, nodeB);
                        //doorNode.edgeCount++;

                        edgeWeights[(nodeA, doorNode)] = (int)Vector3.Distance(pos1, doorPos);
                        edgeWeights[(doorNode, nodeB)] = (int)Vector3.Distance(doorPos, pos2);
                    }

                }
            }
        }

        Debug.Log(graphNodes.GetNodeCount());
        Debug.Log(edgeWeights.Count);



        List<KeyValuePair<(GraphNodes, GraphNodes), int>> sorted = edgeWeights.OrderBy(edge => edge.Value).ToList();

        yield return StartCoroutine(KruskalMST(firstRoom, sorted));

        yield break;
    }

    IEnumerator KruskalMST(GraphNodes startNode, List<KeyValuePair<(GraphNodes, GraphNodes), int>> sortedEdges)
    {
        HashSet<GraphNodes> visitedNodes = new HashSet<GraphNodes>();
        Stack<GraphNodes> stack = new Stack<GraphNodes>(); // Track previous node


        // Step 2: Initialize Union-Find structure
        Dictionary<GraphNodes, GraphNodes> parent = new Dictionary<GraphNodes, GraphNodes>();


        foreach (GraphNodes node in graphNodes.GetNodes())
        {
            parent[node] = node; // Each node is its own parent initially
        }

        GraphNodes Find(GraphNodes node)
        {
            if (parent[node] != node)
            {
                parent[node] = Find(parent[node]); // Path compression
            }
            return parent[node];
        }

        void Union(GraphNodes node1, GraphNodes node2)
        {
            GraphNodes root1 = Find(node1);
            GraphNodes root2 = Find(node2);
            if (root1 != root2)
            {
                parent[root2] = root1;
            }
        }

        stack.Push((startNode)); // Start node has itself as previous

        while (stack.Count > 0)
        {
            GraphNodes current = stack.Pop();

            if (!visitedNodes.Contains(current))
            {
                visitedNodes.Add(current);

                // Step 3: Traverse edges in MST order
                foreach (var edge in sortedEdges)
                {
                    GraphNodes node1 = edge.Key.Item1;
                    GraphNodes node2 = edge.Key.Item2;

                    if ((node1.node == current.node || node2.node == current.node) && Find(node1) != Find(node2)) // Ensure it's a valid MST edge
                    {
                        GraphNodes neighbor = (node1 == current) ? node2 : node1;

                        if (!visitedNodes.Contains(neighbor))
                        {
                            edge.Key.Item1.edgeCount++;
                            edge.Key.Item2.edgeCount++; 
                            stack.Push(neighbor); // Pass current node as previous
                            Union(node1, node2);  
                        }
                    }
                }
            }
        }

        RemoveSingleConnectionDoors();
        Debug.Log(graphNodes.GetNodeCount());
        visitedNodes.Clear();
        stack.Clear();
        stack.Push(startNode);

        while (stack.Count > 0)
        {
            GraphNodes current = stack.Pop();

            if (!visitedNodes.Contains(current))
            {
                visitedNodes.Add(current);
                visitedGraphNodes.Add(current);

                // Step 3: Traverse edges in MST order
                foreach (var edge in sortedEdges)
                {
                    GraphNodes node1 = edge.Key.Item1;
                    GraphNodes node2 = edge.Key.Item2;

                    if (node1.node == current.node && node2.edgeCount != 1 && node2.isDoor 
                        || node2.node == current.node && node1.edgeCount != 1 && node1.isDoor
                        || node1.node == current.node && node1.edgeCount != 1 && node1.isDoor 
                        || node2.node == current.node && node2.edgeCount != 1 && node2.isDoor
                        )
                    {

                        GraphNodes neighbor = (node1 == current) ? node2 : node1;

                        if (!visitedNodes.Contains(neighbor))
                        {
                            stack.Push(neighbor); // Pass current node as previous
                        }

                        //Draw the Graph(Nodes & Edges) during execution
                        Vector3 pos1 = new Vector3(node1.node.x + node1.node.width / 2f, 0, node1.node.y + node1.node.height / 2f);
                        Vector3 pos2 = new Vector3(node2.node.x + node2.node.width / 2f, 0, node2.node.y + node2.node.height / 2f);

                        DebugExtension.DebugWireSphere(pos1, 1f, 100); // Draw nodes at center
                        DebugExtension.DebugWireSphere(pos2, 1f, 100);
                        Debug.DrawLine(pos1, pos2, Color.cyan, 100); // Draw edges

                        yield return new WaitForSeconds(0.05f); // Small delay to visualize step-by-step
                    }
                }
            }
        }
        onGenerateDungeon.Invoke();

        //GenerateTileMap();
        //SpawnDungeonAssets();
    }
    void RemoveSingleConnectionDoors()
    {
        List<GraphNodes> doorsToRemove = graphNodes.GetNodes().Where(n => n.isDoor && n.edgeCount == 1).ToList();

        foreach (var door in doorsToRemove)
        {
            graphNodes.RemoveNode(door);
            doors.Remove(door.node);

            AlgorithmsUtils.DebugRectInt(door.node, Color.red, 5);
        }
    }

    RectInt GetTopRightRoom(List<RectInt> rooms)
    {
        var topRooms = rooms.Where(r => r.yMax == rooms.Max(r => r.yMax));

        return topRooms.OrderByDescending(r => r.xMax).FirstOrDefault();
    }
    #endregion

    #region Simplest Asset Generation
    //public void SpawnDungeonAssets()
    //{
    //    SpawnWalls();
    //    SpawnFloor();
    //}

    //void SpawnWalls()
    //{
    //    HashSet<Vector3> placedPositions = new HashSet<Vector3>();
    //    List<Vector3> doorWorldPositions = new List<Vector3>();

    //    foreach (RectInt door in doors)
    //    {
    //        Vector3 doorPos = new Vector3(door.x + 0.5f, 0, door.y + 0.5f);
    //        doorWorldPositions.Add(doorPos);
    //    }

    //    foreach (RectInt room in dungeonRooms)
    //    {

    //        for (int i = 0; i < room.width; i++)
    //        {
    //            Vector3 bottomPos = new Vector3(room.x + i + 0.5f, 0.5f, room.y + 0.5f);
    //            if (!doorWorldPositions.Contains(bottomPos) && placedPositions.Add(bottomPos))
    //            {
    //                GameObject botWall = Instantiate(wallPrefab, bottomPos, Quaternion.identity);
    //                botWall.name = $"BottomWall_{room.x + i}_{room.y}";
    //            }

    //            Vector3 topPos = new Vector3(room.x + i + 0.5f, 0.5f, room.y + room.height - 0.5f);
    //            if (!doorWorldPositions.Contains(topPos) && placedPositions.Add(topPos))
    //            {
    //                GameObject topWall = Instantiate(wallPrefab, topPos, Quaternion.identity);
    //                topWall.name = $"TopWall_{room.x + i}_{room.y + room.height}";
    //            }

    //        }
    //        for (int i = 1; i < room.height - 1; i++)
    //        {
    //            Vector3 leftPos = new Vector3(room.x + 0.5f, 0.5f, room.y + i + 0.5f);
    //            if (!doorWorldPositions.Contains(leftPos) && placedPositions.Add(leftPos))
    //            {
    //                GameObject leftWall = Instantiate(wallPrefab, leftPos, Quaternion.identity);
    //                leftWall.name = $"LeftWall_{room.x}_{room.y + i}";
    //            }

    //            Vector3 rightPos = new Vector3(room.x + room.width - 0.5f, 0.5f, room.y + i + 0.5f);
    //            if (!doorWorldPositions.Contains(rightPos) && placedPositions.Add(rightPos))
    //            {
    //                GameObject rightWall = Instantiate(wallPrefab, rightPos, Quaternion.identity);
    //                rightWall.name = $"RightWall_{room.x + room.width}_{room.y + i}";
    //            }
    //        }
    //    }
    //}

    //void SpawnFloor()
    //{
    //    HashSet<Vector3> visited = new HashSet<Vector3>();

    //    foreach (GraphNodes room in visitedGraphNodes)
    //    {
    //        for (int i = 0; i < room.node.width; i++)
    //        {
    //            for (int j = 0; j < room.node.height; j++)
    //            {
    //                Vector3 pos = new Vector3(room.node.x + i + 0.5f, 0, room.node.y + j + 0.5f);
    //                if (visited.Add(pos))
    //                {
    //                    Instantiate(floorPrefab, pos, Quaternion.Euler(90, 0, 0), this.transform);
    //                }
                    
    //            }
    //        }
    //    }
    //}

    #endregion
   

    #region Map Settings
    void ChoseMap()
    {
        switch (map)
        {
            case MapSize.Tiny:
                dungeon = new RectInt(0, 0, 50, 50);
                minRoomSize = 15;
                break;
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

            case MapSize.Huge:
                dungeon = new RectInt(0, 0, 1000, 1000);
                minRoomSize = 24;
                break;
            default:
                dungeon = new RectInt();
            break;
        }
    }
    #endregion

}
