using UnityEngine;
using BT_VR;

namespace EcoPark
{
    /// <summary>
    /// Dieu khien xe o to (Asset Store Interceptor) chay tu do quanh Cong vien sinh thai & qua cau ProBuilder:
    /// - WASD / Phim mui ten: tien, lui, danh lai trai/phai
    /// - Space: phanh | L: den pha | K: coi | R: reset ve diem xuat phat
    /// - Su dung Raycast tu dong bam mat duong Terrain VA mat cau ProBuilder (chay muot qua cau bac ngang ho!).
    /// </summary>
    public class EcoParkCarController : MonoBehaviour
    {
        [Header("Thong so van hanh")]
        public float maxForwardSpeed = 16f;
        public float maxReverseSpeed = 7f;
        public float acceleration = 13f;
        public float deceleration = 16f;
        public float brakeDeceleration = 30f;
        public float turnSpeed = 80f;
        public float minDriveY = 2.0f; // Khong cho xe lao xuong long ho (mat nuoc Y = 1.8)
        public float maxClimbSlope = 34f;
        public float mapBoundHalf = 118f;

        [Header("Banh xe, Den & Am thanh")]
        public Transform[] wheels;
        public Light leftHeadlight;
        public Light rightHeadlight;
        public AudioSource engineAudio;
        public AudioSource sirenAudio;

        public bool enableHeadlights = true;
        public bool enableSiren = false;

        private float _currentSpeed;
        private Vector3 _startPos;
        private Quaternion _startRot;

        private void Start()
        {
            SnapToSurface(transform.position, true);
            _startPos = transform.position;
            _startRot = transform.rotation;
            ApplyLightsAndAudio();
        }

        private void Update()
        {
            if (InputHelper.GetKeyDown(KeyCode.L))
            {
                enableHeadlights = !enableHeadlights;
                ApplyLightsAndAudio();
            }

            if (InputHelper.GetKeyDown(KeyCode.K))
            {
                enableSiren = !enableSiren;
                ApplyLightsAndAudio();
            }

            if (InputHelper.GetKeyDown(KeyCode.R))
            {
                ResetCar();
                return;
            }

            float v = InputHelper.GetVerticalAxis();
            float h = InputHelper.GetHorizontalAxis();
            bool braking = InputHelper.GetKey(KeyCode.Space);

            if (braking)
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, brakeDeceleration * Time.deltaTime);
            else if (Mathf.Abs(v) > 0.05f)
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, v > 0f ? v * maxForwardSpeed : v * maxReverseSpeed, acceleration * Time.deltaTime);
            else
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * Time.deltaTime);

            if (Mathf.Abs(h) > 0.05f && Mathf.Abs(_currentSpeed) > 0.2f)
            {
                float dirSign = _currentSpeed >= 0f ? 1f : -1f;
                float factor = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / 3.5f);
                transform.Rotate(Vector3.up, h * turnSpeed * dirSign * factor * Time.deltaTime, Space.World);
            }

            Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;

            Vector3 nextPos = transform.position + flatFwd * (_currentSpeed * Time.deltaTime);
            nextPos.x = Mathf.Clamp(nextPos.x, -mapBoundHalf, mapBoundHalf);
            nextPos.z = Mathf.Clamp(nextPos.z, -mapBoundHalf, mapBoundHalf);

            if (SampleDrivableSurface(nextPos, out float hitY, out Vector3 hitNormal))
            {
                float slope = Vector3.Angle(hitNormal, Vector3.up);
                if (hitY < minDriveY || slope > maxClimbSlope)
                {
                    // Chan lai neu sap lao xuong ho nuoc hoac leo vach qua doc
                    _currentSpeed *= 0.2f;
                }
                else
                {
                    nextPos.y = hitY + 0.03f;
                    transform.position = nextPos;
                    AlignToNormal(hitNormal, false);
                }
            }

            SpinWheels();
            UpdateEngineAudio();
        }

        private void SnapToSurface(Vector3 pos, bool instant)
        {
            if (SampleDrivableSurface(pos, out float hitY, out Vector3 hitNormal))
            {
                pos.y = hitY + 0.03f;
                transform.position = pos;
                AlignToNormal(hitNormal, instant);
            }
        }

        private bool SampleDrivableSurface(Vector3 pos, out float y, out Vector3 normal)
        {
            // Raycast tu tren xuong, bo qua chinh collider cua xe de bat duoc ca Terrain lan cau ProBuilder
            Vector3 origin = new Vector3(pos.x, pos.y + 8f, pos.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 25f);
            float bestY = float.MinValue;
            Vector3 bestNormal = Vector3.up;
            bool found = false;

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform) continue;
                if (hit.collider.gameObject.name == "Water_Lake") continue;
                if (hit.collider.isTrigger) continue;

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    bestNormal = hit.normal;
                    found = true;
                }
            }

            y = bestY;
            normal = bestNormal;
            return found;
        }

        private void AlignToNormal(Vector3 normal, bool instant)
        {
            Vector3 projectedFwd = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            if (projectedFwd.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(projectedFwd, normal);
            transform.rotation = instant ? targetRot : Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        private void SpinWheels()
        {
            if (wheels == null || wheels.Length == 0) return;
            float spin = _currentSpeed * 110f * Time.deltaTime;
            foreach (Transform w in wheels)
            {
                if (w != null) w.Rotate(Vector3.right, spin, Space.Self);
            }
        }

        private void UpdateEngineAudio()
        {
            if (engineAudio == null || engineAudio.clip == null) return;
            if (!engineAudio.isPlaying) engineAudio.Play();

            float norm = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxForwardSpeed);
            engineAudio.pitch = Mathf.Lerp(0.85f, 1.80f, norm);
            engineAudio.volume = Mathf.Lerp(0.16f, 0.45f, norm);
        }

        private void ApplyLightsAndAudio()
        {
            if (leftHeadlight != null) leftHeadlight.enabled = enableHeadlights;
            if (rightHeadlight != null) rightHeadlight.enabled = enableHeadlights;

            if (sirenAudio != null && Application.isPlaying)
            {
                if (enableSiren && !sirenAudio.isPlaying && sirenAudio.clip != null) sirenAudio.Play();
                else if (!enableSiren && sirenAudio.isPlaying) sirenAudio.Pause();
            }
        }

        public void ResetCar()
        {
            _currentSpeed = 0f;
            transform.position = _startPos;
            transform.rotation = _startRot;
        }
    }
}
