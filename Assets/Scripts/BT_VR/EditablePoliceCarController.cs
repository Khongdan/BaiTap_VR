using System.Collections.Generic;
using UnityEngine;

namespace BT_VR
{
    /// <summary>
    /// Xe o to canh sat chay xung quanh ngoi nha (Tuan 4 & Tuan 5):
    /// - Che do PureAnimation: chay Animation thuan (co the sua keyframe trong cua so Animation).
    /// - Che do EditableWaypoints: chinh sua truc tiep lo trinh quanh nha (danh sach toa do customWaypoints),
    ///   toc do, goc re, den pha, den nhay canh sat xanh-do va am thanh coi hu trong Inspector.
    /// </summary>
    public class EditablePoliceCarController : MonoBehaviour
    {
        public enum MovementMode
        {
            EditableWaypoints,
            PureAnimation
        }

        [Header("Che do di chuyen (sua truc tiep trong Inspector)")]
        public MovementMode movementMode = MovementMode.EditableWaypoints;

        [Header("Lo trinh quanh nha (co the chinh sua toa do)")]
        public List<Vector3> customWaypoints = new List<Vector3>
        {
            new Vector3(-9.5f, 0.04f, -8.5f),
            new Vector3(-9.5f, 0.04f,  8.5f),
            new Vector3( 9.5f, 0.04f,  8.5f),
            new Vector3( 9.5f, 0.04f, -8.5f)
        };
        [Range(1f, 25f)] public float moveSpeed = 7.5f;
        [Range(1f, 15f)] public float turnSpeed = 6f;
        public float waypointReachDistance = 0.6f;

        [Header("Hieu ung den & Coi hu (Tuan 5)")]
        public Animator carAnimator;
        public AudioSource sirenAudio;
        public Light redSirenLight;
        public Light blueSirenLight;
        public Light leftHeadlight;
        public Light rightHeadlight;
        public GameObject haloEffectObject;

        public bool enableSirenAudio = true;
        public bool enableSirenLights = true;
        public bool enableHeadlights = true;
        [Range(1f, 20f)] public float sirenBlinkSpeed = 8f;

        [Header("Banh xe")]
        public Transform[] wheels;

        private int _currentWaypointIndex;
        private Vector3 _lastPosition;

        private void Start()
        {
            if (carAnimator == null) carAnimator = GetComponent<Animator>();
            if (sirenAudio == null) sirenAudio = GetComponent<AudioSource>();
            _lastPosition = transform.position;
            ApplyModeAndEffects();
        }

        private void OnValidate()
        {
            ApplyLightAndAudioStates();
        }

        private void Update()
        {
            ApplyModeAndEffects();

            if (movementMode == MovementMode.EditableWaypoints)
                UpdateWaypointMovement();

            UpdateSirenBlink();
            UpdateWheelSpin();
            _lastPosition = transform.position;
        }

        public void ToggleMode()
        {
            movementMode = movementMode == MovementMode.EditableWaypoints
                ? MovementMode.PureAnimation
                : MovementMode.EditableWaypoints;
            ApplyModeAndEffects();
        }

        public void ToggleLights()
        {
            enableHeadlights = !enableHeadlights;
            enableSirenLights = !enableSirenLights;
            ApplyLightAndAudioStates();
        }

        public void ToggleSirenAudio()
        {
            enableSirenAudio = !enableSirenAudio;
            ApplyLightAndAudioStates();
        }

        private void ApplyModeAndEffects()
        {
            if (carAnimator != null)
            {
                bool useAnim = movementMode == MovementMode.PureAnimation;
                if (carAnimator.enabled != useAnim)
                    carAnimator.enabled = useAnim;
            }
            ApplyLightAndAudioStates();
        }

        private void ApplyLightAndAudioStates()
        {
            if (leftHeadlight != null) leftHeadlight.enabled = enableHeadlights;
            if (rightHeadlight != null) rightHeadlight.enabled = enableHeadlights;

            if (!enableSirenLights)
            {
                if (redSirenLight != null) redSirenLight.enabled = false;
                if (blueSirenLight != null) blueSirenLight.enabled = false;
            }

            if (haloEffectObject != null)
                haloEffectObject.SetActive(enableSirenLights);

            if (sirenAudio != null && Application.isPlaying)
            {
                if (enableSirenAudio && !sirenAudio.isPlaying && sirenAudio.clip != null)
                    sirenAudio.Play();
                else if (!enableSirenAudio && sirenAudio.isPlaying)
                    sirenAudio.Pause();
            }
        }

        private void UpdateWaypointMovement()
        {
            if (customWaypoints == null || customWaypoints.Count == 0)
                return;

            Vector3 target = customWaypoints[_currentWaypointIndex];
            target.y = transform.position.y;
            Vector3 direction = target - transform.position;

            if (direction.magnitude <= waypointReachDistance)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % customWaypoints.Count;
                return;
            }

            Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        }

        private void UpdateSirenBlink()
        {
            if (!enableSirenLights) return;
            bool phase = Mathf.Sin(Time.time * sirenBlinkSpeed * Mathf.PI) >= 0f;
            if (redSirenLight != null) redSirenLight.enabled = phase;
            if (blueSirenLight != null) blueSirenLight.enabled = !phase;
        }

        private void UpdateWheelSpin()
        {
            if (wheels == null || wheels.Length == 0) return;
            float moved = Vector3.Distance(transform.position, _lastPosition);
            float spinDegrees = moved * 180f;
            foreach (Transform w in wheels)
            {
                if (w != null) w.Rotate(Vector3.right, spinDegrees, Space.Self);
            }
        }

        private void OnDrawGizmos()
        {
            if (customWaypoints == null || customWaypoints.Count < 2) return;
            Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.9f);
            for (int i = 0; i < customWaypoints.Count; i++)
            {
                Vector3 a = customWaypoints[i] + Vector3.up * 0.35f;
                Vector3 b = customWaypoints[(i + 1) % customWaypoints.Count] + Vector3.up * 0.35f;
                Gizmos.DrawSphere(a, 0.35f);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
