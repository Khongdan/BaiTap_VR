using UnityEngine;
using BT_VR;

namespace BTTH_06
{
    /// <summary>
    /// Dieu khien xe o to (Interceptor template) chay tu do tren map thung lung nui tuyet BTTH_06:
    /// - WASD / Phim mui ten: tien, lui, danh lai trai/phai
    /// - Space: phanh | L: bat/tat den pha | K: bat/tat coi | R: dua xe ve vi tri xuat phat
    /// - Tu dong bam sat do cao va do nghieng cua Terrain, khong cho leo len vach nui qua doc.
    /// </summary>
    public class ValleyCarController : MonoBehaviour
    {
        [Header("Thong so van hanh")]
        public float maxForwardSpeed = 20f;
        public float maxReverseSpeed = 8f;
        public float acceleration = 14f;
        public float deceleration = 18f;
        public float brakeDeceleration = 32f;
        public float turnSpeed = 75f;
        public float maxClimbSlope = 30f;

        [Header("Gioi han khu vuc di chuyen trong map")]
        public Terrain valleyTerrain;
        public float minX = -32f;
        public float maxX = 32f;
        public float minZ = 10f;
        public float maxZ = 200f;

        [Header("Banh xe & Den & Am thanh")]
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
            if (valleyTerrain == null)
                valleyTerrain = Terrain.activeTerrain;

            SnapToTerrain(transform.position);
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
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, brakeDeceleration * Time.deltaTime);
            }
            else if (Mathf.Abs(v) > 0.05f)
            {
                float target = v > 0f ? v * maxForwardSpeed : v * maxReverseSpeed;
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, acceleration * Time.deltaTime);
            }
            else
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * Time.deltaTime);
            }

            // Danh lai khi xe dang di chuyen
            if (Mathf.Abs(h) > 0.05f && Mathf.Abs(_currentSpeed) > 0.2f)
            {
                float dirSign = _currentSpeed >= 0f ? 1f : -1f;
                float speedFactor = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / 4f);
                transform.Rotate(Vector3.up, h * turnSpeed * dirSign * speedFactor * Time.deltaTime, Space.World);
            }

            // Tinh vi tri tiep theo & kiem tra do doc nui
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 nextPos = transform.position + flatForward * (_currentSpeed * Time.deltaTime);
            nextPos.x = Mathf.Clamp(nextPos.x, minX, maxX);
            nextPos.z = Mathf.Clamp(nextPos.z, minZ, maxZ);

            if (valleyTerrain != null)
            {
                Vector3 normal = SampleTerrainNormal(nextPos);
                float slope = Vector3.Angle(normal, Vector3.up);
                if (slope > maxClimbSlope)
                {
                    // Chan lai khi gap vach nui qua doc
                    _currentSpeed *= 0.25f;
                    nextPos = transform.position;
                }
            }

            SnapToTerrain(nextPos);
            SpinWheels();
            UpdateEngineAudio();
        }

        private void SnapToTerrain(Vector3 worldPos)
        {
            if (valleyTerrain == null)
            {
                transform.position = worldPos;
                return;
            }

            float groundY = valleyTerrain.SampleHeight(worldPos) + valleyTerrain.transform.position.y;
            worldPos.y = groundY + 0.03f;
            transform.position = worldPos;

            Vector3 normal = SampleTerrainNormal(worldPos);
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            if (projectedForward.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(projectedForward, normal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }

        private Vector3 SampleTerrainNormal(Vector3 worldPos)
        {
            TerrainData data = valleyTerrain.terrainData;
            Vector3 local = worldPos - valleyTerrain.transform.position;
            float u = Mathf.Clamp01(local.x / data.size.x);
            float v = Mathf.Clamp01(local.z / data.size.z);
            return data.GetInterpolatedNormal(u, v);
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

            float speedNorm = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxForwardSpeed);
            engineAudio.pitch = Mathf.Lerp(0.85f, 1.85f, speedNorm);
            engineAudio.volume = Mathf.Lerp(0.18f, 0.50f, speedNorm);
        }

        private void ApplyLightsAndAudio()
        {
            if (leftHeadlight != null) leftHeadlight.enabled = enableHeadlights;
            if (rightHeadlight != null) rightHeadlight.enabled = enableHeadlights;

            if (sirenAudio != null && Application.isPlaying)
            {
                if (enableSiren && !sirenAudio.isPlaying && sirenAudio.clip != null)
                    sirenAudio.Play();
                else if (!enableSiren && sirenAudio.isPlaying)
                    sirenAudio.Pause();
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
