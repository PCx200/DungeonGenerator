using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Graph<T>
{
    private Dictionary<T, List<T>> adjacencyList;

    public Graph()
    {
        adjacencyList = new Dictionary<T, List<T>>();
    }

    public void Clear()
    {
        adjacencyList.Clear();
    }

    public void RemoveNode(T node)
    {
        if (adjacencyList.ContainsKey(node))
        {
            adjacencyList.Remove(node);
        }

        foreach (var key in adjacencyList.Keys)
        {
            adjacencyList[key].Remove(node);
        }
    }

    public List<T> GetNodes()
    {
        return new List<T>(adjacencyList.Keys);
    }

    public T GetNode(int index)
    {
        return adjacencyList.Keys.ElementAt(index);
    }

    public void AddNode(T node)
    {
        if (!adjacencyList.ContainsKey(node))
        {
            adjacencyList[node] = new List<T>();
        }
    }

    public void RemoveEdge(T fromNode, T toNode)
    {
        if (adjacencyList.ContainsKey(fromNode))
        {
            adjacencyList[fromNode].Remove(toNode);
        }
        if (adjacencyList.ContainsKey(toNode))
        {
            adjacencyList[toNode].Remove(fromNode);
        }
    }

    public void AddEdge(T fromNode, T toNode)
    {
        if (!adjacencyList.ContainsKey(fromNode))
        {
            AddNode(fromNode);
        }
        if (!adjacencyList.ContainsKey(toNode))
        {
            AddNode(toNode);
        }

        adjacencyList[fromNode].Add(toNode);
        adjacencyList[toNode].Add(fromNode);
    }

    public List<T> GetNeighbors(T node)
    {
        return new List<T>(adjacencyList[node]);
    }

    public int GetNodeCount()
    {
        return adjacencyList.Count;
    }

    public void PrintGraph()
    {
        foreach (var node in adjacencyList)
        {
            Debug.Log($"{node.Key}: {string.Join(", ", node.Value)}");
        }
    }

    // Breadth-First Search (BFS)
    public IEnumerator BFS(T startNode)
    {
        HashSet<T> visitedNodes = new HashSet<T>();
        Queue<T> queue = new Queue<T>();
        queue.Enqueue(startNode);
        visitedNodes.Add(startNode);
        Debug.Log(startNode + " Discovered");

        while (queue.Count > 0)
        {
            T currentNode = queue.Dequeue();
            foreach (var neighbour in adjacencyList[currentNode])
            {
                if (!visitedNodes.Contains(neighbour))
                {
                    yield return new WaitForSeconds(1);
                    queue.Enqueue(neighbour);
                    visitedNodes.Add(neighbour);
                    Debug.Log(neighbour + " Discovered");
                }
            }
        }
        foreach (var node in visitedNodes)
        {
            Debug.Log(node);
        }
    }

    // Depth-First Search (DFS)
    public void DFS(T startNode)
    {
        HashSet<T> visitedNodes = new HashSet<T>();
        Stack<T> stack = new Stack<T>();

        stack.Push(startNode);

        while (stack.Count > 0)
        {
            T currentNode = stack.Pop();

            if (!visitedNodes.Contains(currentNode))
            {
                visitedNodes.Add(currentNode);
                Debug.Log(currentNode);

                foreach (var neighbour in adjacencyList[currentNode])
                {
                    if (!visitedNodes.Contains(neighbour))
                    {
                        stack.Push(neighbour);
                    }
                }
            }
        }
    }
}