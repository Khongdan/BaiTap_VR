using UnityEngine;

namespace BTTH_06
{
    /// <summary>
    /// Quay canh quat chinh va canh quat duoi cho Truc thang tuan tra trong Scene BTTH_06.
    /// </summary>
    public class HelicopterRotorSpinner : MonoBehaviour
    {
        public Transform mainRotor;
        public Transform tailRotor;
        public float mainRotorSpeed = 900f;
        public float tailRotorSpeed = 1400f;

        private void Update()
        {
            if (mainRotor != null)
                mainRotor.Rotate(Vector3.up, mainRotorSpeed * Time.deltaTime, Space.Self);

            if (tailRotor != null)
                tailRotor.Rotate(Vector3.right, tailRotorSpeed * Time.deltaTime, Space.Self);
        }
    }
}
