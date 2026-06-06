using System;
using UnityEngine;

namespace LeiTing.Enemy.Movement
{
    public enum OrbitMovementPhase
    {
        Enter,
        Orbit,
        Exit,
        Completed
    }

    public enum OrbitExitDirection
    {
        Down,
        Left,
        Right,
        Up,
        DownLeft,
        DownRight,
        Tangent
    }

    [Serializable]
    public class OrbitMovementConfig
    {
        [Header("Orbit")]
        // 盘旋中心点 X 坐标。
        [Tooltip("盘旋中心点 X 坐标。")]
        public float centerX;

        // 盘旋中心点 Y 坐标。
        [Tooltip("盘旋中心点 Y 坐标。")]
        public float centerY;

        // 椭圆轨迹的水平半径；与 radiusY 相等时为圆形轨迹。
        [Tooltip("椭圆轨迹的水平半径；与 radiusY 相等时为圆形轨迹。")]
        public float radiusX = 1.2f;

        // 椭圆轨迹的垂直半径；与 radiusX 相等时为圆形轨迹。
        [Tooltip("椭圆轨迹的垂直半径；与 radiusX 相等时为圆形轨迹。")]
        public float radiusY = 0.75f;

        // 角速度，单位为度/秒。
        [Tooltip("角速度，单位为度/秒。")]
        public float angularSpeed = 120f;

        // 是否顺时针盘旋；Unity 2D 坐标中正角速度为逆时针。
        [Tooltip("是否顺时针盘旋；Unity 2D 坐标中正角速度为逆时针。")]
        public bool clockwise;

        // 进入盘旋阶段时的起始角度，单位为度。
        [Tooltip("进入盘旋阶段时的起始角度，单位为度。")]
        public float startAngle = 90f;

        // 盘旋持续时间，单位秒；小于等于 0 时可由 loopCount 控制。
        [Tooltip("盘旋持续时间，单位秒；小于等于 0 时可由 loopCount 控制。")]
        public float orbitDuration = 3f;

        // 盘旋圈数；大于 0 时会在达到圈数后进入离场阶段。
        [Tooltip("盘旋圈数；大于 0 时会在达到圈数后进入离场阶段。")]
        public float loopCount;

        // 盘旋中心点每秒向下漂移的速度，用于纵向卷轴关卡；正数向下。
        [Tooltip("盘旋中心点每秒向下漂移的速度，用于纵向卷轴关卡；正数向下。")]
        public float centerMoveSpeedY;

        [Header("Enter")]
        // 入场插值时长，单位秒；小于等于 0 时使用 enterSpeed 或敌机移动速度。
        [Tooltip("入场插值时长，单位秒；小于等于 0 时使用 enterSpeed 或敌机移动速度。")]
        public float enterDuration;

        // 入场移动速度；小于等于 0 时使用敌机配置里的 moveSpeed。
        [Tooltip("入场移动速度；小于等于 0 时使用敌机配置里的 moveSpeed。")]
        public float enterSpeed;

        // 入场阶段是否使用 easeOut 插值。
        [Tooltip("入场阶段是否使用 easeOut 插值。")]
        public bool easeOutEnter = true;

        [Header("Exit")]
        // 盘旋结束后的离场方向。
        [Tooltip("盘旋结束后的离场方向。")]
        public OrbitExitDirection exitDirection = OrbitExitDirection.Down;

        // 离场速度；小于等于 0 时使用敌机配置里的 moveSpeed。
        [Tooltip("离场速度；小于等于 0 时使用敌机配置里的 moveSpeed。")]
        public float exitSpeed;

        // 飞出屏幕后是否销毁对象；对象池项目可关闭并监听 OnCompleted 回收。
        [Tooltip("飞出屏幕后是否销毁对象；对象池项目可关闭并监听 OnCompleted 回收。")]
        public bool destroyOnExitComplete = true;

        // 离开相机边界多少世界单位后判定离场完成。
        [Tooltip("离开相机边界多少世界单位后判定离场完成。")]
        public float exitDespawnPadding = 1.2f;

        [Header("Rotation")]
        // 是否根据上一帧到当前帧的移动方向旋转敌机。
        [Tooltip("是否根据上一帧到当前帧的移动方向旋转敌机。")]
        public bool rotateToPath;

        // 朝向修正角度；若飞机贴图默认机头朝上，通常设置为 -90。
        [Tooltip("朝向修正角度；若飞机贴图默认机头朝上，通常设置为 -90。")]
        public float rotationOffset = -90f;

        public OrbitMovementConfig Clone()
        {
            return (OrbitMovementConfig)MemberwiseClone();
        }
    }

    [DisallowMultipleComponent]
    public class OrbitMovement : MonoBehaviour
    {
        private const float MinRadius = 0.01f;
        private const float MinSpeed = 0.01f;

        [SerializeField] private OrbitMovementConfig config = new OrbitMovementConfig();
        [SerializeField] private bool autoUpdate = true;

        private Vector2 enterStartPosition;
        private Vector2 orbitCenterStart;
        private Vector2 currentExitDirection = Vector2.down;
        private float fallbackMoveSpeed = 2f;
        private float enterElapsed;
        private float orbitElapsed;
        private float exitElapsed;
        private bool isInitialized;

        public event Action<OrbitMovement> OnCompleted;

        public OrbitMovementPhase Phase { get; private set; } = OrbitMovementPhase.Completed;
        public bool IsActive => isInitialized && Phase != OrbitMovementPhase.Completed;

        public bool AutoUpdate
        {
            get => autoUpdate;
            set => autoUpdate = value;
        }

        public OrbitMovementConfig Config => config;

        public void Initialize(OrbitMovementConfig movementConfig, float fallbackSpeed)
        {
            var source = movementConfig != null ? movementConfig.Clone() : new OrbitMovementConfig();
            Initialize(source, transform.position, fallbackSpeed);
        }

        public void Initialize(OrbitMovementConfig movementConfig, Vector2 startPosition, float fallbackSpeed)
        {
            config = movementConfig != null ? movementConfig.Clone() : new OrbitMovementConfig();
            fallbackMoveSpeed = Mathf.Max(MinSpeed, fallbackSpeed);
            enterStartPosition = startPosition;
            orbitCenterStart = new Vector2(config.centerX, config.centerY);
            enterElapsed = 0f;
            orbitElapsed = 0f;
            exitElapsed = 0f;
            isInitialized = true;
            Phase = OrbitMovementPhase.Enter;
            transform.position = startPosition;

            if (((Vector2)transform.position - GetOrbitPosition(0f)).sqrMagnitude <= 0.0001f)
            {
                BeginOrbit();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive || deltaTime <= 0f)
            {
                return;
            }

            var previousPosition = transform.position;

            switch (Phase)
            {
                case OrbitMovementPhase.Enter:
                    UpdateEnter(deltaTime);
                    break;
                case OrbitMovementPhase.Orbit:
                    UpdateOrbit(deltaTime);
                    break;
                case OrbitMovementPhase.Exit:
                    UpdateExit(deltaTime);
                    break;
            }

            ApplyRotation(previousPosition, transform.position);
        }

        private void Update()
        {
            if (autoUpdate)
            {
                Tick(Time.deltaTime);
            }
        }

        private void UpdateEnter(float deltaTime)
        {
            var targetPosition = GetOrbitPosition(0f);
            var enterDuration = config.enterDuration;

            if (enterDuration <= 0f)
            {
                var enterSpeed = config.enterSpeed > 0f ? config.enterSpeed : fallbackMoveSpeed;
                enterDuration = Vector2.Distance(enterStartPosition, targetPosition) / Mathf.Max(MinSpeed, enterSpeed);
            }

            if (enterDuration <= 0f)
            {
                transform.position = targetPosition;
                BeginOrbit();
                return;
            }

            enterElapsed += deltaTime;
            var t = Mathf.Clamp01(enterElapsed / enterDuration);
            if (config.easeOutEnter)
            {
                t = 1f - (1f - t) * (1f - t);
            }

            transform.position = Vector2.LerpUnclamped(enterStartPosition, targetPosition, t);

            if (enterElapsed >= enterDuration)
            {
                transform.position = targetPosition;
                BeginOrbit();
            }
        }

        private void BeginOrbit()
        {
            orbitElapsed = 0f;
            Phase = OrbitMovementPhase.Orbit;
        }

        private void UpdateOrbit(float deltaTime)
        {
            orbitElapsed += deltaTime;
            transform.position = GetOrbitPosition(orbitElapsed);

            var reachedDuration = config.orbitDuration > 0f && orbitElapsed >= config.orbitDuration;
            var reachedLoops = config.loopCount > 0f
                && Mathf.Abs(GetAngularSpeed()) * orbitElapsed >= config.loopCount * 360f;

            if (reachedDuration || reachedLoops)
            {
                BeginExit();
            }
        }

        private void BeginExit()
        {
            currentExitDirection = ResolveExitDirection();
            exitElapsed = 0f;
            Phase = OrbitMovementPhase.Exit;
        }

        private void UpdateExit(float deltaTime)
        {
            exitElapsed += deltaTime;
            var speed = config.exitSpeed > 0f ? config.exitSpeed : fallbackMoveSpeed;
            transform.position += (Vector3)(currentExitDirection * speed * deltaTime);

            if (IsOutsideDespawnBounds(transform.position))
            {
                Complete();
            }
        }

        private Vector2 GetOrbitPosition(float elapsedTime)
        {
            var theta = GetAngleAt(elapsedTime) * Mathf.Deg2Rad;
            var center = orbitCenterStart + Vector2.down * config.centerMoveSpeedY * elapsedTime;
            var radiusX = Mathf.Max(MinRadius, config.radiusX);
            var radiusY = Mathf.Max(MinRadius, config.radiusY);

            return new Vector2(
                center.x + Mathf.Cos(theta) * radiusX,
                center.y + Mathf.Sin(theta) * radiusY);
        }

        private float GetAngleAt(float elapsedTime)
        {
            return config.startAngle + GetAngularSpeed() * elapsedTime;
        }

        private float GetAngularSpeed()
        {
            var direction = config.clockwise ? -1f : 1f;
            return direction * Mathf.Max(MinSpeed, config.angularSpeed);
        }

        private Vector2 ResolveExitDirection()
        {
            switch (config.exitDirection)
            {
                case OrbitExitDirection.Left:
                    return Vector2.left;
                case OrbitExitDirection.Right:
                    return Vector2.right;
                case OrbitExitDirection.Up:
                    return Vector2.up;
                case OrbitExitDirection.DownLeft:
                    return new Vector2(-1f, -1f).normalized;
                case OrbitExitDirection.DownRight:
                    return new Vector2(1f, -1f).normalized;
                case OrbitExitDirection.Tangent:
                    return ResolveTangentDirection();
                case OrbitExitDirection.Down:
                default:
                    return Vector2.down;
            }
        }

        private Vector2 ResolveTangentDirection()
        {
            var theta = GetAngleAt(orbitElapsed) * Mathf.Deg2Rad;
            var angularVelocity = GetAngularSpeed() * Mathf.Deg2Rad;
            var tangentVelocity = new Vector2(
                -Mathf.Sin(theta) * Mathf.Max(MinRadius, config.radiusX),
                Mathf.Cos(theta) * Mathf.Max(MinRadius, config.radiusY)) * angularVelocity;
            tangentVelocity += Vector2.down * config.centerMoveSpeedY;

            return tangentVelocity.sqrMagnitude > 0.0001f ? tangentVelocity.normalized : Vector2.down;
        }

        private void ApplyRotation(Vector2 previousPosition, Vector2 currentPosition)
        {
            if (!config.rotateToPath)
            {
                return;
            }

            var delta = currentPosition - previousPosition;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + config.rotationOffset);
        }

        private bool IsOutsideDespawnBounds(Vector2 position)
        {
            var padding = Mathf.Max(0f, config.exitDespawnPadding);
            var camera = Camera.main;

            if (camera != null && camera.orthographic)
            {
                var halfHeight = camera.orthographicSize;
                var halfWidth = halfHeight * camera.aspect;
                var center = camera.transform.position;
                return position.x < center.x - halfWidth - padding
                    || position.x > center.x + halfWidth + padding
                    || position.y < center.y - halfHeight - padding
                    || position.y > center.y + halfHeight + padding;
            }

            return position.x < -8f - padding
                || position.x > 8f + padding
                || position.y < -6.5f - padding
                || position.y > 6.5f + padding;
        }

        private void Complete()
        {
            if (Phase == OrbitMovementPhase.Completed)
            {
                return;
            }

            Phase = OrbitMovementPhase.Completed;
            isInitialized = false;
            OnCompleted?.Invoke(this);

            if (config.destroyOnExitComplete)
            {
                Destroy(gameObject);
            }
        }
    }
}
