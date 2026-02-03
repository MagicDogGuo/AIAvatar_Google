using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Text;
using GoogleTextToSpeech.Scripts.Data;
using GoogleTextToSpeech.Scripts;


[System.Serializable]
public class UnityAndGeminiKey
{
    public string key;
}

[System.Serializable]
public class Response
{
    public Candidate[] candidates;
}

public class ChatRequest
{
    public Content[] contents;
    public SystemInstruction systemInstruction;
    public GenerationConfig generationConfig;
}

[System.Serializable]
public class SystemInstruction
{
    public Part[] parts;
}

[System.Serializable]
public class GenerationConfig
{
    // Gemini 的 token 限制：用 token 控制「大概」長度（跟字元數不完全等同）
    public int maxOutputTokens = 256;
    // 保守一點，避免太發散產生奇怪符號/格式
    public float temperature = 0.4f;
}

[System.Serializable]
public class Candidate
{
    public Content content;
}

[System.Serializable]
public class Content
{
    public string role; 
    public Part[] parts;
}

[System.Serializable]
public class Part
{
    public string text;
}


public class UnityAndGeminiV3: MonoBehaviour
{
    [Header("Gemini API（由 ApiKeysConfig 讀取）")]
    [Tooltip("金鑰請在 Assets/Resources/Config/ApiKeysConfig.asset 設定 geminiApiKey")]
    private string apiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent"; // Edit it and choose your prefer model

    [Header("Gemini Response Control")]
    [Tooltip("控制 Gemini 回應最長 token（不是字數）。")]
    [SerializeField] private int maxOutputTokens = 256;
    [Tooltip("系統指令：要求 Gemini 用純文字、短回覆、避免特殊符號/Markdown。")]
    [TextArea(3, 10)]
    [SerializeField] private string systemInstructionText =
        "Please keep replies concise and avoid using any special symbols or Markdown formatting (such as #, *, `, >, -, and emoticons). Output only plain text that can be read aloud directly.";

    [Header("TTS Text Sanitizer")]
    [Tooltip("送進 TTS 前最多保留多少字元（硬截斷）。")]
    [SerializeField] private int maxTtsChars = 500;
    [Tooltip("是否保留基本標點（。，、！？,.!?）。關閉=幾乎只留中英數與空白。")]
    [SerializeField] private bool keepBasicPunctuation = true;


    [Header("NPC Function")]
    [SerializeField] private TextToSpeechManager googleServices;
    [Header("UI Display")]
    [Tooltip("用來顯示 Gemini 回覆的 Text 元件（可選）。")]
    [SerializeField] private Text replyText;
    private Content[] chatHistory;

    void Start()
    {
        chatHistory = new Content[] { };
    }

    // Functions for sending a new prompt, or a chat to Gemini
    private IEnumerator SendPromptRequestToGemini(string promptText)
    {
        string url = $"{apiEndpoint}?key={ApiKeysConfig.GeminiKey}";

        var userContent = new Content
        {
            role = "user",
            parts = new Part[] { new Part { text = promptText } }
        };

        var req = new ChatRequest
        {
            contents = new[] { userContent },
            systemInstruction = BuildSystemInstruction(),
            generationConfig = new GenerationConfig { maxOutputTokens = maxOutputTokens, temperature = 0.4f }
        };

        string jsonData = JsonUtility.ToJson(req);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // Create a UnityWebRequest with the JSON data
        using (UnityWebRequest www = new UnityWebRequest(url, "POST")){
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success) {
                Debug.LogError(www.error);
            } else {
                Debug.Log("Request complete!");
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                    {
                        //This is the response to your request
                        string text = response.candidates[0].content.parts[0].text;
                        string ttsText = PrepareTtsText(text);
                        Debug.Log(text);
                        Debug.Log("TTS Sanitized: " + ttsText);
                    }
                else
                {
                    Debug.Log("No text found.");
                }
            }
        }
    }

    public void SendChat(string userMessage)
    {
        // string userMessage = inputField.text;
        StartCoroutine( SendChatRequestToGemini(userMessage));
    }

    private IEnumerator SendChatRequestToGemini(string newMessage)
    {

        string url = $"{apiEndpoint}?key={ApiKeysConfig.GeminiKey}";
     
        Content userContent = new Content
        {
            role = "user",
            parts = new Part[]
            {
                new Part { text = newMessage }
            }
        };

        List<Content> contentsList = new List<Content>(chatHistory);
        contentsList.Add(userContent);
        chatHistory = contentsList.ToArray(); 

        ChatRequest chatRequest = new ChatRequest
        {
            contents = chatHistory,
            systemInstruction = BuildSystemInstruction(),
            generationConfig = new GenerationConfig { maxOutputTokens = maxOutputTokens, temperature = 0.4f }
        };

        string jsonData = JsonUtility.ToJson(chatRequest);

        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // Create a UnityWebRequest with the JSON data
        using (UnityWebRequest www = new UnityWebRequest(url, "POST")){
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success) {
                Debug.LogError(www.error);
            } else {
                Debug.Log("Request complete!");
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                    {
                        //This is the response to your request
                        string reply = response.candidates[0].content.parts[0].text;
                        Content botContent = new Content
                        {
                            role = "model",
                            parts = new Part[]
                            {
                                new Part { text = reply }
                            }
                        };

                        Debug.Log(reply);
                        
                        // 顯示到 Text UI
                        if (replyText != null)
                        {
                            replyText.text = reply;
                        }
                        
                        string ttsText = PrepareTtsText(reply);
                        if (string.IsNullOrWhiteSpace(ttsText))
                        {
                            Debug.LogWarning("Gemini reply became empty after sanitizing; skipping TTS.");
                        }
                        else
                        {
                            googleServices.SendTextToGoogle(ttsText);
                        }
                        //This part adds the response to the chat history, for your next message
                        contentsList.Add(botContent);
                        chatHistory = contentsList.ToArray();
                    }
                else
                {
                    Debug.Log("No text found.");
                }
             }
        }  
    }

    private SystemInstruction BuildSystemInstruction()
    {
        if (string.IsNullOrWhiteSpace(systemInstructionText))
            return null;

        return new SystemInstruction
        {
            parts = new[] { new Part { text = systemInstructionText } }
        };
    }

    private string PrepareTtsText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // 1) 逐字過濾：保留中英數、空白；可選保留少量標點
        var sb = new StringBuilder(raw.Length);
        bool lastWasSpace = false;

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (IsCjk(c) || char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSpace = false;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }

            if (keepBasicPunctuation && IsBasicPunctuation(c))
            {
                sb.Append(c);
                lastWasSpace = false;
                continue;
            }

            // 其他符號全部丟掉（含 Markdown 符號、emoji、括號、引號等）
        }

        var text = sb.ToString().Trim();

        // 2) 截斷（避免太長念不完）
        if (maxTtsChars > 0 && text.Length > maxTtsChars)
            text = text.Substring(0, maxTtsChars);

        return text;
    }

    private static bool IsBasicPunctuation(char c)
    {
        // 保留少量「唸出來不太會干擾」的標點
        switch (c)
        {
            case '。':
            case '，':
            case '、':
            case '！':
            case '？':
            case '.':
            case ',':
            case '!':
            case '?':
                return true;
            default:
                return false;
        }
    }

    private static bool IsCjk(char c)
    {
        // 常用中日韓統一表意文字區段（涵蓋多數中文）
        return (c >= 0x4E00 && c <= 0x9FFF)
               || (c >= 0x3400 && c <= 0x4DBF)
               || (c >= 0xF900 && c <= 0xFAFF);
    }

}


