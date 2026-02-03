using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.InputSystem;
using System;

namespace GoogleSpeechToText.Scripts
{
    public class SpeechToTextManager : MonoBehaviour
    {
        [Header("Google Cloud API（由 ApiKeysConfig 讀取）")]
        [Tooltip("金鑰請在 Assets/Resources/Config/ApiKeysConfig.asset 設定 googleCloudApiKey")]
        [Header("Gemini Manager Prefab")]
        public UnityAndGeminiV3 geminiManager;

        [Header("Microphone")]
        [Tooltip("留空則使用 Microphone.devices[0]。若指定，必須完全符合裝置名稱。")]
        [SerializeField] private string microphoneDevice;
        [Tooltip("若 >= 0，優先用此 index 選擇 Microphone.devices[index]（比打字更不易錯）。")]
        [SerializeField] private int microphoneDeviceIndex = -1;
        [Tooltip("避免太短導致 GetPosition=0；建議 0.2~0.5 秒")]
        [SerializeField] private float minRecordSeconds = 0.25f;
        [Tooltip("等待麥克風開始輸出資料的最長時間（秒）。")]
        [SerializeField] private float micStartTimeoutSeconds = 1.0f;
        [Tooltip("除錯：將錄到的 WAV 存到 Application.persistentDataPath，方便確認是否真的有聲音。")]
        [SerializeField] private bool debugWriteWavToDisk = false;

        [Header("Speech-to-Text Config")]
        [Tooltip("例如：zh-TW、zh-CN、en-US")]
        [SerializeField] private string languageCode = "zh-TW";
                
        private AudioClip clip;
        private byte[] bytes;
        private bool recording = false;
        private string _deviceInUse;
        private float _recordingStartTime;
        private Coroutine _stopCoroutine;

    private void Start()
    {
        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("SpeechToText: Microphone.devices 為空。若你確定有麥克風，請檢查 Windows 麥克風權限/裝置。");
            return;
        }

        Debug.Log($"SpeechToText: 可用麥克風裝置 ({devices.Length}) => [{string.Join(", ", devices)}]");
    }

    void Update()
    {
         if (Keyboard.current.spaceKey.wasPressedThisFrame && !recording)
            {
                StartRecording();
            }

            // Check if the spacebar is released
            if (Keyboard.current.spaceKey.wasReleasedThisFrame && recording)
            {
                StopRecording();
            }
    }


    private void StartRecording()
    {
        if (recording) return;

        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogError("SpeechToText StartRecording failed: 找不到任何麥克風裝置。請確認 Windows 麥克風權限與裝置可用。");
            return;
        }

        if (microphoneDeviceIndex >= 0 && microphoneDeviceIndex < devices.Length)
            _deviceInUse = devices[microphoneDeviceIndex];
        else
            _deviceInUse = string.IsNullOrWhiteSpace(microphoneDevice) ? devices[0] : microphoneDevice;

        var deviceExists = false;
        foreach (var d in devices)
        {
            if (d == _deviceInUse) { deviceExists = true; break; }
        }

        if (!deviceExists)
        {
            Debug.LogError($"SpeechToText StartRecording failed: 指定的 microphoneDevice 找不到：'{_deviceInUse}'. 可用裝置：[{string.Join(", ", devices)}]");
            return;
        }

        clip = Microphone.Start(_deviceInUse, false, 10, 44100);
        _recordingStartTime = Time.realtimeSinceStartup;
        recording = true;
    }

    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels) {
        using (var memoryStream = new MemoryStream(44 + samples.Length * 2)) {
            using (var writer = new BinaryWriter(memoryStream)) {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + samples.Length * 2);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2);
                writer.Write((ushort)(channels * 2));
                writer.Write((ushort)16);
                writer.Write("data".ToCharArray());
                writer.Write(samples.Length * 2);

                foreach (var sample in samples) {
                    writer.Write((short)(sample * short.MaxValue));
                }
            }
            return memoryStream.ToArray();
        }
    }

    private void StopRecording()
    {
            if (clip == null)
            {
                Debug.LogError("SpeechToText StopRecording failed: AudioClip is null. Did StartRecording() run successfully?");
                recording = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiKeysConfig.GoogleCloudKey))
            {
                Debug.LogError("SpeechToText: API 金鑰為空。請選單 Config→建立 API 金鑰設定，在 ApiKeysConfig.asset 填入 googleCloudApiKey。");
                recording = false;
                return;
            }

            if (geminiManager == null)
            {
                Debug.LogError("SpeechToText StopRecording failed: geminiManager is not assigned. Please drag your UnityAndGeminiV3 object into SpeechToTextManager.geminiManager in the Inspector.");
                recording = false;
                return;
            }

            if (_stopCoroutine != null)
            {
                StopCoroutine(_stopCoroutine);
                _stopCoroutine = null;
            }
            _stopCoroutine = StartCoroutine(StopAndSendCoroutine());
    }

    private IEnumerator StopAndSendCoroutine()
    {
            // 確保至少錄到一點點資料（太短常常 position=0）
            while (Time.realtimeSinceStartup - _recordingStartTime < minRecordSeconds)
                yield return null;

            var startWait = Time.realtimeSinceStartup;
            var position = 0;
            while ((position = Microphone.GetPosition(_deviceInUse)) <= 0 &&
                   Time.realtimeSinceStartup - startWait < micStartTimeoutSeconds)
            {
                yield return null;
            }

            if (position <= 0)
            {
                Debug.LogWarning(
                    "SpeechToText: No microphone data captured (position <= 0). " +
                    "可能原因：放開太快 / 麥克風權限被擋 / 裝置被別的程式獨佔。請在 Windows 設定確認：隱私權與安全性 → 麥克風 → 允許應用程式存取麥克風。");
                recording = false;
                _stopCoroutine = null;
                yield break;
            }

            // 重要：先 GetData 再 End。有些環境下 End 之後再讀可能會讀到全 0。
            var all = new float[clip.samples * clip.channels];
            clip.GetData(all, 0);
            Microphone.End(_deviceInUse);

            var take = Mathf.Min(position * clip.channels, all.Length);
            var samples = new float[take];
            Array.Copy(all, 0, samples, 0, take);
            bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);
            recording = false;
            _stopCoroutine = null;

            // 簡單檢查一下是不是幾乎靜音（靜音會直接導致 results 為空）
            float maxAbs = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                var a = Mathf.Abs(samples[i]);
                if (a > maxAbs) maxAbs = a;
            }
            Debug.Log($"SpeechToText audio stats: duration={(position / (float)clip.frequency):0.00}s, freq={clip.frequency}, channels={clip.channels}, maxAbs={maxAbs:0.000}");

            if (debugWriteWavToDisk)
            {
                try
                {
                    var path = Path.Combine(Application.persistentDataPath, "stt_mic_debug.wav");
                    File.WriteAllBytes(path, bytes);
                    Debug.Log("SpeechToText debug WAV saved: " + path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("SpeechToText debug WAV save failed: " + e.Message);
                }
            }

            GoogleCloudSpeechToText.SendSpeechToTextRequest(bytes, ApiKeysConfig.GoogleCloudKey, clip.frequency, clip.channels, languageCode,
                (response) =>
                {
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        Debug.LogWarning("Speech-to-Text returned empty response body.");
                        return;
                    }

                    Debug.Log("Speech-to-Text Response: " + response);

                    SpeechToTextResponse speechResponse = null;
                    try
                    {
                        speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Speech-to-Text response JSON parse failed: " + e.Message + "\nRaw: " + response);
                        return;
                    }

                    if (speechResponse == null || speechResponse.results == null || speechResponse.results.Length == 0)
                    {
                        Debug.LogWarning("Speech-to-Text: No results (maybe silence / unrecognized speech).");
                        return;
                    }

                    var firstResult = speechResponse.results[0];
                    if (firstResult == null || firstResult.alternatives == null || firstResult.alternatives.Length == 0)
                    {
                        Debug.LogWarning("Speech-to-Text: No alternatives in first result.");
                        return;
                    }

                    var transcript = firstResult.alternatives[0]?.transcript;
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        Debug.LogWarning("Speech-to-Text: Transcript is empty.");
                        return;
                    }

                    Debug.Log("Transcript: " + transcript);
                    geminiManager.SendChat(transcript);
                },
                (error) =>
                {
                    var msg = error?.error?.message ?? "(unknown error)";
                    Debug.LogError("Speech-to-Text Error: " + msg);
                });
    }

    }
}
