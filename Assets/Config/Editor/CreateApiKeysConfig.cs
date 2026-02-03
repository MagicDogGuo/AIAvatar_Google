#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 首次開啟專案時自動建立 ApiKeysConfig.asset（若不存在）。
/// 此 asset 已加入 .gitignore，clone 專案後會自動建立空白 config，請在 Inspector 填入金鑰。
/// </summary>
[InitializeOnLoad]
public static class CreateApiKeysConfig
{
    private const string ConfigPath = "Assets/Resources/Config/ApiKeysConfig.asset";

    static CreateApiKeysConfig()
    {
        EditorApplication.delayCall += EnsureConfigExists;
    }

    [MenuItem("Config/建立 API 金鑰設定")]
    public static void EnsureConfigExists()
    {
        var asset = AssetDatabase.LoadAssetAtPath<ApiKeysConfig>(ConfigPath);
        if (asset != null) return;

        // 建立 Resources/Config 資料夾
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Config"))
            AssetDatabase.CreateFolder("Assets/Resources", "Config");

        asset = ScriptableObject.CreateInstance<ApiKeysConfig>();
        AssetDatabase.CreateAsset(asset, ConfigPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[ApiKeysConfig] 已自動建立 Assets/Resources/Config/ApiKeysConfig.asset，請在 Inspector 填入 Gemini 與 Google Cloud API 金鑰。");
    }
}
#endif
