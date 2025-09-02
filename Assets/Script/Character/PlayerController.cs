using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Contact Damage")]
    [Tooltip("장애물 또는 보스에 닿았을 때 플레이어가 받는 대미지(기본값, DamageSource가 없을 때만 사용)")]
    public float contactDamage = 1f;

    // 1. 피격 무적 처리 변수
    private bool isInvincible = false;
    public bool IsInvincible => isInvincible;
    private float invincibilityDuration = 1f;
    private float invincibilityTimer = 0f;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public int maxJumpCount = 2;
    private int jumpCount;
    [Header("Variable Jump")]
    public bool variableJump = true;
    [Range(0f, 1f)]
    public float jumpCutMultiplier = 0.5f;

    [Header("Sprint Settings")]
    public float sprintMultiplier = 1.5f;
    public float staminaDrainRate = 20f;
    public float staminaRegenRate = 2f;

    [Header("Roll & Dash")]
    public float rollThreshold = 0.2f;
    public float rollDistance = 3f;
    public float rollDuration = 0.3f;
    public int rollStaminaCost = 15;

    [Header("Roll Settings")]
    public LayerMask obstacleLayers;

    private bool isEvading = false;
    public bool IsEvading => isEvading;
    private bool shiftPressed = false;
    private float shiftPressTime = 0f;

    [Header("Weapon Mode")]
    public int currentWeaponIndex = 0; // 0=검, 1=총
    public GameObject[] weaponObjects;

    [Header("Sword (Visual Swing Only)")]
    public Transform sword;
    public KeyCode attackKey = KeyCode.X;
    public float swingAngle = 45f;
    public float swingDuration = 0.3f;

    [Header("Slash Spawn Settings (실제 무기)")]
    public GameObject slashPrefab;
    public Transform slashSpawnPoint;
    public float slashLifetime = 0.25f;
    public bool flipSlashOnLeft = true;

    [Header("Gun Attack")]
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public float bulletSpeed = 15f;

    [Header("Attack (Hold to Repeat)")]
    public bool enableHoldAttack = true;
    public float swordManualPadding = 0.35f;
    public float holdExtraDelay = 0.08f;
    public float gunAutoBaseCooldown = 0.16f;

    private Rigidbody2D rb;
    private float originalGravityScale;
    private Stamina staminaComponent;
    private bool isAttacking;
    private bool waitingReturn;
    private bool doReturn;

    private int facingDirection = 1;
    private bool backstepActive = false;
    private int backstepFacingDir = 1;
    private int moveInputDirection = 0;
    private KeyCode lastMoveKey;

    private float sprintDrainAccumulator = 0f;
    private float staminaRegenAccumulator = 0f;

    private Animator animator;
    private Health health;

    private float nextSwordAutoTime = 0f;
    private float nextGunAutoTime = 0f;

    public bool IsGrounded => jumpCount == 0;

    [Header("SFX")]
    public AudioClip sfxJumpGround;
    public AudioClip sfxJumpAir;
    public AudioClip sfxHit;
    public AudioClip sfxRoll;
    public AudioClip sfxDeath;
    public AudioClip sfxSword;
    public AudioClip sfxGun;
    private bool _deathSfxPlayed = false;

    void PlaySfx2D(AudioClip clip)
    {
        if (clip == null) return;
        var am = FindObjectOfType<AudioManager>();
        if (am != null) am.PlaySFX(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }

    void PlayJumpSfxByCount(int currentJumpCount)
    {
        PlaySfx2D(currentJumpCount == 0 ? sfxJumpGround : sfxJumpAir);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravityScale = rb.gravityScale;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        staminaComponent = GetComponent<Stamina>();
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();

        // 씬 생성 즉시: Pending의 ‘최근 무기’ 선적용 (UI 초기화보다 먼저)
        ApplyRecentWeaponFromPendingSave();
    }

    void Start()
    {
        // 선적용된 인덱스 기준 비주얼 갱신
        UpdateWeaponVisuals();

        // 1프레임 뒤 UI 하이라이트 동기화(다른 매니저 초기화보다 ‘마지막에’ 덮어쓰기)
        StartCoroutine(SyncWeaponUIEndOfFrame());
    }

    void ApplyRecentWeaponFromPendingSave()
    {
        var data = SaveLoadBuffer.Pending;
        if (data == null) return;
        if (data.recentWeaponIndex < 0) return;

        int max = (weaponObjects != null && weaponObjects.Length > 0) ? weaponObjects.Length - 1 : 0;
        currentWeaponIndex = Mathf.Clamp(data.recentWeaponIndex, 0, Mathf.Max(0, max));
    }

    IEnumerator SyncWeaponUIEndOfFrame()
    {
        yield return null; // 한 프레임 대기
        var wm = FindObjectOfType<WeaponManager>();
        if (wm != null)
            wm.UpdateAllSlots(true, currentWeaponIndex); // SFX 없이 하이라이트만
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (DialogueManager.DialogueOpen)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            animator.SetBool("isRunning", false);
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", false);
            return;
        }

        float moveInput = 0f;
        bool leftPressed  = Input.GetKey(KeyCode.LeftArrow);
        bool rightPressed = Input.GetKey(KeyCode.RightArrow);
        if (leftPressed)  moveInput -= 1f;
        if (rightPressed) moveInput += 1f;
        moveInputDirection = (int)Mathf.Sign(moveInput);

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            shiftPressed = true;
            shiftPressTime = Time.time;
        }
        if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
        {
            if (shiftPressed)
            {
                float held = Time.time - shiftPressTime;
                if (held < rollThreshold && InputLocker.CanDodge)
                    TryRoll();
            }
            shiftPressed = false;
        }

        UpdateInvincibility();
        UpdateFacing();
        HandleMove();
        HandleJump();
        HandleAttack();

        if (variableJump && Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                rb.linearVelocity.y * Mathf.Clamp01(jumpCutMultiplier)
            );
        }

        bool grounded = jumpCount == 0;
        bool running  = moveInputDirection != 0 && grounded && !isEvading;
        bool falling  = rb.linearVelocity.y < -0.1f;

        animator.SetBool("isRunning", running);
        animator.SetBool("isFalling", falling);
        if (grounded) animator.SetBool("isJumping", false);
    }

    void UpdateFacing()
    {
        bool leftPressed = Input.GetKey(KeyCode.LeftArrow);
        bool rightPressed = Input.GetKey(KeyCode.RightArrow);

        if (Input.GetKeyDown(KeyCode.LeftArrow))      lastMoveKey = KeyCode.LeftArrow;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) lastMoveKey = KeyCode.RightArrow;

        if (leftPressed && rightPressed)
        {
            if (!backstepActive)
            {
                backstepActive = true;
                backstepFacingDir = facingDirection;
            }
            moveInputDirection = (lastMoveKey == KeyCode.LeftArrow) ? -1 : 1;
        }
        else
        {
            backstepActive = false;
            if (leftPressed) moveInputDirection = -1;
            else if (rightPressed) moveInputDirection = 1;
            else moveInputDirection = 0;

            if (moveInputDirection != 0) facingDirection = moveInputDirection;
        }

        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (backstepActive ? backstepFacingDir : facingDirection);
        transform.localScale = s;
    }

    void HandleMove()
    {
        if (!InputLocker.CanMove || isEvading) return;

        bool holdsShift = Input.GetKey(KeyCode.LeftShift) && moveInputDirection != 0;
        bool canDash = holdsShift
                        && staminaComponent.CurrentSP > 0
                        && !staminaComponent.IsExhausted
                        && InputLocker.CanDash;

        float speed = moveSpeed * (canDash ? sprintMultiplier : 1f);
        rb.linearVelocity = new Vector2(moveInputDirection * speed, rb.linearVelocity.y);

        if (canDash)
        {
            sprintDrainAccumulator += staminaDrainRate * Time.deltaTime;
            int drain = Mathf.FloorToInt(sprintDrainAccumulator);
            if (drain > 0)
            {
                staminaComponent.UseSP(drain);
                sprintDrainAccumulator -= drain;
            }
        }
        else
        {
            sprintDrainAccumulator = 0f;
            staminaRegenAccumulator += staminaRegenRate * Time.deltaTime;
            int regen = Mathf.FloorToInt(staminaRegenAccumulator);
            if (regen > 0)
            {
                staminaComponent.RecoverSP(regen);
                staminaRegenAccumulator -= regen;
            }
        }
    }

    void HandleJump()
    {
        if (!InputLocker.CanJump || isEvading) return;

        if (Input.GetButtonDown("Jump") && jumpCount < maxJumpCount)
        {
            PlayJumpSfxByCount(jumpCount);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpCount++;
            animator.SetBool("isJumping", true);
        }
    }

    void OnTriggerEnter2D(Collider2D other) { TryApplyDamageFrom(other); }
    void OnCollisionEnter2D(Collision2D col)
    {
        TryApplyDamageFrom(col.collider);
        if (col.contacts.Length > 0 && col.contacts[0].normal.y > 0.5f && rb.linearVelocity.y <= 0f)
            jumpCount = 0;
    }
    void OnTriggerStay2D(Collider2D other) { TryApplyDamageFrom(other); }
    void OnCollisionStay2D(Collision2D col)
    {
        TryApplyDamageFrom(col.collider);
        if (col.contacts.Length > 0 && col.contacts[0].normal.y > 0.5f && rb.linearVelocity.y <= 0f)
            jumpCount = 0;
    }

    private void TryApplyDamageFrom(Collider2D other)
    {
        if (isEvading || isInvincible) return;

        float vertical = rb.linearVelocity.y;

        var ds = other.GetComponent<DamageSource>();
        if (ds != null)
        {
            if (ds.requireBossAttackState && !ds.IsValidNow()) return;

            PlaySfx2D(sfxHit);
            health.TakeDamage(ds.damage);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, vertical);
            EnterInvincibleState();

            if (health != null && health.IsDead && !_deathSfxPlayed)
            {
                _deathSfxPlayed = true;
                PlaySfx2D(sfxDeath);
            }
            return;
        }

        bool hitBoss = other.CompareTag("Boss") && IsBossAttacking(other);
        bool hitObstacle = other.CompareTag("Obstacle");
        if ((hitBoss || hitObstacle))
        {
            PlaySfx2D(sfxHit);
            health.TakeDamage(contactDamage);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, vertical);
            EnterInvincibleState();

            if (health != null && health.IsDead && !_deathSfxPlayed)
            {
                _deathSfxPlayed = true;
                PlaySfx2D(sfxDeath);
            }
        }
    }

    bool IsBossAttacking(Collider2D other)
    {
        var bossAnim = other.GetComponent<Animator>();
        if (bossAnim == null) bossAnim = other.GetComponentInParent<Animator>();
        if (bossAnim == null) return false;
        var state = bossAnim.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Attack1") || state.IsName("Attack2");
    }

    void TryRoll()
    {
        if (isEvading) return;
        if (jumpCount > 0) return;
        if (staminaComponent.UseSP(rollStaminaCost))
            StartCoroutine(RollCoroutine());
    }

    IEnumerator RollCoroutine()
    {
        isEvading = true;
        isInvincible = true;

        PlaySfx2D(sfxRoll);

        if (facingDirection > 0) animator.SetTrigger("Roll");
        else                      animator.SetTrigger("Roll2");

        Flip(facingDirection);

        float rollSpeed = rollDistance / rollDuration;
        rb.linearVelocity = new Vector2(facingDirection * rollSpeed, rb.linearVelocity.y);

        float timer = 0f;
        while (timer < rollDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        isEvading = false;
        isInvincible = false;

        if (spriteRenderer != null) spriteRenderer.color = originalColor;
    }

    private void EnterInvincibleState()
    {
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
    }

    void UpdateInvincibility()
    {
        if (!isInvincible) return;

        invincibilityTimer -= Time.deltaTime;
        if (invincibilityTimer <= 0f)
        {
            isInvincible = false;
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
        }
    }

    public void SetInvincible(bool value)
    {
        isInvincible = value;
        invincibilityTimer = value ? float.PositiveInfinity : 0f;
        if (spriteRenderer != null)
        {
            float alpha = value ? 0.5f : originalColor.a;
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
        }
    }

    void HandleAttack()
    {
        if (isEvading) return;
        if (!InputLocker.CanAttack) return;

        bool press = Input.GetKeyDown(attackKey);
        bool hold  = Input.GetKey(attackKey);

        if (currentWeaponIndex == 0)
        {
            if (press && Time.time >= nextSwordAutoTime)
            {
                float manualMin = Mathf.Max(0.01f, swingDuration + swordManualPadding);
                float interval  = manualMin + Mathf.Max(0f, holdExtraDelay);
                if (!isAttacking)
                {
                    StartCoroutine(AttackRoutine());
                    nextSwordAutoTime = Time.time + interval;
                }
                else nextSwordAutoTime = Time.time + 0.02f;
            }
            if (hold && Time.time >= nextSwordAutoTime)
            {
                float manualMin = Mathf.Max(0.01f, swingDuration + swordManualPadding);
                float interval  = manualMin + Mathf.Max(0f, holdExtraDelay);
                if (!isAttacking)
                {
                    StartCoroutine(AttackRoutine());
                    nextSwordAutoTime = Time.time + interval;
                }
                else nextSwordAutoTime = Time.time + 0.02f;
            }
        }
        else
        {
            if (press && Time.time >= nextGunAutoTime)
            {
                float interval = Mathf.Max(0f, gunAutoBaseCooldown) + Mathf.Max(0f, holdExtraDelay);
                FireGun();
                nextGunAutoTime = Time.time + interval;
            }
            if (enableHoldAttack && hold && Time.time >= nextGunAutoTime)
            {
                float interval = Mathf.Max(0f, gunAutoBaseCooldown) + Mathf.Max(0f, holdExtraDelay);
                FireGun();
                nextGunAutoTime = Time.time + interval;
            }
        }
    }

    void FireGun()
    {
        PlaySfx2D(sfxGun);

        var bm = GetComponent<BulletManager>();
        int count = bm?.count ?? 1;
        float verticalOffset = 0.2f;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = bulletSpawnPoint.position + Vector3.up * (i * verticalOffset);
            var b = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

            if (b.TryGetComponent<Rigidbody2D>(out var br))
                br.linearVelocity = new Vector2(facingDirection * bulletSpeed * (bm?.speedMultiplier ?? 1f), 0f);

            if (b.TryGetComponent<Bullet>(out var bulletComp))
                bulletComp.damage += bm?.damageBonus ?? 0;

            if (bm != null)
                b.transform.localScale *= bm.sizeMultiplier;
        }
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;

        PlaySfx2D(sfxSword);

        var sm = GetComponent<SlashManager>();
        if (sm != null)
            sm.SpawnSlash(facingDirection, baseDamage: 7f);

        Quaternion orig = sword != null ? sword.localRotation : Quaternion.identity;
        Quaternion down = orig * Quaternion.Euler(0f, 0f, -swingAngle);
        float half = Mathf.Max(0.01f, swingDuration / 2f);
        float t = 0f;

        while (t < half)
        {
            if (sword != null) sword.localRotation = Quaternion.Slerp(orig, down, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        if (sword != null) sword.localRotation = down;
        yield return new WaitForSeconds(0.1f);

        t = 0f;
        while (t < half)
        {
            if (sword != null) sword.localRotation = Quaternion.Slerp(down, orig, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        if (sword != null) sword.localRotation = orig;

        yield return new WaitForSeconds(0.05f);
        isAttacking = false;
    }

    public void UpdateWeaponVisuals()
    {
        for (int i = 0; i < weaponObjects.Length; i++)
            weaponObjects[i].SetActive(i == currentWeaponIndex);
    }

    private void Flip(int dir)
    {
        if (spriteRenderer != null) spriteRenderer.flipX = (dir < 0);
        else
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * dir;
            transform.localScale = s;
        }
    }
}
