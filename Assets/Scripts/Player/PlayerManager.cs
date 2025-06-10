using Unity.VisualScripting;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] PlayerController playerPrefab;
    [SerializeField] MouseClickController mouseClickController;
    
    [SerializeField] Vector3 cameraPos = new Vector3(0, 9, -7);
    [SerializeField] Quaternion cameraRotation = Quaternion.Euler(35, 0, 0);

    [SerializeField] bool useNavMesh;
    [SerializeField] GameObject navMesh;

    /// <summary>
    /// Spawns the player in the top-right room of the dungeon.
    /// Instantiates the player prefab and attaches the camera to follow the player.
    /// </summary>
    /// 
    private void Awake()
    {
        if (useNavMesh)
        {
            navMesh.SetActive(true);
        }
        else
        {
            navMesh.SetActive(false);
        }
    }
    public void SpawnPlayer()
    {

        RectInt startRoom = GenerateDungeon.Instance.GetTopRightRoom(GenerateDungeon.Instance.dungeonRooms);
        Vector3 spawnPos = new Vector3(startRoom.x + startRoom.width / 2, 0, startRoom.y + startRoom.height / 2);
        PlayerController playerClone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        FollowPathController followPath = playerClone.GetComponent<FollowPathController>();
        followPath.pathFinder = FindFirstObjectByType<PathFinder>(); // Or assign it from a serialized field

        PlayerController player = playerClone.GetComponent<PlayerController>();
        RegisterPlayer(player);

        // After Spawning the player, the main camera is being put as a child of the player object
        SetCamera(playerClone);


    }

    /// <summary>
    /// Registers the player controller to respond to mouse click events.
    /// Clears old listeners to ensure only the current player is active.
    /// </summary>
    public void RegisterPlayer(PlayerController playerController)
    {
        mouseClickController.OnClick.RemoveAllListeners();
        if (useNavMesh)
        {
            mouseClickController.OnClick.AddListener(playerController.GoToDestination);
        }
        else
        {

            FollowPathController followPath = playerController.GetComponent<FollowPathController>();

            if (followPath != null)
            {
                mouseClickController.OnClick.AddListener(followPath.GoToDestination);
            }
            else
            {
                Debug.LogError("FollowPathController not found on player!");
            }
        }
    }

    /// <summary>
    /// Attaches the main camera as a child of the player object and positions it for a top-down view.
    /// </summary>
    private void SetCamera(PlayerController player)
    {
        Camera mainCamera = Camera.main;
        mainCamera.transform.SetParent(player.transform);
        mainCamera.transform.localPosition = cameraPos;
        mainCamera.transform.rotation = cameraRotation;
    }
}
