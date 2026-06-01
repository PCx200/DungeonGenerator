<h1>Dungeon Generator</h1>

For this project, I explored different pathfinding algorithms (Breadth-First Search, Depth-First Search, Dijkstra’s Algorithm, A*). With these algorithms, I made an agent move across a generated grid. The user could choose which algorithm would be used by the agent. When the user clicks on a specific tile, it is visually shown which path the agent is going to take, as well as all the tiles that were checked or used by the algorithm. This clearly demonstrated the differences and optimisations between the algorithms, as well as my understanding of how to implement them within the project.

Another topic that I explored was Minimal Spanning Trees (Kruskal’s Algorithm and Prim’s Algorithm), which I used to make the dungeon more interesting and to remove unnecessary loops created during the initial dungeon generation. I used backtracking when removing rooms, ensuring that I would not accidentally split the dungeon into two separate halves. If this occurred, I would place the last removed room back into the dungeon and the room list.

Other topics I explored were Marching Squares (bitwise weighting of the tiles) and Flood Fill, allowing me to generate dungeon assets such as different wall and floor variations. Finally, I explored the Wave Function Collapse Algorithm, which observes local patterns and propagates constraints outward.

<img width="660" height="600" alt="DungeonGenerator gif" src="https://github.com/user-attachments/assets/1332d9a0-b45a-4644-a0e2-47f010c61f67" />

