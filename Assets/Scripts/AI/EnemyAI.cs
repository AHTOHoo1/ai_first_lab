using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 10;

    [Header("Search")]
    [SerializeField] private float searchTime = 3f;

    private float attackTimer;
    private float searchTimer;
    private Vector2 lastKnownPlayerPosition;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float visionAngle = 180f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayer;

    private Transform player;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float pointReachedDistance = 0.2f;

    [Header("Idle")]
    [SerializeField] private float idleTime = 2f;

    private Rigidbody2D rb;

    private EnemyState currentState = EnemyState.Idle;

    private int currentPatrolPoint = 0;
    private float idleTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        idleTimer = idleTime;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void Update()
    {
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

    private void Idle()
    {
        rb.linearVelocity = Vector2.zero;

        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    private void Patrol()
    {
        if (CanSeePlayer())
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Transform targetPoint = patrolPoints[currentPatrolPoint];

        Vector2 direction =
            ((Vector2)targetPoint.position - rb.position).normalized;

        rb.linearVelocity = direction * moveSpeed;

        float distance =
            Vector2.Distance(rb.position, targetPoint.position);

        if (distance <= pointReachedDistance)
        {
            currentPatrolPoint++;

            if (currentPatrolPoint >= patrolPoints.Length)
                currentPatrolPoint = 0;
        }
    }

    private void RotateTowardsMovement()
    {
        Vector2 velocity = rb.linearVelocity;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        float targetAngle =
            Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;

        float newAngle = Mathf.MoveTowardsAngle(
            rb.rotation,
            targetAngle,
            rotationSpeed * Time.fixedDeltaTime * 100f
        );

        rb.MoveRotation(newAngle);
    }

    private void ChangeState(EnemyState newState)
    {
        currentState = newState;

        Debug.Log(gameObject.name + " → " + currentState);

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
            rb.linearVelocity = Vector2.zero;
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector2 directionToPlayer = (player.position - transform.position).normalized;

        float distanceToPlayer = Vector2.Distance(
            transform.position,
            player.position
        );

        // Проверяем расстояние
        if (distanceToPlayer > detectionRange)
            return false;

        // Проверяем угол обзора
        float angle = Vector2.Angle(transform.up, directionToPlayer);

        if (angle > visionAngle / 2f)
            return false;

        // Проверяем препятствие
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

    private void Chase()
    {
        if (player == null)
            return;

        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        // Игрок снова обнаружен
        if (CanSeePlayer())
        {
            lastKnownPlayerPosition = player.position;

        if (distance <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        Vector2 direction =
            ((Vector2)player.position - rb.position).normalized;

        rb.linearVelocity = direction * moveSpeed;

        return;
        }

        // Игрок потерян
        rb.linearVelocity = Vector2.zero;
        ChangeState(EnemyState.Search);
    }

    private void Attack()
    {
        rb.linearVelocity = Vector2.zero;

        if (player == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        // Игрок убежал
        if (distance > attackRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Поворачиваемся к игроку
        Vector2 direction =
            ((Vector2)player.position - rb.position).normalized;

        float targetAngle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        float newAngle = Mathf.MoveTowardsAngle(
            rb.rotation,
            targetAngle,
            rotationSpeed * Time.deltaTime * 100f
        );

        rb.MoveRotation(newAngle);

        // Таймер атаки
        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    private void PerformAttack()
    {
        Debug.Log(gameObject.name + " атакует игрока! Урон: " + attackDamage);

        // Позже сюда добавим реальное уменьшение HP игрока.
    }

    private void Search()
    {
        rb.linearVelocity = Vector2.zero;

        searchTimer -= Time.deltaTime;

        // Игрок снова найден
        if (CanSeePlayer())
        {
            lastKnownPlayerPosition = player.position;
            ChangeState(EnemyState.Chase);
            return;
        }

        // Идём к последней известной позиции
        Vector2 direction =
            (lastKnownPlayerPosition - rb.position).normalized;

        float distance =
            Vector2.Distance(rb.position, lastKnownPlayerPosition);

        if (distance > 0.2f)
        {
            rb.linearVelocity = direction * moveSpeed;
        }

        // Закончилось время поиска
        if (searchTimer <= 0f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

}
