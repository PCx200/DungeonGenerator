using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] PlayerController playerPrefab;
    [SerializeField] MouseClickController mouseClickController;

    public void SpawnPlayer()
    {
        RectInt startRoom = GenerateDungeon.Instance.GetTopRightRoom(GenerateDungeon.Instance.dungeonRooms);
        Vector3 spawnPos = new Vector3(startRoom.x + startRoom.width / 2, 0, startRoom.y + startRoom.height / 2);
        PlayerController playerClone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        PlayerController player = playerClone.GetComponent<PlayerController>();
        RegisterPlayer(player);

        // After Spawning the player, the main camera is being put as a child of the player object
        SetCamera(playerClone);
    }

    public void RegisterPlayer(PlayerController playerController)
    {
        mouseClickController.OnClick.RemoveAllListeners();
        mouseClickController.OnClick.AddListener(playerController.GoToDestination);
    }

    private void SetCamera(PlayerController player)
    {
        Camera mainCamera = Camera.main;
        mainCamera.transform.SetParent(player.transform);
        mainCamera.transform.localPosition = new Vector3(0, 9, -7);
        mainCamera.transform.rotation = Quaternion.Euler(35, 0, 0);
    }
}
