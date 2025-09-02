using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Linq;

public class DeathManager : MonoBehaviour
{
    public static DeathManager Instance;

    [Header("UI 레퍼런스")]
    public Image fadeImage;
    public Text youDiedText;
    public GameObject upgradeUIPrefab;

    [Header("UI를 띄울 Canvas (Screen Space - Overlay)")]
    public Canvas uiCanvas;

    [Header("페이드 설정")]
    public float fadeInDuration = 1f;
    public float holdDuration = 0.3f;
    public float fadeOutDuration = 1f;

    public BossController currentBoss;
    private float bossInitialHp;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        if (fadeImage) fadeImage.gameObject.SetActive(false);
        if (youDiedText) youDiedText.gameObject.SetActive(false);
    }

    public void RegisterBoss(BossController boss)
    {
        currentBoss = boss;
        var h = boss ? boss.GetComponent<Health>() : null;
        bossInitialHp = h ? h.maxHp : 0f;
    }

    public void StartDeathSequence()
    {
        StartCoroutine(DeathSequence());
    }

    // 업그레이드 UI에서 선택 확정 시 호출 권장
    public void OnUpgradeConfirmed()
    {
        DialogueManager.ForceCloseAll();
        TryAutoSave("AfterDeath");
    }

    IEnumerator DeathSequence()
    {
        // 입력/대화 차단
        DialogueManager.ForceCloseAll();

        // 1) 텍스트·페이드 준비(알파 0)
        if (youDiedText)
        {
            youDiedText.gameObject.SetActive(true);
            var c0 = youDiedText.color;
            c0.a = 0f;
            youDiedText.color = c0;
        }

        if (fadeImage)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
        }

        // 2) 페이드 인(검은 화면 0→1, 텍스트 0→1)
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeInDuration);

            if (fadeImage)
                fadeImage.color = Color.Lerp(Color.clear, Color.black, k);

            if (youDiedText)
            {
                var c = youDiedText.color;
                c.a = Mathf.Lerp(0f, 1f, k);
                youDiedText.color = c;
            }

            yield return null;
        }

        // 스냅샷 저장
        TryAutoSave("AfterDeath");

        // 3) 잠시 유지
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        // 4) 페이드 아웃(검은 화면 1→0, 텍스트 1→0)
        float u = 0f;
        while (u < fadeOutDuration)
        {
            u += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(u / fadeOutDuration);

            if (fadeImage)
                fadeImage.color = Color.Lerp(Color.black, Color.clear, k);

            if (youDiedText)
            {
                var c = youDiedText.color;
                c.a = Mathf.Lerp(1f, 0f, k);
                youDiedText.color = c;
            }

            yield return null;
        }

        if (fadeImage) fadeImage.gameObject.SetActive(false);
        if (youDiedText) youDiedText.gameObject.SetActive(false);

        // ─────────────────────────────────────────────
        // 하드 모드: 업그레이드 선택 없이 즉시 리로드
        // ─────────────────────────────────────────────
        if (DifficultyManager.IsHardMode)
        {
            var stm = SceneTransitionManager.Instance;
            if (stm != null) stm.ReloadCurrentScene();
            else
            {
                var curr = SceneManager.GetActiveScene().name;
                SceneManager.LoadScene(curr, LoadSceneMode.Single);
            }
            yield break;
        }

        // 5) (이지 모드) 보스 피해 비율로 업그레이드 개수 결정
        var bossHealth = currentBoss ? currentBoss.GetComponent<Health>() : null;
        float currentHp = bossHealth ? bossHealth.CurrentHp : 0f;
        float damageDealt = bossInitialHp - currentHp;
        float ratio = bossInitialHp > 0f ? damageDealt / bossInitialHp : 0f;
        int pickCount = (ratio >= 0.7f) ? 3 : (ratio >= 0.3f) ? 2 : 1;

        // 6) 업그레이드 UI 표시
        if (upgradeUIPrefab && uiCanvas)
        {
            var uiGO = Instantiate(upgradeUIPrefab, uiCanvas.transform, false);

            DialogueManager.ForceCloseAll();

            var sel = uiGO.GetComponent<UpgradeSelectionUI>();
            if (sel != null) sel.Init(pickCount);
        }
    }

    void TryAutoSave(string spawnIdHint)
    {
        var snap = FindObjectOfType<GameSnapshotter>();
        if (snap == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        string spawnPointId = ResolveSpawnPointId(spawnIdHint);

        AutoSaveAPI.SaveNow(sceneName, spawnPointId, snap);
    }

    string ResolveSpawnPointId(string hint = "")
    {
        var byTag = GameObject.FindWithTag("Respawn");
        if (byTag) return byTag.name;

        var byName = GameObject.Find("RespawnPoint") ?? GameObject.Find("SpawnPoint");
        if (byName) return byName.name;

        return string.IsNullOrEmpty(hint) ? "DefaultSpawn" : hint;
    }
}
