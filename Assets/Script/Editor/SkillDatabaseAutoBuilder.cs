#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;
using System.IO;

public class SkillDatabaseAutoBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    // 메뉴에서 수동 재생성도 가능
    [MenuItem("Tools/SaveSystem/Rebuild Skill Database (Resources)")]
    public static void RebuildMenu() => BuildInternal();

    // 빌드 직전에 자동 생성/업데이트
    public void OnPreprocessBuild(BuildReport report) => BuildInternal();

    private static void BuildInternal()
    {
        // 프로젝트 전체에서 SkillSO 에셋 수집
        var guids = AssetDatabase.FindAssets("t:SkillSO");
        var skills = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => AssetDatabase.LoadAssetAtPath<SkillSO>(p))
            .Where(s => s != null)
            .Distinct()
            .ToList();

        const string resourcesDir = "Assets/Resources";
        const string assetPath    = resourcesDir + "/SkillDatabase.asset";

        if (!Directory.Exists(resourcesDir))
            Directory.CreateDirectory(resourcesDir);

        // 기존 DB 로드 또는 생성
        var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(assetPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<SkillDatabase>();
            AssetDatabase.CreateAsset(db, assetPath);
        }

        db.skills = skills; // 이름 매핑은 SkillDatabase 내부 OnEnable에서 준비됨
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SkillDatabaseAutoBuilder] Built SkillDatabase with {skills.Count} entries at {assetPath}");
    }
}
#endif
