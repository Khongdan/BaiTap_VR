using UnityEngine;
using BT_VR;

namespace BTTH_06
{
    /// <summary>
    /// Quan ly goc nhin Camera sinh dong cho Scene BTTH_06 (khong hien khung GUI che man hinh):
    /// - [1]: Bam theo xe o to dang lai trong thung lung (mac dinh)
    /// - [2]: Goc nhin toan canh thung lung & 3 dinh nui tuyet giong anh mau
    /// - [3]: Ngam truc thang bay tuan tra tren bau troi thung lung
    /// </summary>
    public class BTTH06CameraController : MonoBehaviour
    {
        public enum CameraMode
        {
            FollowCar = 1,
            ScenicOverview = 2,
            FollowHelicopter = 3
        }

        public Transform carTarget;
        public Transform helicopterTarget;
        public CameraMode currentMode = CameraMode.FollowCar;

        public Vector3 scenicPosition = new Vector3(0f, 16.5f, 8f);
        public Vector3 scenicLookAt = new Vector3(0f, 12f, 160f);
        public float smoothSpeed = 6f;

        private void Start()
        {
            SnapCamera();
        }

        private void Update()
        {
            if (InputHelper.GetKeyDown(KeyCode.Alpha1)) currentMode = CameraMode.FollowCar;
            else if (InputHelper.GetKeyDown(KeyCode.Alpha2)) currentMode = CameraMode.ScenicOverview;
            else if (InputHelper.GetKeyDown(KeyCode.Alpha3)) currentMode = CameraMode.FollowHelicopter;
        }

        private void LateUpdate()
        {
            GetPose(out Vector3 pos, out Quaternion rot);
            transform.position = Vector3.Lerp(transform.position, pos, smoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, smoothSpeed * Time.deltaTime);
        }

        private void SnapCamera()
        {
            GetPose(out Vector3 pos, out Quaternion rot);
            transform.position = pos;
            transform.rotation = rot;
        }

        private void GetPose(out Vector3 pos, out Quaternion rot)
        {
            if (currentMode == CameraMode.FollowCar && carTarget != null)
            {
                Vector3 flatFwd = Vector3.ProjectOnPlane(carTarget.forward, Vector3.up).normalized;
                if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
                pos = carTarget.position - flatFwd * 9.5f + Vector3.up * 4.4f;
                rot = Quaternion.LookRotation((carTarget.position + flatFwd * 8f + Vector3.up * 1.4f) - pos, Vector3.up);
                return;
            }

            if (currentMode == CameraMode.FollowHelicopter && helicopterTarget != null)
            {
                pos = helicopterTarget.position - helicopterTarget.forward * 14f + Vector3.up * 4.5f;
                rot = Quaternion.LookRotation(helicopterTarget.position - pos, Vector3.up);
                return;
            }

            pos = scenicPosition;
            rot = Quaternion.LookRotation(scenicLookAt - pos, Vector3.up);
        }
    }
}
