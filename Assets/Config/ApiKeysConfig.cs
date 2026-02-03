using UnityEngine;

/// <summary>
/// 集中管理 API 金鑰的 ScriptableObject。
/// 此 asset 已加入 .gitignore，不會被提交到 Git，避免金鑰外洩。
/// 首次開啟專案時，Editor 會自動建立空白的 ApiKeysConfig.asset，請在 Inspector 填入金鑰。
/// </summary>
[CreateAssetMenu(fileName = "ApiKeysConfig", menuName = "Config/Api Keys", order = 0)]
public class ApiKeysConfig : ScriptableObject
{
    [Header("Gemini API（AI 對話）")]
    [Tooltip("從 https://aistudio.google.com/api-keys 取得")]
    public string geminiApiKey = "";

    [Header("Google Cloud API（STT + TTS）")]
    [Tooltip("從 https://console.cloud.google.com/apis/credentials 取得，供 Speech-to-Text 與 Text-to-Speech 共用")]
    public string googleCloudApiKey = "";

    private static ApiKeysConfig _instance;

    /// <summary>
    /// 從 Resources/Config 載入 ApiKeysConfig。
    /// </summary>
    public static ApiKeysConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ApiKeysConfig>("Config/ApiKeysConfig");
            return _instance;
        }
    }

    public static string GeminiKey => Instance != null ? Instance.geminiApiKey : "";
    public static string GoogleCloudKey => Instance != null ? Instance.googleCloudApiKey : "";
}
