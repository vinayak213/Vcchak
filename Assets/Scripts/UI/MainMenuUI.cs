using UnityEngine;
using UnityEngine.UI;

namespace RunAndGun
{
    /// <summary>
    /// Main menu screen with play, level-select, settings, and quit buttons.
    /// Supports an animated background and a title entrance animation.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button levelSelectButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Title Animation")]
        [SerializeField] private RectTransform titleTransform;
        [SerializeField] private float titleBobAmplitude = 8f;
        [SerializeField] private float titleBobSpeed = 1.5f;
        [SerializeField] private float titleSlideInDuration = 0.6f;
        [SerializeField] private float titleSlideInDistance = 200f;

        [Header("Animated Background")]
        [SerializeField] private RawImage backgroundImage;
        [SerializeField] private Vector2 backgroundScrollSpeed = new Vector2(0.02f, 0.01f);

        [Header("Audio")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip buttonClickSfx;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //

        private Vector2 titleAnchoredOrigin;
        private float slideInElapsed;
        private bool slideInComplete;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (levelSelectButton != null)
                levelSelectButton.onClick.AddListener(OnLevelSelectClicked);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void Start()
        {
            // Hide sub-panels at start
            if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            // Title slide-in setup
            if (titleTransform != null)
            {
                titleAnchoredOrigin = titleTransform.anchoredPosition;
                titleTransform.anchoredPosition = titleAnchoredOrigin + Vector2.up * titleSlideInDistance;
                slideInElapsed = 0f;
                slideInComplete = false;
            }

            // Play menu music
            if (menuMusic != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(menuMusic);
            }

#if UNITY_WEBGL || UNITY_EDITOR
            // Hide quit button on platforms where Application.Quit does nothing
            if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
                quitButton.gameObject.SetActive(false);
#endif
        }

        private void Update()
        {
            AnimateTitle();
            AnimateBackground();
        }

        private void OnDestroy()
        {
            if (playButton != null)      playButton.onClick.RemoveListener(OnPlayClicked);
            if (levelSelectButton != null) levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            if (settingsButton != null)   settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (quitButton != null)       quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        // ------------------------------------------------------------------ //
        //  Animation
        // ------------------------------------------------------------------ //

        private void AnimateTitle()
        {
            if (titleTransform == null) return;

            // Slide in
            if (!slideInComplete)
            {
                slideInElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(slideInElapsed / titleSlideInDuration);
                // Ease-out quadratic
                float eased = 1f - (1f - t) * (1f - t);
                float yOffset = Mathf.Lerp(titleSlideInDistance, 0f, eased);
                titleTransform.anchoredPosition = titleAnchoredOrigin + Vector2.up * yOffset;

                if (t >= 1f)
                    slideInComplete = true;

                return;
            }

            // Bob idle
            float bob = Mathf.Sin(Time.unscaledTime * titleBobSpeed) * titleBobAmplitude;
            titleTransform.anchoredPosition = titleAnchoredOrigin + Vector2.up * bob;
        }

        private void AnimateBackground()
        {
            if (backgroundImage == null) return;

            Rect uvRect = backgroundImage.uvRect;
            uvRect.x += backgroundScrollSpeed.x * Time.unscaledDeltaTime;
            uvRect.y += backgroundScrollSpeed.y * Time.unscaledDeltaTime;
            backgroundImage.uvRect = uvRect;
        }

        // ------------------------------------------------------------------ //
        //  Button handlers
        // ------------------------------------------------------------------ //

        private void OnPlayClicked()
        {
            PlayClickSound();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartNewGame();
            }
        }

        private void OnLevelSelectClicked()
        {
            PlayClickSound();

            if (levelSelectPanel != null)
                levelSelectPanel.SetActive(true);
        }

        private void OnSettingsClicked()
        {
            PlayClickSound();

            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        private void OnQuitClicked()
        {
            PlayClickSound();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlayClickSound()
        {
            if (buttonClickSfx != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(buttonClickSfx);
            }
        }
    }
}
