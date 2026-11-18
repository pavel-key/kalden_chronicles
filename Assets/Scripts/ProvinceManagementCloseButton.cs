
using UnityEngine;

public class ProvinceManagementCloseButton : MonoBehaviour
{
    public void Close()
    {
        if (ProvinceManagementSceneManager.Instance != null)
        {
            ProvinceManagementSceneManager.Instance.Close();
        }
    }
}
