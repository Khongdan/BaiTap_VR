using UnityEngine;
using BT_VR;

namespace EcoPark
{
    /// <summary>
    /// Camera da goc nhin sinh dong cho Scene KhungCanhTuongTac (khong hien khung GUI):
    /// - [1]: Bam theo xe o to khi lai dao cong vien & qua cau (mac dinh)
    /// - [2]: Goc quay Cinematic Orbit tu dong xoay muot quanh toan bo Cong vien & Ho nuoc (dep de quay video nop bai)
    /// - [3]: Bay theo truc thang ngam toan canh tu tren cao
    /// </summary>
    public class EcoParkCameraController : MonoBehaviour
    {
        public enum CameraMode
        {
            FollowCar = 1,
            CinematicOrbit = 2,
            FollowHelicopter = 3
        }

        public Transform carTarget;
        public Transform helicopterTarget;
        public CameraMode currentMode = CameraMode.FollowCar;

        [Header("Cinematic Orbit")]
        public Vector3 orbitCenter = new Vector3(0f, 5f, 0f);
        public float orbitRadius = 56f;
        public float orbitHeight = 25f;
        public float orbitSpeedDegrees = 10f;
        public float smoothSpeed = 6f;

        private float _orbitAngle = -35f;

        private void Start()
        {
            SnapCamera();
        }

        private void Update()
        {
            if (InputHelper.GetKeyDown(KeyCode.Alpha1)) currentMode = CameraMode.FollowCar;
            else if (InputHelper.GetKeyDown(KeyCode.Alpha2)) currentMode = CameraMode.CinematicOrbit;
            else if (InputHelper.GetKeyDown(KeyCode.Alpha3)) currentMode = CameraMode.FollowHelicopter;

            if (currentMode == CameraMode.CinematicOrbit)
                _orbitAngle += orbitSpeedDegrees * Time.deltaTime;
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
                pos = carTarget.position - flatFwd * 9.5f + Vector3.up * 4.5f;
                rot = Quaternion.LookRotation((carTarget.position + flatFwd * 7f + Vector3.up * 1.4f) - pos, Vector3.up);
                return;
            }

            if (currentMode == CameraMode.FollowHelicopter && helicopterTarget != null)
            {
                pos = helicopterTarget.position - helicopterTarget.forward * 14f + Vector3.up * 5f;
                rot = Quaternion.LookRotation(helicopterTarget.position - pos, Vector3.up);
                return;
            }

            float rad = _orbitAngle * Mathf.Deg2Rad;
            pos = orbitCenter + new Vector3(Mathf.Cos(rad) * orbitRadius, orbitHeight, Mathf.Sin(rad) * orbitRadius);
            rot = Quaternion.LookRotation(orbitCenter - pos, Vector3.up);
        }
    }
}
