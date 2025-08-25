using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadService : MonoBehaviour
{
    public static SceneLoadService Instance { get; private set; }
    [SerializeField] private SceneFader fader;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadSceneWithFade(string sceneName)
    {
        StartCoroutine(CoLoad(sceneName));
    }

    private IEnumerator CoLoad(string sceneName)
    {
        if (fader != null) yield return fader.FadeOut();
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
        // 새 씬에도 SceneFader가 1개 있어야 페이드인 가능
        var newFader = FindObjectOfType<SceneFader>();
        if (newFader != null) yield return newFader.FadeIn();
    }
}
