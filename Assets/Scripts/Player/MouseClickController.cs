using UnityEngine;
using UnityEngine.Events;

public class MouseClickController : MonoBehaviour
{
    public Vector3 clickPosition;
    public UnityEvent<Vector3> OnClick;
    void Update() 
    {
        MoveOnClick();
    }

    void MoveOnClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(mouseRay, out RaycastHit hitInfo))
            {
                Vector3 clickWorldPosition = hitInfo.point;
                Debug.Log(clickWorldPosition);

                clickPosition = clickWorldPosition;

                Vector3 dir = clickPosition - transform.position;

                Debug.DrawRay(transform.position, dir, Color.red, 5f);

                OnClick.Invoke(clickPosition);
            }
        }
    }
}
