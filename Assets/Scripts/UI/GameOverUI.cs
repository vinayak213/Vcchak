using UnityEngine;
using UnityEngine.UI;

namespace RunAndGun
{
    public class GameOverUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject gameOverPanel;

        [Header("Score Display")]
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text highScoreLabel;
        [SerializeField] private Text coinsLabel;

        [Header("High Score Indicator")]
        [SerializeField] private GameObject newHighScoreIndicator;
        [SerializeField] private float indicatorPulseSpeed = 2f;
        [SerializeField] private float indicatorPulseMin = 0.7f;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Audio")]
        [SerializeField] private AudioClip gameOverSfx;
        [SerializeField] private AudioClip newHighScoreSfx;
        [SerializeField] private AudioClip buttonClickSfx;

        private bool isNewHighScore;
        private CanvasGroup indicatorCanvasGroup;

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
            if (newHighScoreIndicator != null)
            {
                indicatorCanvasGroup = newHighScoreIndicator.GetComponent<CanvasGroup>();
                if (indicatorCanvasGroup == null) indicatorCanvasGroup = newHighScoreIndicator.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable() { if (GameManager.Instance != null) GameManager.Instance.OnGameStateChanged += HandleStateChanged; }
        private void OnDisable() { if (GameManager.Instance != null) GameManager.Instance.OnGameStateChanged -= HandleStateChanged; }
        private void Start() { if (gameOverPanel != null) gameOverPanel.SetActive(false); }
        private void Update() { PulseHighScoreIndicator(); }

        private void OnDestroy()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(OnRetry);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenu);
        }

        private void HandleStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.GameOver) Show();
            else if (previous == GameState.GameOver) Hide();
        }

        private void Show()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            int coins = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentCoins : 0;
            int levelIndex = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 0;
            if (scoreLabel != null) scoreLabel.text = $"Score: {score:N0}";
            if (coinsLabel != null) coinsLabel.text = $"Coins: {coins}";
            isNewHighScore = ScoreManager.Instance != null && ScoreManager.Instance.TrySaveHighScore(levelIndex);
            int displayHigh = ScoreManager.Instance != null ? ScoreManager.Instance.GetHighScore(levelIndex) : 0;
            if (highScoreLabel != null) highScoreLabel.text = $"Best: {displayHigh:N0}";
            if (newHighScoreIndicator != null) newHighScoreIndicator.SetActive(isNewHighScore);
            if (isNewHighScore && newHighScoreSfx != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(newHighScoreSfx);
            else if (gameOverSfx != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(gameOverSfx);
        }

        private void Hide() { if (gameOverPanel != null) gameOverPanel.SetActive(false); }

        private void PulseHighScoreIndicator()
        {
            if (!isNewHighScore || indicatorCanvasGroup == null) return;
            float alpha = Mathf.Lerp(indicatorPulseMin, 1f, (Mathf.Sin(Time.unscaledTime * indicatorPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
            indicatorCanvasGroup.alpha = alpha;
        }

        private void OnRetry() { PlayClickSound(); if (GameManager.Instance != null) GameManager.Instance.RestartLevel(); }
        private void OnMainMenu() { PlayClickSound(); if (GameManager.Instance != null) GameManager.Instance.ReturnToMainMenu(); }
        private void PlayClickSound() { if (buttonClickSfx != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(buttonClickSfx); }
    }
}
