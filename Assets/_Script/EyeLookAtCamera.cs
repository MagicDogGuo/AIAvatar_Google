using UnityEngine;

namespace GeminiManager
{
    /// <summary>
    /// 控制左右眼看向攝影機的系統
    /// 支援角度限制，避免眼睛轉動過度不自然
    /// </summary>
    public class EyeLookAtCamera : MonoBehaviour
    {
        [Header("Eye Transforms")]
        [Tooltip("右眼的 Transform（會看向攝影機）")]
        [SerializeField] private Transform rightEye;
        
        [Tooltip("左眼的 Transform（會看向攝影機）")]
        [SerializeField] private Transform leftEye;

        [Header("Target Camera")]
        [Tooltip("要看向的攝影機。留空則自動尋找 Main Camera")]
        [SerializeField] private Camera targetCamera;

        public enum FocusMode
        {
            /// <summary>直接看鏡頭位置（鏡頭很近時可能會略鬥雞眼）</summary>
            CameraPosition,
            /// <summary>看鏡頭前方一個「聚焦點」，更像在聚焦看鏡頭</summary>
            CameraForwardPoint,
            /// <summary>看自訂目標點（例如放一個空物件在鏡頭前）</summary>
            CustomTarget
        }

        [Header("Focus / Convergence")]
        [Tooltip("聚焦模式")]
        [SerializeField] private FocusMode focusMode = FocusMode.CameraForwardPoint;

        [Tooltip("CustomTarget 模式用：聚焦目標 Transform")]
        [SerializeField] private Transform customTarget;

        [Tooltip("CameraForwardPoint 模式用：聚焦點距離鏡頭多遠（公尺）。建議 0.3~2。")]
        [SerializeField] private float focusDistance = 0.8f;

        [Tooltip("聚焦強度：0=維持基準眼睛方向，1=完全看向聚焦點")]
        [Range(0f, 1f)]
        [SerializeField] private float focusWeight = 1f;

        [Tooltip("微小視線抖動（更像活的在聚焦）。0=關閉")]
        [SerializeField] private float microSaccadeDegrees = 0.25f;

        [Tooltip("微小視線抖動更新頻率（秒）。例如 0.15~0.35")]
        [SerializeField] private float microSaccadeInterval = 0.2f;

        [Header("Settings")]
        [Tooltip("平滑度（0=立即，1=完全平滑）。建議 0.1~0.3")]
        [SerializeField] private float smoothness = 0.2f;
        
        [Tooltip("更新頻率（每秒更新次數）。0 = 每幀更新")]
        [SerializeField] private float updateRate = 0f;

        public enum LocalAxis
        {
            X, NegX,
            Y, NegY,
            Z, NegZ
        }

        [Header("Axis / Debug")]
        [Tooltip("眼球「面向前方」使用的 local 軸（不同模型可能不是 Z+）。")]
        [SerializeField] private LocalAxis forwardAxis = LocalAxis.Z;

        [Tooltip("用於 LookRotation 的 Up 參考（留空則用 Vector3.up）。通常可填 Head/Neck。")]
        [SerializeField] private Transform upReference;

        [Tooltip("每幀畫出眼睛射向攝影機的 Debug Ray")]
        [SerializeField] private bool debugDrawRays = true;

        [Tooltip("Debug Ray 長度。0 = 用眼睛到攝影機的距離")]
        [SerializeField] private float debugRayLength = 0f;

        [Tooltip("每幀印出目前計算到的角度（會很吵）")]
        [SerializeField] private bool debugLogAngles = false;

        [SerializeField] private Vector3 debugRightEyeRayDir;
        [SerializeField] private Vector3 debugLeftEyeRayDir;

        private Quaternion _rightEyeBaseLocalRot;
        private Quaternion _leftEyeBaseLocalRot;
        private float lastUpdateTime;
        private float updateInterval;
        private float _nextMicroSaccadeTime;
        private Vector2 _microYawPitch; // degrees: x=yaw, y=pitch

        void Start()
        {
            // 自動尋找 Main Camera
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    Debug.LogWarning("EyeLookAtCamera: 找不到 Main Camera，請在 Inspector 中指定 targetCamera。");
                }
            }

            // 儲存初始旋轉（基準點）
            if (rightEye != null)
            {
                _rightEyeBaseLocalRot = rightEye.localRotation;
            }
            else
            {
                Debug.LogWarning("EyeLookAtCamera: rightEye Transform 未設定！");
            }

            if (leftEye != null)
            {
                _leftEyeBaseLocalRot = leftEye.localRotation;
            }
            else
            {
                Debug.LogWarning("EyeLookAtCamera: leftEye Transform 未設定！");
            }

            // 計算更新間隔
            updateInterval = updateRate > 0 ? 1f / updateRate : 0f;
        }

        void LateUpdate()
        {
            // 檢查更新頻率
            if (updateRate > 0)
            {
                if (Time.time - lastUpdateTime < updateInterval)
                    return;
                lastUpdateTime = Time.time;
            }

            if (targetCamera == null)
                return;

            // 更新微小視線抖動（讓聚焦更自然）
            if (microSaccadeDegrees > 0f && Time.time >= _nextMicroSaccadeTime)
            {
                _microYawPitch = Random.insideUnitCircle * microSaccadeDegrees;
                _nextMicroSaccadeTime = Time.time + Mathf.Max(0.02f, microSaccadeInterval);
            }

            // 更新右眼
            if (rightEye != null)
            {
                debugRightEyeRayDir = UpdateEyeRotationFollowRay(rightEye, _rightEyeBaseLocalRot, Color.red);
            }

            // 更新左眼
            if (leftEye != null)
            {
                debugLeftEyeRayDir = UpdateEyeRotationFollowRay(leftEye, _leftEyeBaseLocalRot, Color.blue);
            }
        }

        /// <summary>
        /// 取消 XY 極限：讓眼睛跟著 (eye -> focusPoint) 的 Ray 方向
        /// </summary>
        private Vector3 UpdateEyeRotationFollowRay(Transform eyeTransform, Quaternion baseLocalRotation, Color rayColor)
        {
            var focusPoint = GetFocusPointWorld();

            // Ray: 眼睛 -> 聚焦點
            var dirWorld = focusPoint - eyeTransform.position;
            if (dirWorld.sqrMagnitude < 0.000001f)
                return Vector3.zero;

            var rayDir = ApplyMicroSaccade(dirWorld.normalized);

            if (debugDrawRays)
            {
                var len = debugRayLength > 0f ? debugRayLength : dirWorld.magnitude;
                Debug.DrawRay(eyeTransform.position, rayDir * len, rayColor);
            }

            var up = upReference != null ? upReference.up : Vector3.up;
            var axisVec = AxisToVector(forwardAxis);

            // desiredWorld: 讓「forwardAxis」指向 rayDir
            var lookZ = Quaternion.LookRotation(rayDir, up);
            var desiredWorld = lookZ * Quaternion.FromToRotation(axisVec, Vector3.forward);

            // 保留基準姿勢的相對扭轉（避免直接覆蓋造成奇怪 roll）
            var parentRot = eyeTransform.parent != null ? eyeTransform.parent.rotation : Quaternion.identity;
            var baseWorld = parentRot * baseLocalRotation;
            var baseForwardWorld = baseWorld * axisVec;
            var baseLook = Quaternion.LookRotation(baseForwardWorld, up) * Quaternion.FromToRotation(axisVec, Vector3.forward);
            var targetWorld = desiredWorld * Quaternion.Inverse(baseLook) * baseWorld;
            // 聚焦權重：0=維持基準、1=完全看向聚焦點
            var blendedWorld = focusWeight >= 1f ? targetWorld : Quaternion.Slerp(baseWorld, targetWorld, Mathf.Clamp01(focusWeight));
            var targetLocal = Quaternion.Inverse(parentRot) * blendedWorld;

            var t = smoothness <= 0f ? 1f : Mathf.Clamp01(smoothness);
            eyeTransform.localRotation = Quaternion.Slerp(eyeTransform.localRotation, targetLocal, t);

            if (debugLogAngles)
                Debug.Log($"EyeLookAtCamera [{eyeTransform.name}] focusPoint={focusPoint} rayDir={rayDir}");

            return rayDir;
        }

        /// <summary>
        /// 在編輯器中繪製 Gizmos（方便除錯）
        /// </summary>
        void OnDrawGizmosSelected()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
                return;
            // 改用 Debug.DrawRay（Game 視窗也看得到），這裡保留空的避免干擾
        }

        /// <summary>
        /// 重置眼睛到基準位置
        /// </summary>
        public void ResetEyes()
        {
            if (rightEye != null)
            {
                rightEye.localRotation = _rightEyeBaseLocalRot;
            }
            if (leftEye != null)
            {
                leftEye.localRotation = _leftEyeBaseLocalRot;
            }
        }

        private static Vector3 AxisToVector(LocalAxis axis)
        {
            switch (axis)
            {
                case LocalAxis.X: return Vector3.right;
                case LocalAxis.NegX: return Vector3.left;
                case LocalAxis.Y: return Vector3.up;
                case LocalAxis.NegY: return Vector3.down;
                case LocalAxis.Z: return Vector3.forward;
                case LocalAxis.NegZ: return Vector3.back;
                default: return Vector3.forward;
            }
        }

        private Vector3 GetFocusPointWorld()
        {
            switch (focusMode)
            {
                case FocusMode.CameraPosition:
                    return targetCamera.transform.position;
                case FocusMode.CustomTarget:
                    if (customTarget != null) return customTarget.position;
                    // fallback
                    return targetCamera.transform.position + targetCamera.transform.forward * Mathf.Max(0.01f, focusDistance);
                case FocusMode.CameraForwardPoint:
                default:
                    return targetCamera.transform.position + targetCamera.transform.forward * Mathf.Max(0.01f, focusDistance);
            }
        }

        private Vector3 ApplyMicroSaccade(Vector3 rayDir)
        {
            if (microSaccadeDegrees <= 0f) return rayDir;
            if (_microYawPitch == Vector2.zero) return rayDir;

            // 以鏡頭的 up / right 當基準做微小角度偏移（更像在盯鏡頭時的微動）
            var up = targetCamera != null ? targetCamera.transform.up : Vector3.up;
            var right = targetCamera != null ? targetCamera.transform.right : Vector3.right;

            var q = Quaternion.AngleAxis(_microYawPitch.x, up) * Quaternion.AngleAxis(_microYawPitch.y, right);
            return (q * rayDir).normalized;
        }
    }
}
