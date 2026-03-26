using UnityEngine;
using UnityEngine.UI;

namespace RunAndGun
{
    /// <summary>
    /// Pause menu overlay. Manages Time.timeScale, provides resume / restart /
    /// main-menu buttons, and exposes a settings sub-panel.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Settings Controls")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private Button settingsBackButton;

        [Header("Control Hint")]
        [SerializeField] private Text controlHintText;
        [SerializeField] private string desktopHint = "ESC - Pause  |  WASD - Move  |  Space - Jump  |  LClick - Shoot  |  Q - Switch Weapon";
        [SerializeField] private string mobileHint = "Use the on-screen controls to play.";

        [Header("Audio")]
        [SerializeField] private AudioClip buttonClickSfx;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //

        private bool isPaused;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (resumeButton != null)    resumeButton.onClick.AddListener(Resume);
            if (restartButton != null)   restartButton.onClick.AddListener(Restart);
            if (settingsButton != null)  settingsButton.onClick.AddListener(OpenSettings);
            if (mainMenuButton != null)  mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);

            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            if (muteToggle != null)
                muteToggle.onValueChanged.AddListener(OnMuteToggled);
        }

        private void Start()
        {
            if (pausePanel != null)    pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            UpdateControlHint();
        }

        private void Update()
        {
            // Listen for pause input
            bool pauseRequested = false;

            if (InputManager.Instance != null)
                pauseRequested = InputManager.Instance.PauseDown;
            else
                pauseRequested = Input.GetKeyDown(KeyCode.Escape);

            if (pauseRequested)
            {
                if (isPaused)
                    Resume();
                else
                    Pause();
            }
        }

        private void OnDestroy()
        {
            if (resumeButton != null)    resumeButton.onClick.RemoveListener(Resume);
            if (restartButton != null)   restartButton.onClick.RemoveListener(Restart);
            if (settingsButton != null)  settingsButton.onClick.RemoveListener(OpenSettings);
            if (mainMenuButton != null)  mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(CloseSettings);

            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            if (muteToggle != null)
                muteToggle.onValueChanged.RemoveListener(OnMuteToggled);
        }

        // ------------------------------------------------------------------ //
        //  Pause / Resume
        // ------------------------------------------------------------------ //

        public void Pause()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                return;

            isPaused = true;
            Time.timeScale = 0f;

            if (pausePanel != null)
                pausePanel.SetActive(true);

            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            SyncSettingsUI();

            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Paused);
        }

        public void Resume()
        {
            PlayClickSound();

            isPaused = false;
            Time.timeScale = 1f;

            if (pausePanel != null)
                pausePanel.SetActive(false);

            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Playing);
        }

        // ------------------------------------------------------------------ //
        //  Button handlers
        // ------------------------------------------------------------------ //

        private void Restart()
        {
            PlayClickSound();
            isPaused = false;
            Time.timeScale = 1f;

            if (GameManager.Instance != null)
                GameManager.Instance.RestartLevel();
        }

        private void ReturnToMainMenu()
        {
            PlayClickSound();
            isPaused = false;
            Time.timeScale = 1f;

            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToMainMenu();
        }

        // ------------------------------------------------------------------ //
        //  Settings sub-panel
        // ------------------------------------------------------------------ //

        private void OpenSettings()
        {
            PlayClickSound();

            if (settingsPanel != null)
                settingsPanel.SetActive(true);

            SyncSettingsUI();
        }

        private void CloseSettings()
        {
            PlayClickSound();

            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        private void SyncSettingsUI()
        {
            if (AudioManager.Instance == null) return;

            if (musicVolumeSlider != null)
                musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);

            if (muteToggle != null)
                muteToggle.SetIsOnWithoutNotify(AudioManager.Instance.IsMuted);
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicVolume(value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetSfxVolume(value);
        }

        private void OnMuteToggled(bool muted)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMuted(muted);
        }

        // ------------------------------------------------------------------ //
        //  Misc
        // ------------------------------------------------------------------ //

        private void UpdateControlHint()
        {
            if (controlHintText == null) return;

            bool mobile = InputManager.Instance != null && InputManager.Instance.IsMobile;
            controlHintText.text = mobile ? mobileHint : desktopHint;
        }

        private void PlayClickSound()
        {
            if (buttonClickSfx != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(buttonClickSfx);
        }
    }
}
