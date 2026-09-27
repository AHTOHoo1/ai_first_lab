using UnityEngine;

public class EnemyTeleportAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float pointReachedDistance = 0.2f;

    [Header("Idle")]
    [SerializeField] private float idleTime = 2f;

    [Header("Normal Vision")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float visionAngle = 180f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Extra Scan")]
    [SerializeField] private float scanRange = 1f;
    [SerializeField] private float scanInterval = 0.5f;

    [Header("Combat")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 10;

    [Header("Search")]
    [SerializeField] private float searchTime = 3f;

    [Header("Teleport")]
    [SerializeField] private float teleportCooldown = 5f;
    [SerializeField] private float maxTeleportDistance = 4f;
    [SerializeField] private float teleportTriggerDistance = 3f;
    [SerializeField] private float teleportCheckRadius = 0.25f;
    [SerializeField] private float teleportStopDistance = 1.5f;

    private Rigidbody2D rb;

    private EnemyState currentState = EnemyState.Idle;

    private int currentPatrolPoint = 0;

    private float idleTimer;
    private float attackTimer;
    private float searchTimer;
    private float scanTimer;
    private float teleportTimer;

    private Transform player;

    private Vector2 lastKnownPlayerPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        idleTimer = idleTime;
        teleportTimer = teleportCooldown;
        scanTimer = scanInterval;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void Update()
    {
        scanTimer -= Time.deltaTime;
        teleportTimer -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Idle:
                Idle();
                break;

            case EnemyState.Patrol:
                Patrol();
                break;

            case EnemyState.Chase:
                Chase();
                break;

            case EnemyState.Attack:
                Attack();
                break;

            case EnemyState.Search:
                Search();
                break;

            case EnemyState.Teleport:
                Teleport();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (currentState == EnemyState.Patrol ||
            currentState == EnemyState.Chase ||
            currentState == EnemyState.Search)
        {
            RotateTowardsMovement();
        }
    }

    // =========================
    // IDLE
    // =========================

    private void Idle()
    {
        rb.linearVelocity = Vector2.zero;

        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    // =========================
    // PATROL
    // =========================

    private void Patrol()
    {
        if (PlayerDetected())
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Transform targetPoint =
            patrolPoints[currentPatrolPoint];

        Vector2 direction =
            ((Vector2)targetPoint.position - rb.position).normalized;

        rb.linearVelocity = direction * moveSpeed;

        float distance =
            Vector2.Distance(
                rb.position,
                targetPoint.position
            );

        if (distance <= pointReachedDistance)
        {
            currentPatrolPoint++;

            if (currentPatrolPoint >= patrolPoints.Length)
                currentPatrolPoint = 0;
        }
    }

    // =========================
    // PLAYER DETECTION
    // =========================

    private bool PlayerDetected()
    {
        // Обычное зрение
        if (CanSeePlayer())
            return true;

        // Дополнительное 360° сканирование
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;

            if (CanScanPlayer())
                return true;
        }

        return false;
    }

    // =========================
    // NORMAL VISION
    // =========================

    private bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector2 directionToPlayer =
            (player.position - transform.position).normalized;

        float distanceToPlayer =
            Vector2.Distance(
                transform.position,
                player.position
            );

        // Дальность зрения
        if (distanceToPlayer > detectionRange)
            return false;

        // Угол зрения 180°
        float angle =
            Vector2.Angle(
                transform.up,
                directionToPlayer
            );

        if (angle > visionAngle / 2f)
            return false;

        // Проверка стены
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            directionToPlayer,
            distanceToPlayer,
            obstacleLayer
        );

        if (hit.collider != null)
            return false;

        return true;
    }

    // =========================
    // 360° SCAN
    // =========================

    private bool CanScanPlayer()
    {
        if (player == null)
            return false;

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        // Радиус дополнительного сканирования
        if (distance > scanRange)
            return false;

        Vector2 direction =
            (player.position - transform.position).normalized;

        // Стена всё ещё блокирует сканирование
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            direction,
            distance,
            obstacleLayer
        );

        if (hit.collider != null)
            return false;

        // Нет проверки угла = 360°
        return true;
    }

    // =========================
    // CHASE
    // =========================

    private void Chase()
    {
        if (player == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        bool detected = PlayerDetected();

        if (detected)
        {
            lastKnownPlayerPosition = player.position;

            // Сначала проверяем атаку
            if (distance <= attackRange)
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            // Телепортируемся, если игрок достаточно далеко
            if (teleportTimer <= 0f &&
                distance >= teleportTriggerDistance)
            {
                ChangeState(EnemyState.Teleport);
                return;
            }

            Vector2 direction =
                ((Vector2)player.position - rb.position)
                .normalized;

            rb.linearVelocity =
                direction * moveSpeed;

            return;
        }

        // Игрок потерян
        rb.linearVelocity = Vector2.zero;

        ChangeState(EnemyState.Search);
    }

    // =========================
    // ATTACK
    // =========================

    private void Attack()
    {
        rb.linearVelocity = Vector2.zero;

        if (player == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        if (distance > attackRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        Vector2 direction =
            ((Vector2)player.position - rb.position)
            .normalized;

        float targetAngle =
            Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg - 90f;

        float newAngle =
            Mathf.MoveTowardsAngle(
                rb.rotation,
                targetAngle,
                rotationSpeed *
                Time.deltaTime *
                100f
            );

        rb.MoveRotation(newAngle);

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            PerformAttack();

            attackTimer = attackCooldown;
        }
    }

    private void PerformAttack()
    {
        Debug.Log(
            gameObject.name +
            " атакует игрока! Урон: " +
            attackDamage
        );
    }

    // =========================
    // SEARCH
    // =========================

    private void Search()
    {
        if (PlayerDetected())
        {
            lastKnownPlayerPosition =
                player.position;

            ChangeState(EnemyState.Chase);
            return;
        }

        searchTimer -= Time.deltaTime;

        Vector2 direction =
            (lastKnownPlayerPosition - rb.position)
            .normalized;

        float distance =
            Vector2.Distance(
                rb.position,
                lastKnownPlayerPosition
            );

        if (distance > 0.2f)
        {
            rb.linearVelocity =
                direction * moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (searchTimer <= 0f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    // =========================
    // TELEPORT
    // =========================

    private void Teleport()
    {
        rb.linearVelocity = Vector2.zero;

        if (player == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        Vector2 teleportPosition =
            FindTeleportPosition();

        if (teleportPosition != Vector2.zero)
        {
            rb.position = teleportPosition;

            teleportTimer =
                teleportCooldown;

            Debug.Log(
                gameObject.name +
                " → TELEPORT"
            );
        }

        ChangeState(EnemyState.Chase);
    }

    // =========================
    // FIND TELEPORT POSITION
    // =========================

    private Vector2 FindTeleportPosition()
    {
        if (player == null)
            return Vector2.zero;

        Vector2 directionToPlayer =
            ((Vector2)player.position - rb.position)
            .normalized;

        float currentDistance =
            Vector2.Distance(
                rb.position,
                player.position
            );

        // Телепортируемся именно В СТОРОНУ игрока
        float teleportDistance =
            Mathf.Min(
                maxTeleportDistance,
                currentDistance - teleportStopDistance
            );

        if (teleportDistance <= 0f)
            return Vector2.zero;

        Vector2 targetPosition =
            rb.position +
            directionToPlayer * teleportDistance;

        // Проверяем, не попали ли внутрь стены
        Collider2D obstacle =
            Physics2D.OverlapCircle(
                targetPosition,
                teleportCheckRadius,
                obstacleLayer
            );

        if (obstacle != null)
            return Vector2.zero;

        return targetPosition;
    }

    // =========================
    // ROTATION
    // =========================

    private void RotateTowardsMovement()
    {
        Vector2 velocity =
            rb.linearVelocity;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        float targetAngle =
            Mathf.Atan2(
                velocity.y,
                velocity.x
            ) * Mathf.Rad2Deg - 90f;

        float newAngle =
            Mathf.MoveTowardsAngle(
                rb.rotation,
                targetAngle,
                rotationSpeed *
                Time.fixedDeltaTime *
                100f
            );

        rb.MoveRotation(newAngle);
    }

    // =========================
    // STATE CHANGE
    // =========================

    private void ChangeState(
        EnemyState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        Debug.Log(
            gameObject.name +
            " → " +
            currentState
        );

        if (newState == EnemyState.Idle)
        {
            idleTimer = idleTime;
        }

        if (newState == EnemyState.Attack)
        {
            attackTimer = 0f;
        }

        if (newState == EnemyState.Search)
        {
            searchTimer = searchTime;
        }
    }
}
