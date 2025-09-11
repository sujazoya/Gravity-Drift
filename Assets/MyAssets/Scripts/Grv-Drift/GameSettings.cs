using System;
using System.IO;
using UnityEngine;

namespace zoya.game
{




    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }

        [Header("Audio Settings")]
        [Range(0.001f, 1f)] public float MasterVolume = 1f;
        [Range(0.001f, 1f)] public float MusicVolume = 1f;
        [Range(0.001f, 1f)] public float SFXVolume = 1f;

        [Header("Video Settings")]
        public bool IsFullscreen = true;
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;
        public int RefreshRate = 60;
        public int QualityLevel = 2;
        public float FieldOfView = 90f;
        public bool VSyncEnabled = true;
        public int FrameRateLimit = 144;

        [Header("Gameplay Settings")]
        public float MouseSensitivity = 2f;
        public bool InvertMouseY = false;
        public string Username = "Player";
        public float GamepadSensitivity = 2f;
        public bool VibrationEnabled = true;

        [Header("Network Settings")]
        public string LastConnectedIP = "localhost";
        public int LastConnectedPort = 7770;
        public int NetworkTickRate = 60;
        public bool EnablePrediction = true;
        public bool EnableReconciliation = true;

        [Header("Control Bindings")]
        public KeyCode ForwardKey = KeyCode.W;
        public KeyCode BackwardKey = KeyCode.S;
        public KeyCode LeftKey = KeyCode.A;
        public KeyCode RightKey = KeyCode.D;
        public KeyCode JumpKey = KeyCode.Space;
        public KeyCode CrouchKey = KeyCode.LeftControl;
        public KeyCode SprintKey = KeyCode.LeftShift;
        public KeyCode UseKey = KeyCode.E;
        public KeyCode ReloadKey = KeyCode.R;
        public KeyCode ScoreboardKey = KeyCode.Tab;

        private string _settingsFilePath;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeSettings()
        {
            _settingsFilePath = Path.Combine(Application.persistentDataPath, "gamesettings.json");
            LoadSettings();
            ApplySettingsImmediately();
        }

        public void SaveSettings()
        {
            try
            {
                SettingsData data = new SettingsData
                {
                    // Audio
                    MasterVolume = MasterVolume,
                    MusicVolume = MusicVolume,
                    SFXVolume = SFXVolume,

                    // Video
                    IsFullscreen = IsFullscreen,
                    ResolutionWidth = ResolutionWidth,
                    ResolutionHeight = ResolutionHeight,
                    RefreshRate = RefreshRate,
                    QualityLevel = QualityLevel,
                    FieldOfView = FieldOfView,
                    VSyncEnabled = VSyncEnabled,
                    FrameRateLimit = FrameRateLimit,

                    // Gameplay
                    MouseSensitivity = MouseSensitivity,
                    InvertMouseY = InvertMouseY,
                    Username = Username,
                    GamepadSensitivity = GamepadSensitivity,
                    VibrationEnabled = VibrationEnabled,

                    // Network
                    LastConnectedIP = LastConnectedIP,
                    LastConnectedPort = LastConnectedPort,
                    NetworkTickRate = NetworkTickRate,
                    EnablePrediction = EnablePrediction,
                    EnableReconciliation = EnableReconciliation,

                    // Controls
                    ForwardKey = ForwardKey,
                    BackwardKey = BackwardKey,
                    LeftKey = LeftKey,
                    RightKey = RightKey,
                    JumpKey = JumpKey,
                    CrouchKey = CrouchKey,
                    SprintKey = SprintKey,
                    UseKey = UseKey,
                    ReloadKey = ReloadKey,
                    ScoreboardKey = ScoreboardKey
                };

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(_settingsFilePath, json);

                Debug.Log("Settings saved successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save settings: {e.Message}");
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    SettingsData data = JsonUtility.FromJson<SettingsData>(json);

                    // Audio
                    MasterVolume = data.MasterVolume;
                    MusicVolume = data.MusicVolume;
                    SFXVolume = data.SFXVolume;

                    // Video
                    IsFullscreen = data.IsFullscreen;
                    ResolutionWidth = data.ResolutionWidth;
                    ResolutionHeight = data.ResolutionHeight;
                    RefreshRate = data.RefreshRate;
                    QualityLevel = data.QualityLevel;
                    FieldOfView = data.FieldOfView;
                    VSyncEnabled = data.VSyncEnabled;
                    FrameRateLimit = data.FrameRateLimit;

                    // Gameplay
                    MouseSensitivity = data.MouseSensitivity;
                    InvertMouseY = data.InvertMouseY;
                    Username = data.Username;
                    GamepadSensitivity = data.GamepadSensitivity;
                    VibrationEnabled = data.VibrationEnabled;

                    // Network
                    LastConnectedIP = data.LastConnectedIP;
                    LastConnectedPort = data.LastConnectedPort;
                    NetworkTickRate = data.NetworkTickRate;
                    EnablePrediction = data.EnablePrediction;
                    EnableReconciliation = data.EnableReconciliation;

                    // Controls
                    ForwardKey = data.ForwardKey;
                    BackwardKey = data.BackwardKey;
                    LeftKey = data.LeftKey;
                    RightKey = data.RightKey;
                    JumpKey = data.JumpKey;
                    CrouchKey = data.CrouchKey;
                    SprintKey = data.SprintKey;
                    UseKey = data.UseKey;
                    ReloadKey = data.ReloadKey;
                    ScoreboardKey = data.ScoreboardKey;

                    Debug.Log("Settings loaded successfully.");
                }
                else
                {
                    Debug.Log("No settings file found. Using default settings.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load settings: {e.Message}");
            }
        }

        public void ApplySettingsImmediately()
        {
            // Apply video settings
            Screen.fullScreen = IsFullscreen;
            QualitySettings.SetQualityLevel(QualityLevel);
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
            Application.targetFrameRate = FrameRateLimit;

            // Apply resolution if needed
            if (Screen.currentResolution.width != ResolutionWidth ||
                Screen.currentResolution.height != ResolutionHeight ||
                Screen.currentResolution.refreshRateRatio.value != RefreshRate)
            {
                Screen.SetResolution(
                ResolutionWidth,
                ResolutionHeight,
                IsFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed,
                new RefreshRate()
                {
                    numerator = (uint)RefreshRate,  // cast int -> uint
                    denominator = 1u                 // 1u ensures it's a uint
                }
            );


            }

            Debug.Log("Settings applied immediately.");
        }

        public void ResetToDefaults()
        {
            // Audio defaults
            MasterVolume = 1f;
            MusicVolume = 1f;
            SFXVolume = 1f;

            // Video defaults
            IsFullscreen = true;
            ResolutionWidth = 1920;
            ResolutionHeight = 1080;
            RefreshRate = 60;
            QualityLevel = 2;
            FieldOfView = 90f;
            VSyncEnabled = true;
            FrameRateLimit = 144;

            // Gameplay defaults
            MouseSensitivity = 2f;
            InvertMouseY = false;
            Username = "Player";
            GamepadSensitivity = 2f;
            VibrationEnabled = true;

            // Network defaults
            LastConnectedIP = "localhost";
            LastConnectedPort = 7770;
            NetworkTickRate = 60;
            EnablePrediction = true;
            EnableReconciliation = true;

            // Control defaults
            ForwardKey = KeyCode.W;
            BackwardKey = KeyCode.S;
            LeftKey = KeyCode.A;
            RightKey = KeyCode.D;
            JumpKey = KeyCode.Space;
            CrouchKey = KeyCode.LeftControl;
            SprintKey = KeyCode.LeftShift;
            UseKey = KeyCode.E;
            ReloadKey = KeyCode.R;
            ScoreboardKey = KeyCode.Tab;

            ApplySettingsImmediately();
            SaveSettings();
        }

        public bool ValidateSettings()
        {
            bool isValid = true;

            // Validate audio settings
            if (MasterVolume < 0.001f || MasterVolume > 1f) isValid = false;
            if (MusicVolume < 0.001f || MusicVolume > 1f) isValid = false;
            if (SFXVolume < 0.001f || SFXVolume > 1f) isValid = false;

            // Validate video settings
            if (ResolutionWidth < 640 || ResolutionHeight < 480) isValid = false;
            if (RefreshRate < 30 || RefreshRate > 240) isValid = false;
            if (QualityLevel < 0 || QualityLevel > QualitySettings.names.Length - 1) isValid = false;
            if (FieldOfView < 60f || FieldOfView > 120f) isValid = false;

            // Validate gameplay settings
            if (MouseSensitivity < 0.1f || MouseSensitivity > 10f) isValid = false;
            if (GamepadSensitivity < 0.1f || GamepadSensitivity > 10f) isValid = false;

            return isValid;
        }

        public string GetSettingsSummary()
        {
            return $"Settings Summary:\n" +
                   $"Username: {Username}\n" +
                   $"Resolution: {ResolutionWidth}x{ResolutionHeight}@{RefreshRate}Hz\n" +
                   $"Quality: {QualitySettings.names[QualityLevel]}\n" +
                   $"Fullscreen: {IsFullscreen}\n" +
                   $"VSync: {VSyncEnabled}\n" +
                   $"FPS Limit: {FrameRateLimit}\n" +
                   $"Mouse Sensitivity: {MouseSensitivity}";
        }

        private void OnApplicationQuit()
        {
            SaveSettings();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveSettings();
            }
        }

        [Serializable]
        private class SettingsData
        {
            // Audio
            public float MasterVolume;
            public float MusicVolume;
            public float SFXVolume;

            // Video
            public bool IsFullscreen;
            public int ResolutionWidth;
            public int ResolutionHeight;
            public int RefreshRate;
            public int QualityLevel;
            public float FieldOfView;
            public bool VSyncEnabled;
            public int FrameRateLimit;

            // Gameplay
            public float MouseSensitivity;
            public bool InvertMouseY;
            public string Username;
            public float GamepadSensitivity;
            public bool VibrationEnabled;

            // Network
            public string LastConnectedIP;
            public int LastConnectedPort;
            public int NetworkTickRate;
            public bool EnablePrediction;
            public bool EnableReconciliation;

            // Controls
            public KeyCode ForwardKey;
            public KeyCode BackwardKey;
            public KeyCode LeftKey;
            public KeyCode RightKey;
            public KeyCode JumpKey;
            public KeyCode CrouchKey;
            public KeyCode SprintKey;
            public KeyCode UseKey;
            public KeyCode ReloadKey;
            public KeyCode ScoreboardKey;
        }
    }
}