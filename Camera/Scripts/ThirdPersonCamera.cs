using System;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace MK.ThirdPerson
{
    [System.Serializable]
    public struct MinMax
    {
        public float min;
        public float max;

        public MinMax(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
    
    /// <summary>
    /// A third-person camera controller.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Transform pitcher;
        public Transform zoomerParent;
        public Transform zoomer;
        
        [Header("Settings")]
        [SerializeField] float zoomValue = 0.0f;
        public float zoomSpeed = 10f;
        public float rotationSpeed = 10f;
        public float rotationLerpSpeed = 10f;
        public float positionLerpSpeed = 10f;

        [Header("Limit")] 
        public bool limitPitchRotation = true;
        public MinMax minMaxPitchAngle = new MinMax(-20f, 30f);
        public MinMax minMaxZoomLimit = new MinMax(-20, 10);
        
        [System.NonSerialized] public Vector3 offset;
        [System.NonSerialized] public Camera mainCamera;

        private Transform cameraTarget;
        private Quaternion targetMainRotation;
        private Quaternion targetPitchRotation;

        private float deltaPitchAngle;

        /// <summary>
        /// Sets the zoom value and clamps it to the defined zoom limit.
        /// </summary>
        /// <param name="value">The new zoom value.</param>
        public float ZoomValue
        {
            get { return zoomValue; }
            set { zoomValue = Mathf.Clamp(value, minMaxZoomLimit.min, minMaxZoomLimit.max); }
        }

        private void Awake()
        {
            mainCamera = GetComponentInChildren<Camera>();

            targetMainRotation = Quaternion.identity;
            targetPitchRotation = Quaternion.identity;
        }

        private void Start()
        {
            offset = transform.position - target.position;
            cameraTarget = new GameObject("CameraTarget").transform;
        }

        private void LateUpdate()
        {
            // Position
            cameraTarget.position = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, cameraTarget.position, Time.deltaTime * positionLerpSpeed);

            // Rotation
            transform.rotation = Quaternion.Lerp(transform.rotation, targetMainRotation, Time.deltaTime * rotationLerpSpeed);
            pitcher.localRotation = Quaternion.Lerp(pitcher.localRotation, targetPitchRotation, Time.deltaTime * rotationLerpSpeed);
            
            // Zoom
            zoomer.transform.localPosition = new Vector3
            (
                0,
                0,
                Mathf.Lerp(zoomer.transform.localPosition.z, zoomValue, Time.deltaTime * zoomSpeed)
            );
        }

        /// <summary>
        /// Rotates the camera horizontally with the defined rotationSpeed based on <param name="value">value</param>.
        /// <para>To rotate vertically use <see cref="RotateVertical"></see>.</para>
        /// </summary>
        /// <param name="value">An input value between -1 and 1.</param>
        public void RotateHorizontal(float value)
        {
            targetMainRotation *= Quaternion.AngleAxis(value * rotationSpeed * Time.deltaTime, Vector3.up);
        }

        /// <summary>
        /// Rotates the camera vertically with the defined rotationSpeed based on <param name="value">value</param>.
        /// <para>To rotate horizontally use <see cref="RotateHorizontal"></see>.</para>
        /// </summary>
        /// <param name="value">An input value between -1 and 1.</param>
        public void RotateVertical(float value)
        {
            float angle = value * rotationSpeed * Time.deltaTime;
            float newDeltaAngle = deltaPitchAngle + angle;

            if (limitPitchRotation && (newDeltaAngle > minMaxPitchAngle.min && newDeltaAngle < minMaxPitchAngle.max))
            {
                deltaPitchAngle = newDeltaAngle;
                targetPitchRotation *= Quaternion.AngleAxis(angle, Vector3.right);
            }
        }
    }
}
