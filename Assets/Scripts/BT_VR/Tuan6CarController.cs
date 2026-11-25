using UnityEngine;

namespace BT_VR
{
    /// <summary>
    /// Xe o to nguoi choi dieu khien chay quanh ngoi nha (Tuan 6):
    /// - Dieu khien bang phim WASD / Mui ten (tien, lui, re trai/phai vong quanh nha).
    /// - Khi cham vat can (Barrier / tag Obstacle): bat khoi (Particle), phat am thanh va cham,
    ///   va KHOA HAN KHONG THE DI CHUYEN DUOC NUA (isBlocked = true).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Tuan6CarController : MonoBehaviour
    {
        [Header("Thong so lai xe quanh nha")]
        public float baseSpeed = 10f;
        public float turnSpeed = 95f;
        public float roadLimitX = 16.5f;
        public float roadLimitZ = 15.5f;

        [Header("Hieu ung & Am thanh")]
        public GameObject smokeParticle;
        public AudioSource engineAudio;
        public AudioSource sfxAudio;
        public AudioClip crashClip;

        [Header("Banh xe")]
        public Transform[] wheels;

        [Header("Trang thai")]
        public bool isBlocked = false;
        public float currentSpeed = 0f;

        private Rigidbody _rb;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        private void Start()
        {
            if (smokeParticle != null)
                smokeParticle.SetActive(false);
        }

        private void FixedUpdate()
        {
            // Khi da cham vat can -> KHONG the di chuyen duoc nua!
            if (isBlocked)
            {
                currentSpeed = 0f;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                if (engineAudio != null && engineAudio.isPlaying)
                    engineAudio.Pause();
                return;
            }

            float v = InputHelper.GetVerticalAxis();
            float h = InputHelper.GetHorizontalAxis();
            currentSpeed = v * baseSpeed;

            if (Mathf.Abs(h) > 0.05f)
            {
                float dirSign = v < -0.05f ? -1f : 1f;
                float yaw = h * turnSpeed * dirSign * Time.fixedDeltaTime;
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));
            }

            if (Mathf.Abs(v) > 0.05f)
            {
                Vector3 nextPos = _rb.position + transform.forward * (currentSpeed * Time.fixedDeltaTime);
                nextPos.x = Mathf.Clamp(nextPos.x, -roadLimitX, roadLimitX);
                nextPos.z = Mathf.Clamp(nextPos.z, -roadLimitZ, roadLimitZ);
                nextPos.y = _initialPosition.y;
                _rb.MovePosition(nextPos);
            }
            else
            {
                _rb.linearVelocity = Vector3.zero;
            }
        }

        private void Update()
        {
            if (isBlocked) return;

            if (wheels != null && Mathf.Abs(currentSpeed) > 0.05f)
            {
                float spin = currentSpeed * 90f * Time.deltaTime;
                foreach (Transform w in wheels)
                {
                    if (w != null) w.Rotate(Vector3.right, spin, Space.Self);
                }
            }

            if (engineAudio != null && engineAudio.clip != null)
            {
                if (Mathf.Abs(currentSpeed) > 0.1f)
                {
                    if (!engineAudio.isPlaying) engineAudio.Play();
                    engineAudio.pitch = 1.0f + Mathf.Abs(currentSpeed) * 0.03f;
                }
                else if (engineAudio.isPlaying)
                {
                    engineAudio.Pause();
                }
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (isBlocked) return;

            if (other.gameObject.name.StartsWith("Barrier") || other.gameObject.CompareTag("Obstacle"))
            {
                isBlocked = true;
                currentSpeed = 0f;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                if (smokeParticle != null)
                {
                    smokeParticle.SetActive(true);
                    var ps = smokeParticle.GetComponent<ParticleSystem>();
                    if (ps != null) ps.Play();
                }

                if (sfxAudio != null && crashClip != null)
                    sfxAudio.PlayOneShot(crashClip, 1f);

                if (engineAudio != null && engineAudio.isPlaying)
                    engineAudio.Pause();

                Debug.Log("[BT_VR - Tuan 6] Xe da cham vat can -> Dung han, khong the di chuyen!");
            }
        }

        public void ResetCar()
        {
            isBlocked = false;
            currentSpeed = 0f;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = _initialPosition;
            _rb.rotation = _initialRotation;
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;

            if (smokeParticle != null)
            {
                var ps = smokeParticle.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                smokeParticle.SetActive(false);
            }
        }
    }
}
