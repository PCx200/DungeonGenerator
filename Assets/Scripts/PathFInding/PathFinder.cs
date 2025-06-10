using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum Algorithms
{
    BFS,
    Dijkstra,
    AStar
}

public class PathFinder : MonoBehaviour
{

    //public GraphGenerator graphGenerator;
    [SerializeField]TileMapGenerator tileMapGenerator;
    private Vector3 startNode;
    private Vector3 endNode;

    public List<Vector3> path = new List<Vector3>();
    HashSet<Vector3> discovered = new HashSet<Vector3>();

    private Graph<Vector3> graph = new Graph<Vector3>();

    public Algorithms algorithm = Algorithms.BFS;

    void Awake()
    {
        tileMapGenerator = GetComponent<TileMapGenerator>();
        tileMapGenerator.onPlacedAssets.AddListener(OnGraphReady);
    }

    void OnGraphReady()
    {
        graph = tileMapGenerator.floorGraph;
        Debug.Log("Graph loaded! Nodes: " + graph.GetNodeCount());
    }

    private Vector3 GetClosestNodeToPosition(Vector3 position)
    {
        Vector3 snappedPosition = new Vector3(
          Mathf.Round(position.x),
          0f,
          Mathf.Round(position.z)
        );

        Vector3 closestNode = Vector3.zero;
        float closestDistance = Mathf.Infinity;

        foreach (Vector3 node in graph.GetNodes())
        {
            float distance = Vector3.Distance(snappedPosition, node);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestNode = node;
            }
        }

        return closestNode;
    }

    public List<Vector3> CalculatePath(Vector3 from, Vector3 to)
    {
        Vector3 playerPosition = from;

        startNode = GetClosestNodeToPosition(playerPosition);
        endNode = GetClosestNodeToPosition(to);

        List<Vector3> shortestPath = new List<Vector3>();

        switch (algorithm)
        {
            case Algorithms.BFS:
                shortestPath = BFS(startNode, endNode);
                break;
            case Algorithms.Dijkstra:
                shortestPath = Dijkstra(startNode, endNode);
                break;
            case Algorithms.AStar:
                shortestPath = AStar(startNode, endNode);
                break;
        }

        path = shortestPath; //Used for drawing the path

        return shortestPath;
    }

    List<Vector3> BFS(Vector3 start, Vector3 end)
    {
        discovered.Clear();
        Queue<Vector3> queue = new Queue<Vector3>();
        Dictionary<Vector3, Vector3> childParent = new Dictionary<Vector3, Vector3>();

        queue.Enqueue(start);
        discovered.Add(start);

        while (queue.Count > 0)
        {
            Vector3 vis = queue.Dequeue();
            discovered.Add(vis);
            if (vis == end)
            {
                return ReconstructPath(childParent, start, end);
            }
            foreach (Vector3 neighbour in graph.GetNeighbors(vis))
            {
                if (!discovered.Contains(neighbour))
                {
                    queue.Enqueue(neighbour);
                    discovered.Add(neighbour);
                    childParent.Add(neighbour, vis);

                }
            }

        }

        //Use this "discovered" list to see the nodes in the visual debugging used on OnDrawGizmos()


        return new List<Vector3>(); // No path found
    }


    public List<Vector3> Dijkstra(Vector3 start, Vector3 end)
    {
        //Use this "discovered" list to see the nodes in the visual debugging used on OnDrawGizmos()
        discovered.Clear();

        List<(Vector3 node, float priority)> priorityQueue = new List<(Vector3 node, float priority)>();

        Dictionary<Vector3, float> costs = new Dictionary<Vector3, float>();
        Dictionary<Vector3, Vector3> childParent = new Dictionary<Vector3, Vector3>();

        priorityQueue.Add((start, 0));
        costs.Add(start, 0);

        discovered.Add(start);


        while (priorityQueue.Count > 0)
        {
            priorityQueue = priorityQueue.OrderBy(p => p.priority).ToList();
            Vector3 v = priorityQueue[0].node;
            priorityQueue.RemoveAt(0);

            if (v == end)
            {
                return ReconstructPath(childParent, start, end);
            }
            foreach (var neighbour in graph.GetNeighbors(v))
            {
                float newCost = costs[v] + Cost(v, neighbour);
                if (!costs.ContainsKey(neighbour) || newCost < costs[neighbour])
                {
                    costs[neighbour] = newCost;
                    childParent[neighbour] = v;
                    priorityQueue.Add((neighbour, newCost));
                    discovered.Add(neighbour);
                }
            }

        }
        /* */
        return new List<Vector3>(); // No path found
    }

    List<Vector3> AStar(Vector3 start, Vector3 end)
    {
        //Use this "discovered" list to see the nodes in the visual debugging used on OnDrawGizmos()
        discovered.Clear();

        List<(Vector3 node, float priority)> priorityQueue = new List<(Vector3 node, float priority)>();

        Dictionary<Vector3, float> costs = new Dictionary<Vector3, float>();
        Dictionary<Vector3, Vector3> childParent = new Dictionary<Vector3, Vector3>();

        priorityQueue.Add((start, 0));
        costs.Add(start, 0);

        discovered.Add(start);


        while (priorityQueue.Count > 0)
        {
            priorityQueue = priorityQueue.OrderBy(p => p.priority).ToList();

            Vector3 v = priorityQueue[0].node;
            priorityQueue.RemoveAt(0);

            if (v == end)
            {
                return ReconstructPath(childParent, start, end);
            }
            foreach (var neighbour in graph.GetNeighbors(v))
            {
                float newCost = costs[v] + Cost(v, neighbour);
                if (!costs.ContainsKey(neighbour) || newCost < costs[neighbour])
                {
                    costs[neighbour] = newCost;
                    childParent[neighbour] = v;
                    priorityQueue.Add((neighbour, newCost + Heuristic(neighbour, end)));
                    discovered.Add(neighbour);
                }
            }

        }
        /* */
        return new List<Vector3>(); // No path found
    }

    public float Cost(Vector3 from, Vector3 to)
    {
        return Vector3.Distance(from, to);
    }

    public float Heuristic(Vector3 from, Vector3 to)
    {
        return Vector3.Distance(from, to);
    }

    List<Vector3> ReconstructPath(Dictionary<Vector3, Vector3> parentMap, Vector3 start, Vector3 end)
    {
        List<Vector3> path = new List<Vector3>();
        Vector3 currentNode = end;

        while (currentNode != start)
        {
            path.Add(currentNode);
            currentNode = parentMap[currentNode];
        }

        path.Add(start);
        path.Reverse();
        return path;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startNode, .3f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(endNode, .3f);

        if (discovered != null)
        {
            foreach (var node in discovered)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(node, .3f);
            }
        }

        if (path != null)
        {
            foreach (var node in path)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(node, .3f);
            }
        }


    }
}
