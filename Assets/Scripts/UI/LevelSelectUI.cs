using UnityEngine;
using UnityEngine.UI;

namespace RunAndGun
{
    /// <summary>
    /// Populates a grid of level buttons with locked/unlocked visuals,
    /// star ratings, and an optional level preview image.
    /// </summary>
    public class LevelSelectUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("References")]
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject levelButtonPrefab;
        [SerializeField] private Button backButton;

        [Header("Level Data")]
        [SerializeField] private LevelEntry[] levels;

        [Header("Preview")]
        [SerializeField] private Image previewImage;
        [SerializeField] private Text previewLevelName;
        [SerializeField] private Text previewHighScore;

        [Header("Audio")]
        [SerializeField] private AudioClip buttonClickSfx;
        [SerializeField] private AudioClip lockedSfx;

        // ------------------------------------------------------------------ //
        //  Types
        // ------------------------------------------------------------------ //

        [System.Serializable]
        public class LevelEntry
        {
            public string displayName;
            public Sprite previewSprite;
            public Sprite starFilledSprite;
            public Sprite starEmptySprite;
        }

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //

        private LevelButtonUI[] spawnedButtons;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnEnable()
        {
            BuildGrid();
            ClearPreview();
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBackClicked);
        }

        // ------------------------------------------------------------------ //
        //  Grid construction
        // ------------------------------------------------------------------ //

        private void BuildGrid()
        {
            // Destroy old buttons
            if (spawnedButtons != null)
            {
                for (int i = 0; i < spawnedButtons.Length; i++)
                {
                    if (spawnedButtons[i] != null)
                        Destroy(spawnedButtons[i].gameObject);
                }
            }

            int levelCount = GameManager.Instance != null
                ? GameManager.Instance.LevelCount
                : (levels != null ? levels.Length : 0);

            spawnedButtons = new LevelButtonUI[levelCount];

            for (int i = 0; i < levelCount; i++)
            {
                if (levelButtonPrefab == null || buttonContainer == null) break;

                GameObject go = Instantiate(levelButtonPrefab, buttonContainer);
                LevelButtonUI btn = go.GetComponent<LevelButtonUI>();

                if (btn == null)
                    btn = go.AddComponent<LevelButtonUI>();

                bool unlocked = GameManager.Instance != null && GameManager.Instance.IsLevelUnlocked(i);
                int stars = GetStarRating(i);
                string displayName = (levels != null && i < levels.Length)
                    ? levels[i].displayName
                    : $"Level {i + 1}";

                Sprite starFilled = (levels != null && i < levels.Length) ? levels[i].starFilledSprite : null;
                Sprite starEmpty = (levels != null && i < levels.Length) ? levels[i].starEmptySprite : null;

                btn.Setup(i, displayName, unlocked, stars, starFilled, starEmpty);
                int capturedIndex = i;
                btn.Button.onClick.AddListener(() => OnLevelButtonClicked(capturedIndex));

                spawnedButtons[i] = btn;
            }
        }

        // ------------------------------------------------------------------ //
        //  Button handling
        // ------------------------------------------------------------------ //

        private void OnLevelButtonClicked(int levelIndex)
        {
            bool unlocked = GameManager.Instance != null && GameManager.Instance.IsLevelUnlocked(levelIndex);

            if (!unlocked)
            {
                if (lockedSfx != null && AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(lockedSfx);
                return;
            }

            PlayClickSound();
            ShowPreview(levelIndex);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadLevel(levelIndex);
            }
        }

        private void ShowPreview(int levelIndex)
        {
            if (levels == null || levelIndex >= levels.Length) return;

            LevelEntry entry = levels[levelIndex];

            if (previewImage != null && entry.previewSprite != null)
            {
                previewImage.sprite = entry.previewSprite;
                previewImage.enabled = true;
            }

            if (previewLevelName != null)
                previewLevelName.text = entry.displayName;

            if (previewHighScore != null && ScoreManager.Instance != null)
                previewHighScore.text = $"Best: {ScoreManager.Instance.GetHighScore(levelIndex)}";
        }

        private void ClearPreview()
        {
            if (previewImage != null)
                previewImage.enabled = false;

            if (previewLevelName != null)
                previewLevelName.text = string.Empty;

            if (previewHighScore != null)
                previewHighScore.text = string.Empty;
        }

        private void OnBackClicked()
        {
            PlayClickSound();
            gameObject.SetActive(false);
        }

        private int GetStarRating(int levelIndex)
        {
            // Star rating is derived from high score thresholds.
            if (ScoreManager.Instance == null) return 0;

            int highScore = ScoreManager.Instance.GetHighScore(levelIndex);
            if (highScore <= 0) return 0;
            if (highScore >= 5000) return 3;
            if (highScore >= 2000) return 2;
            return 1;
        }

        private void PlayClickSound()
        {
            if (buttonClickSfx != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(buttonClickSfx);
        }
    }

    /// <summary>
    /// Attached to each instantiated level button in the selection grid.
    /// </summary>
    public class LevelButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text labelText;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Image[] starImages;
        [SerializeField] private CanvasGroup canvasGroup;

        public Button Button
        {
            get
            {
                if (button == null)
                    button = GetComponent<Button>();
                return button;
            }
        }

        public void Setup(int levelIndex, string displayName, bool unlocked, int starCount,
                          Sprite starFilled, Sprite starEmpty)
        {
            if (labelText != null)
                labelText.text = displayName;

            // Lock state
            if (lockIcon != null)
                lockIcon.gameObject.SetActive(!unlocked);

            if (canvasGroup != null)
                canvasGroup.alpha = unlocked ? 1f : 0.5f;

            // Star display
            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    if (starImages[i] == null) continue;

                    bool filled = i < starCount;
                    if (filled && starFilled != null)
                        starImages[i].sprite = starFilled;
                    else if (!filled && starEmpty != null)
                        starImages[i].sprite = starEmpty;

                    starImages[i].enabled = unlocked;
                }
            }
        }
    }
}
