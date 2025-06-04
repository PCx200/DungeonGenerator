using NUnit.Framework;
using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using NaughtyAttributes;
using System.Linq;
using UnityEngine.UI;
using System.Xml.Linq;
using System.Threading;
using Unity.Properties;
using UnityEngine.Analytics;
using UnityEditor;
using System.Text;
using UnityEngine.Events;
using Unity.AI.Navigation;

public class GenerateDungeon : MonoBehaviour
{
    public static GenerateDungeon Instance;

    [SerializeField] public bool createImmediately = false;

    [SerializeField] NavMeshSurface navMeshSurface;

    public RectInt dungeon = new RectInt(0, 0, 0, 0);

    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject floorPrefab;
    public bool useSimpleAssets;
    public enum MapSize { Tiny, Small, Medium, Large, Huge }

    public MapSize map;

    [SerializeField] int seed;
    [SerializeField] bool useRandomSeed;

    // variables for room modification
    [SerializeField] int minRoomSize;

    float splitPercent;
    bool verticalSplit;


    int roomHeight;

    // needed for creating wall between intersecting rooms
    int roomOverlap = 1;

    // what percent of the smallest rooms you want to remove after creating the dungeon
    [SerializeField] int removePercentage;

    public List<RectInt> dungeonRooms;
    public List<RectInt> doors;

    Graph<Node> graph = new Graph<Node>();
    Dictionary<(Node, Node), int> edgeWeights = new Dictionary<(Node, Node), int>();
    List<Node> visitedNodes = new List<Node>();

    System.Random rand;

    [SerializeField] UnityEvent onGenerateDungeon;

    void Start()
    {
        DungeonGenerate();
        Instance = this;    
    }

    [Button]
    void DungeonGenerate()
    {
        GenerateSeed();
        dungeonRooms = new List<RectInt>();
        doors = new List<RectInt>();
        ChoseMap();
        dungeonRooms.Add(dungeon);
        graph = new Graph<Node>();
        edgeWeights = new Dictionary<(Node, Node), int>();
        visitedNodes = new List<Node>();
        StartCoroutine(RecursiveSplit());
    }

    void GenerateSeed()
    {
        if (!useRandomSeed)
        {
            rand = new System.Random(seed);
            //Debug.Log(seed);
        }
        else
        {
            seed = Random.Range(0, 1000000);
            rand = new System.Random(seed);
            Debug.Log(seed);
        }
    }

    #region Split
 
    /// <summary>
    /// Splits a given room either vertically or horizontally into two smaller rooms,
    /// </summary>
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

    /// <summary>
    /// Recursively splits rooms until no further valid splits can be made.
    /// </summary>
    IEnumerator RecursiveSplit()
    {
        bool hasSplit = false;
        List<RectInt> currentRooms = new List<RectInt>(dungeonRooms);

        foreach (RectInt room in currentRooms)
        {
            if (room.width > minRoomSize * 2 || room.height > minRoomSize * 2)
            {
                (RectInt room1, RectInt room2) = Split(room);
                hasSplit = true;
                if (!createImmediately)
                {
                    yield return new WaitForSeconds(0.05f);
                }

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
                //foreach (RectInt door in doors)
                //{
                //    AlgorithmsUtils.DebugRectInt(door, Color.green, 10, true, roomHeight);
                //}
            }

            //draw all the rooms -> too expenisve
            //for (int i = 0; i < dungeonRooms.Count; i++)
            //{
            //    RectInt roomToDraw = dungeonRooms[i];
            //    DebugDrawingBatcher.BatchCall(() => AlgorithmsUtils.DebugRectInt(roomToDraw, Color.white, 1, true, roomHeight));
            //}
            yield return StartCoroutine(CreateGraph());
        }
    }
    #endregion

    #region Rooms Removal
    /// <summary>
    /// Removes a specified percentage of the smallest rooms while preserving dungeon connectivity.
    /// Also removes doors associated with the removed rooms and visualizes the process.
    /// </summary>
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
            if (!createImmediately)
            {
                yield return new WaitForSeconds(0.2f);
            }

        }

    }
    #endregion

    #region Check Conectivity
    /// <summary>
    /// DFS to check whether all rooms in the dungeon are connected via shared doors.
    /// </summary>
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

    /// <summary>
    /// Checks whether two rooms share a door by verifying intersection with existing doors.
    /// </summary>
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

    /// <summary>
    /// Detects intersecting rooms and places doors between them with slight randomness.
    /// </summary>
    IEnumerator PutDoors()
    {
        if (!createImmediately)
        {
            yield return new WaitForSeconds(1);
        }

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
                    if (!createImmediately)
                    {
                        yield return new WaitForSeconds(0.01f);
                    }
                    AlgorithmsUtils.DebugRectInt(door, Color.red, 10, true, roomHeight);

                }
            }
        }
    }
    #endregion

    #region Graph and Traversal

    /// <summary>
    /// Constructs a graph representing rooms and doors, adding nodes and weighted edges for shared doors.
    /// </summary>
    IEnumerator CreateGraph()
    {
        int graphLenght;
        Node firstRoom = new Node();

        RectInt topRightRoom = GetTopRightRoom(dungeonRooms);

        firstRoom.node = topRightRoom;

        foreach (RectInt room in dungeonRooms)
        {
            Node roomNode = new Node();
            roomNode.node = room;
            graph.AddNode(roomNode);
        }
        graphLenght = graph.GetNodeCount();
        //Debug.Log(graphNodes.GetNodeCount());

        for (int i = 0; i < graphLenght; i++)
        {
            for (int j = i + 1; j < graphLenght; j++)
            {
                RectInt sharedDoor;

                Node nodeA = graph.GetNode(i);
                Node nodeB = graph.GetNode(j);

                if (AlgorithmsUtils.Intersects(nodeA.node, nodeB.node))
                {
                    if (ShareDoor(nodeA.node, nodeB.node, out sharedDoor))
                    {
                        Node doorNode = new Node();
                        doorNode.isDoor = true;
                        doorNode.node = sharedDoor;

                        Vector3 posRoomA = new Vector3(nodeA.node.x + nodeA.node.width / 2, 0, nodeA.node.y + nodeA.node.height / 2);
                        Vector3 posRoomB = new Vector3(nodeB.node.x + nodeB.node.width / 2, 0, nodeB.node.y + nodeB.node.height / 2);
                        Vector3 doorPos = new Vector3(doorNode.node.x + doorNode.node.width, 0, doorNode.node.y + doorNode.node.height);

                        graph.AddNode(doorNode);

                        graph.AddEdge(nodeA, doorNode);
                        //doorNode.edgeCount++;
                        graph.AddEdge(doorNode, nodeB);
                        //doorNode.edgeCount++;

                        edgeWeights[(nodeA, doorNode)] = (int)Vector3.Distance(posRoomA, doorPos);
                        edgeWeights[(doorNode, nodeB)] = (int)Vector3.Distance(doorPos, posRoomB);
                    }

                }
            }
        }

        //Debug.Log(graphNodes.GetNodeCount());
        //Debug.Log(edgeWeights.Count);



        List<KeyValuePair<(Node, Node), int>> sorted = edgeWeights.OrderByDescending(edge => edge.Value).ToList();

        yield return StartCoroutine(KruskalMST(firstRoom, sorted));

        yield break;
    }

    /// <summary>
    /// Executes Kruskal's algorithm to form a Minimum Spanning Tree (MST) from a sorted list of edges.
    /// Also traverses the MST for visual debugging and triggers dungeon generation.
    /// </summary>
    IEnumerator KruskalMST(Node startNode, List<KeyValuePair<(Node, Node), int>> sortedEdges)
    {
        Dictionary<Node, Node> parent = new Dictionary<Node, Node>();
        HashSet<Node> visitedNodes = new HashSet<Node>();
        Stack<Node> stack = new Stack<Node>();

        InitializeUnionFind(graph.GetNodes(), parent);

        BuildMST(sortedEdges, graph.GetNodeCount(), parent, visitedNodes, stack);

        RemoveSingleConnectionDoors();
        yield return TraverseMST(startNode, sortedEdges, visitedNodes, stack);
        //yield return TraverseMSTRecursive(startNode, sortedEdges, visitedNodes);

        onGenerateDungeon.Invoke();
    }

    #region Traversal Helper Methods

    /// <summary>
    /// Initializes the union-find data structure used in Kruskal’s algorithm by assigning each node as its own parent.
    /// </summary>
    void InitializeUnionFind(IEnumerable<Node> nodes, Dictionary<Node, Node> parent)
    {
        parent.Clear();
        foreach (Node node in nodes)
        {
            parent[node] = node;
        }
    }

    /// <summary>
    /// Finds the root of the set that the given node belongs to, with path compression for efficiency.
    /// </summary>
    Node Find(Node node, Dictionary<Node, Node> parent)
    {
        if (parent[node] != node)
        {
            parent[node] = Find(parent[node], parent); // Path compression
        }
        return parent[node];
    }

    /// <summary>
    /// Unifies the sets of two nodes in the union-find structure, making one the parent of the other.
    /// </summary>
    void Union(Node node1, Node node2, Dictionary<Node, Node> parent)
    {
        Node root1 = Find(node1, parent);
        Node root2 = Find(node2, parent);
        if (root1 != root2)
        {
            parent[root2] = root1;
        }
    }

    /// <summary>
    /// Constructs the MST from sorted edges using union-find and tracks which nodes are included.
    /// Also updates edge counts and prepares the stack for traversal.
    /// </summary>
    void BuildMST(List<KeyValuePair<(Node, Node), int>> sortedEdges, int nodeCount, Dictionary<Node, Node> parent, HashSet<Node> visitedNodes, Stack<Node> stack)
    {
        visitedNodes.Clear();
        stack.Clear();
        int edgeCount = 0;

        foreach (KeyValuePair<(Node, Node), int> edge in sortedEdges)
        {
            Node node1 = edge.Key.Item1;
            Node node2 = edge.Key.Item2;

            if (Find(node1, parent) != Find(node2, parent))
            {
                Union(node1, node2, parent);
                node1.edgeCount++;
                node2.edgeCount++;

                if (visitedNodes.Add(node1))
                { 
                    stack.Push(node1);
                }
                else
                {
                    stack.Push(node2);
                }

                edgeCount++;
                if (edgeCount == nodeCount - 1)
                { 
                    break; 
                }
            }
        }
    }

    /// <summary>
    /// Traverses the MST using an iterative DFS approach, visualizing edges and marking visited nodes.
    /// </summary>
    IEnumerator TraverseMST(Node startNode, List<KeyValuePair<(Node, Node), int>> sortedEdges, HashSet<Node> visitedNodes, Stack<Node> stack)
    {
        visitedNodes.Clear();
        stack.Clear();
        stack.Push(startNode);

        while (stack.Count > 0)
        {
            Node current = stack.Pop();

            if (!visitedNodes.Contains(current))
            {
                visitedNodes.Add(current);

                foreach (KeyValuePair<(Node,Node), int> edge in sortedEdges)
                {
                    Node node1 = edge.Key.Item1;
                    Node node2 = edge.Key.Item2;

                    if (IsValidMSTEdge(current, node1, node2))
                    {
                        Node neighbor = (node1 == current) ? node2 : node1;
                        if (!visitedNodes.Contains(neighbor))
                            stack.Push(neighbor);

                        DrawDebugEdge(node1, node2);

                        if (!createImmediately)
                            yield return new WaitForSeconds(0.05f);
                    }
                }
            }
        }
        this.visitedNodes = visitedNodes.ToList();
    }
    IEnumerator TraverseMSTRecursive(Node startNode, List<KeyValuePair<(Node, Node), int>> sortedEdges, HashSet<Node> visitedNodes)
    {

        if (!visitedNodes.Contains(startNode))
        { 
            visitedNodes.Add(startNode);

            foreach (KeyValuePair<(Node, Node), int> edge in sortedEdges)
            {
                Node node1 = edge.Key.Item1;
                Node node2 = edge.Key.Item2;

                if (IsValidMSTEdge(startNode, node1, node2))
                {

                    Node neighbor = (node1 == startNode) ? node2 : node1;
                    DrawDebugEdge(node1, node2);
                    if (!visitedNodes.Contains(neighbor))
                    {
                        yield return StartCoroutine(TraverseMSTRecursive(neighbor, sortedEdges, visitedNodes));
                    }
                }

            }

        }
        this.visitedNodes = visitedNodes.ToList();
    }

    /// <summary>
    /// Determines whether a given edge between two nodes is valid for inclusion in the MST visualization.
    /// Specifically checks for door nodes with sufficient connectivity.
    /// </summary>
    bool IsValidMSTEdge(Node current, Node node1, Node node2)
    {
        return
            (node1.node == current.node && node2.edgeCount != 1 && node2.isDoor) ||
            (node2.node == current.node && node1.edgeCount != 1 && node1.isDoor) ||
            (node1.node == current.node && node1.edgeCount != 1 && node1.isDoor) ||
            (node2.node == current.node && node2.edgeCount != 1 && node2.isDoor);
    }

    /// <summary>
    /// Draws visual debug lines and spheres between two nodes to represent MST connections in the scene.
    /// </summary>
    void DrawDebugEdge(Node node1, Node node2)
    {
        Vector3 pos1 = new Vector3(node1.node.x + node1.node.width / 2f, 0, node1.node.y + node1.node.height / 2f);
        Vector3 pos2 = new Vector3(node2.node.x + node2.node.width / 2f, 0, node2.node.y + node2.node.height / 2f);

        DebugExtension.DebugWireSphere(pos1, 1f, 100);
        DebugExtension.DebugWireSphere(pos2, 1f, 100);
        Debug.DrawLine(pos1, pos2, Color.cyan, 100);
    }

    /// <summary>
    /// Removes door nodes from the graph that are only connected to one other node, cleaning up unused doors.
    /// </summary>
    void RemoveSingleConnectionDoors()
    {
        List<Node> doorsToRemove = graph.GetNodes().Where(n => n.isDoor && n.edgeCount == 1).ToList();

        foreach (var door in doorsToRemove)
        {
            graph.RemoveNode(door);
            doors.Remove(door.node);

            AlgorithmsUtils.DebugRectInt(door.node, Color.red, 5);
        }
    }
    #endregion

    public RectInt GetTopRightRoom(List<RectInt> rooms)
    {
        var topRooms = rooms.Where(r => r.yMax == rooms.Max(r => r.yMax));

        return topRooms.OrderByDescending(r => r.xMax).FirstOrDefault();
    }
    #endregion

    #region Simplest Asset Generation
    public void SpawnSimpleAssets()
    {
        SpawnWalls();
        SpawnFloor();
    }

    void SpawnWalls()
    {
        HashSet<Vector3> placedPositions = new HashSet<Vector3>();
        HashSet<Vector2Int> doorWorldPositions = new HashSet<Vector2Int>();

        // Store door positions as Vector2Int to avoid floating point precision issues
        foreach (RectInt door in doors)
        {
            doorWorldPositions.Add(new Vector2Int(door.x, door.y));
        }

        foreach (RectInt room in dungeonRooms)
        {
            // Bottom and top walls
            for (int i = 0; i < room.width; i++)
            {
                Vector3 bottomPos = new Vector3(room.x + i + 0.5f, 0.5f, room.y + 0.5f);
                Vector3 topPos = new Vector3(room.x + i + 0.5f, 0.5f, room.y + room.height - 0.5f);

                Vector2Int bottomPosInt = new Vector2Int(room.x + i, room.y);
                Vector2Int topPosInt = new Vector2Int(room.x + i, room.y + room.height - 1);

                if (!doorWorldPositions.Contains(bottomPosInt) && placedPositions.Add(bottomPos))
                {
                    GameObject botWall = Instantiate(wallPrefab, bottomPos, Quaternion.identity);
                    botWall.name = $"BottomWall_{room.x + i}_{room.y}";
                }

                if (!doorWorldPositions.Contains(topPosInt) && placedPositions.Add(topPos))
                {
                    GameObject topWall = Instantiate(wallPrefab, topPos, Quaternion.identity);
                    topWall.name = $"TopWall_{room.x + i}_{room.y + room.height - 1}";
                }
            }

            // Left and right walls
            for (int i = 1; i < room.height - 1; i++)
            {
                Vector3 leftPos = new Vector3(room.x + 0.5f, 0.5f, room.y + i + 0.5f);
                Vector3 rightPos = new Vector3(room.x + room.width - 0.5f, 0.5f, room.y + i + 0.5f);

                Vector2Int leftPosInt = new Vector2Int(room.x, room.y + i);
                Vector2Int rightPosInt = new Vector2Int(room.x + room.width - 1, room.y + i);

                if (!doorWorldPositions.Contains(leftPosInt) && placedPositions.Add(leftPos))
                {
                    GameObject leftWall = Instantiate(wallPrefab, leftPos, Quaternion.identity);
                    leftWall.name = $"LeftWall_{room.x}_{room.y + i}";
                }

                if (!doorWorldPositions.Contains(rightPosInt) && placedPositions.Add(rightPos))
                {
                    GameObject rightWall = Instantiate(wallPrefab, rightPos, Quaternion.identity);
                    rightWall.name = $"RightWall_{room.x + room.width - 1}_{room.y + i}";
                }
            }
        }
    }

    void SpawnFloor()
    {
        HashSet<Vector3> visited = new HashSet<Vector3>();

        foreach (Node room in visitedNodes)
        {
            for (int i = 1; i < room.node.width - 1; i++)
            {
                for (int j = 1; j < room.node.height - 1; j++)
                {
                    Vector3 pos = new Vector3(room.node.x + i + 0.5f, 0, room.node.y + j + 0.5f);
                    if (visited.Add(pos))
                    {
                        Instantiate(floorPrefab, pos, Quaternion.Euler(90, 0, 0), transform);
                    }

                }
            }
        }
        foreach (RectInt door in doors)
        {
            Vector3 doorPos = new Vector3(door.x + 0.5f, 0, door.y + 0.5f);
            if (visited.Add(doorPos))
            {
                Instantiate(floorPrefab, doorPos, Quaternion.Euler(90, 0, 0), transform);
            }
        }
    }

    #endregion


    #region Map Settings
    /// <summary>
    /// Sets the dungeon size and minimum room size based on the selected map size enum.
    /// </summary>
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
                minRoomSize = 12;
                break;

            case MapSize.Large:
                dungeon = new RectInt(0, 0, 250, 250);
                minRoomSize = 12;
                break;

            case MapSize.Huge:
                dungeon = new RectInt(0, 0, 500, 500);
                minRoomSize = 12;
                break;
            default:
                dungeon = new RectInt();
                break;
        }
    }
    #endregion

    public Node GetStartNode()
    {
        return visitedNodes.FirstOrDefault();
    }

    [Button]
    public void BakeNavMesh()
    {
        navMeshSurface.BuildNavMesh();
    }
}
