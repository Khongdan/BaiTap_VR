using UnityEngine;

namespace BT_VR
{
    /// <summary>
    /// Gan truc tiep tren Main Camera trong Scene BT_VR.
    /// Chuyen nhanh goc nhin: [1] Lai xe nguoi choi | [2] Toan canh ngoi nha | [3] Xem xe canh sat Animation.
    /// </summary>
    public class BT_VR_SceneController : MonoBehaviour
    {
        public enum ViewMode
        {
            FollowPlayerCar = 1,
            HouseOverview = 2,
            FollowPoliceCar = 3
        }

        [Header("Doi tuong trong Scene BT_VR")]
        public Tuan6CarController playerCar;
        public EditablePoliceCarController policeCar;

        [Header("Cau hinh Camera")]
        public ViewMode currentView = ViewMode.FollowPlayerCar;
        public Vector3 overviewPosition = new Vector3(-20f, 16f, -21f);
        public Vector3 overviewLookAt = new Vector3(0f, 1.5f, 0f);
        public float cameraSmoothSpeed = 7f;

        private void Start()
        {
            SnapCameraToCurrentView();
        }

        private void Update()
        {
            if (InputHelper.GetKeyDown(KeyCode.Alpha1)) currentView = ViewMode.FollowPlayerCar;
            else if (InputHelper.GetKeyDown(KeyCode.Alpha2)) currentView = ViewMode.HouseOverview;
            else if (InputHelper.GetKeyDown(KeyCode.Alpha3)) currentView = ViewMode.FollowPoliceCar;

            if (policeCar != null)
            {
                if (InputHelper.GetKeyDown(KeyCode.M)) policeCar.ToggleMode();
                if (InputHelper.GetKeyDown(KeyCode.L)) policeCar.ToggleLights();
                if (InputHelper.GetKeyDown(KeyCode.K)) policeCar.ToggleSirenAudio();
            }

            if (playerCar != null && InputHelper.GetKeyDown(KeyCode.R))
            {
                playerCar.ResetCar();
            }
        }

        private void LateUpdate()
        {
            GetDesiredCameraPose(out Vector3 targetPos, out Quaternion targetRot);
            transform.position = Vector3.Lerp(transform.position, targetPos, cameraSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, cameraSmoothSpeed * Time.deltaTime);
        }

        private void SnapCameraToCurrentView()
        {
            GetDesiredCameraPose(out Vector3 pos, out Quaternion rot);
            transform.position = pos;
            transform.rotation = rot;
        }

        private void GetDesiredCameraPose(out Vector3 targetPos, out Quaternion targetRot)
        {
            if (currentView == ViewMode.FollowPlayerCar && playerCar != null)
            {
                Transform t = playerCar.transform;
                targetPos = t.position - t.forward * 7.8f + Vector3.up * 4.2f;
                targetRot = Quaternion.LookRotation((t.position + Vector3.up * 1.1f) - targetPos, Vector3.up);
                return;
            }

            if (currentView == ViewMode.FollowPoliceCar && policeCar != null)
            {
                Transform t = policeCar.transform;
                targetPos = t.position - t.forward * 8.2f + Vector3.up * 4.4f;
                targetRot = Quaternion.LookRotation((t.position + Vector3.up * 1.1f) - targetPos, Vector3.up);
                return;
            }

            targetPos = overviewPosition;
            targetRot = Quaternion.LookRotation(overviewLookAt - targetPos, Vector3.up);
        }
    }
}
