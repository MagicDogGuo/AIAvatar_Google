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

        private const int RecordSampleRate = 44100;

        private AudioClip clip;
        private byte[] bytes;
        private bool recording = false;
        private string _deviceInUse;
        private float _recordingStartTime;
        private Coroutine _stopCoroutine;
#if UNITY_WEBGL && !UNITY_EDITOR
        private float[] _webglRecordBuffer;
#endif

    private void Start()
    {
        var devices = MicrophoneBridge.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("SpeechToText: 找不到麥克風裝置。請檢查權限與裝置。");
            return;
        }
        Debug.Log($"SpeechToText: 可用麥克風 ({devices.Length}) => [{string.Join(", ", devices)}]");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !recording)
            StartRecording();
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame && recording)
            StopRecording();
    }

    private void StartRecording()
    {
        if (recording) return;

        var devices = MicrophoneBridge.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogError("SpeechToText StartRecording failed: 找不到任何麥克風裝置。");
            return;
        }

        if (microphoneDeviceIndex >= 0 && microphoneDeviceIndex < devices.Length)
            _deviceInUse = devices[microphoneDeviceIndex];
        else
            _deviceInUse = string.IsNullOrWhiteSpace(microphoneDevice) ? devices[0] : microphoneDevice;

        clip = MicrophoneBridge.Start(_deviceInUse, false, 10, RecordSampleRate);
        _recordingStartTime = Time.realtimeSinceStartup;
        recording = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_webglRecordBuffer == null || _webglRecordBuffer.Length < RecordSampleRate * 10)
            _webglRecordBuffer = new float[RecordSampleRate * 10];
#endif
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
        if (clip == null && !MicrophoneBridge.IsWebGL)
        {
            Debug.LogError("SpeechToText StopRecording failed: AudioClip is null.");
            recording = false;
            return;
        }
        if (string.IsNullOrWhiteSpace(ApiKeysConfig.GoogleCloudKey))
        {
            Debug.LogError("SpeechToText: API 金鑰為空。請在 ApiKeysConfig.asset 填入 googleCloudApiKey。");
            recording = false;
            return;
        }
        if (geminiManager == null)
        {
            Debug.LogError("SpeechToText StopRecording failed: geminiManager 未指定。");
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
        int position = 0;
        int sampleRate = RecordSampleRate;
        int channels = 1;

#if UNITY_WEBGL && !UNITY_EDITOR
        while (Time.realtimeSinceStartup - _recordingStartTime < minRecordSeconds)
            yield return null;

        int n = MicrophoneBridge.ReadLatestSamples(_webglRecordBuffer);
        MicrophoneBridge.End(_deviceInUse);

        if (n <= 0)
        {
            Debug.LogWarning("SpeechToText WebGL: 未取得麥克風資料，請允許瀏覽器麥克風權限。");
            recording = false;
            _stopCoroutine = null;
            yield break;
        }

        var samples = new float[n];
        Array.Copy(_webglRecordBuffer, samples, n);
        bytes = EncodeAsWAV(samples, RecordSampleRate, 1);
        position = n;
        sampleRate = RecordSampleRate;
        channels = 1;
#else
        while (Time.realtimeSinceStartup - _recordingStartTime < minRecordSeconds)
            yield return null;

        var startWait = Time.realtimeSinceStartup;
        while ((position = MicrophoneBridge.GetPosition(_deviceInUse)) <= 0 &&
               Time.realtimeSinceStartup - startWait < micStartTimeoutSeconds)
            yield return null;

        if (position <= 0)
        {
            Debug.LogWarning("SpeechToText: 未錄到麥克風資料，請檢查權限與裝置。");
            recording = false;
            _stopCoroutine = null;
            yield break;
        }

        var all = new float[clip.samples * clip.channels];
        clip.GetData(all, 0);
        MicrophoneBridge.End(_deviceInUse);

        var take = Mathf.Min(position * clip.channels, all.Length);
        var samples = new float[take];
        Array.Copy(all, 0, samples, 0, take);
        bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);
        sampleRate = clip.frequency;
        channels = clip.channels;
#endif

        recording = false;
        _stopCoroutine = null;

        float maxAbs = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            var a = Mathf.Abs(samples[i]);
            if (a > maxAbs) maxAbs = a;
        }
        Debug.Log($"SpeechToText audio: duration={(position / (float)sampleRate):0.00}s, freq={sampleRate}, maxAbs={maxAbs:0.000}");

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

        GoogleCloudSpeechToText.SendSpeechToTextRequest(bytes, ApiKeysConfig.GoogleCloudKey, sampleRate, channels, languageCode,
            (response) =>
            {
                if (string.IsNullOrWhiteSpace(response))
                {
                    Debug.LogWarning("Speech-to-Text returned empty response body.");
                    return;
                }
                Debug.Log("Speech-to-Text Response: " + response);

                SpeechToTextResponse speechResponse = null;
                try { speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response); }
                catch (Exception e)
                {
                    Debug.LogError("Speech-to-Text JSON parse failed: " + e.Message + "\nRaw: " + response);
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
