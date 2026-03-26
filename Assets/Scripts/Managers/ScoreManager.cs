using System;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Tracks current score, high scores per level, coin collection,
    /// and a combo multiplier system. Persists high scores via PlayerPrefs.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Combo")]
        [SerializeField] private float comboWindowSeconds = 2f;
        [SerializeField] private int maxComboMultiplier = 10;

        [Header("Score Popup")]
        [SerializeField] private GameObject scorePopupPrefab;
        [SerializeField] private Vector2 popupOffset = new Vector2(0f, 1f);

        private const string PrefKeyHighScore = "RG_HighScore_Level_";
        private const string PrefKeyTotalCoins = "RG_TotalCoins";

        private int currentScore;
        private int currentCoins;
        private int totalCoinsCollected;
        private int comboMultiplier;
        private int comboKillCount;
        private float lastScoreTime;

        // ----- Public properties ----- //

        public int CurrentScore => currentScore;
        public int CurrentCoins => currentCoins;
        public int TotalCoinsCollected => totalCoinsCollected;
        public int ComboMultiplier => comboMultiplier;

        // ----- Events ----- //

        /// <summary>Args: new score, delta (points just earned including multiplier).</summary>
        public event Action<int, int> OnScoreChanged;
        public event Action<int> OnCoinsChanged;
        public event Action<int> OnComboChanged;
        public event Action<int> OnNewHighScore;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            totalCoinsCollected = PlayerPrefs.GetInt(PrefKeyTotalCoins, 0);
        }

        private void Update()
        {
            if (comboMultiplier > 1 && Time.time - lastScoreTime > comboWindowSeconds)
            {
                ResetCombo();
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Call at the start of each level to zero out running totals.
        /// </summary>
        public void ResetForLevel()
        {
            currentScore = 0;
            currentCoins = 0;
            comboMultiplier = 1;
            comboKillCount = 0;
            lastScoreTime = -999f;

            OnScoreChanged?.Invoke(currentScore, 0);
            OnCoinsChanged?.Invoke(currentCoins);
            OnComboChanged?.Invoke(comboMultiplier);
        }

        /// <summary>
        /// Add base points (multiplied by the current combo). Optionally spawns a popup.
        /// </summary>
        public void AddScore(int basePoints, Vector3? worldPosition = null)
        {
            if (basePoints <= 0) return;

            int earned = basePoints * comboMultiplier;
            currentScore += earned;
            lastScoreTime = Time.time;

            // Advance combo
            comboKillCount++;
            int newMultiplier = Mathf.Min(1 + comboKillCount / 3, maxComboMultiplier);
            if (newMultiplier != comboMultiplier)
            {
                comboMultiplier = newMultiplier;
                OnComboChanged?.Invoke(comboMultiplier);
            }

            OnScoreChanged?.Invoke(currentScore, earned);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(earned);
            }

            if (worldPosition.HasValue)
            {
                SpawnScorePopup(earned, worldPosition.Value);
            }
        }

        /// <summary>
        /// Collect a coin worth the given value. Coins track separately from score.
        /// </summary>
        public void CollectCoin(int value = 1, Vector3? worldPosition = null)
        {
            currentCoins += value;
            totalCoinsCollected += value;
            OnCoinsChanged?.Invoke(currentCoins);

            // Coins also grant score
            AddScore(value * 10, worldPosition);

            PlayerPrefs.SetInt(PrefKeyTotalCoins, totalCoinsCollected);
        }

        /// <summary>
        /// Save the current score as the level high score if it exceeds the previous best.
        /// Returns true if a new high score was set.
        /// </summary>
        public bool TrySaveHighScore(int levelIndex)
        {
            string key = PrefKeyHighScore + levelIndex;
            int previous = PlayerPrefs.GetInt(key, 0);

            if (currentScore > previous)
            {
                PlayerPrefs.SetInt(key, currentScore);
                PlayerPrefs.Save();
                OnNewHighScore?.Invoke(currentScore);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Retrieve the saved high score for a given level.
        /// </summary>
        public int GetHighScore(int levelIndex)
        {
            return PlayerPrefs.GetInt(PrefKeyHighScore + levelIndex, 0);
        }

        /// <summary>
        /// Clear all saved high scores (used by reset-progress flow).
        /// </summary>
        public void ClearAllHighScores(int levelCount)
        {
            for (int i = 0; i < levelCount; i++)
            {
                PlayerPrefs.DeleteKey(PrefKeyHighScore + i);
            }

            PlayerPrefs.DeleteKey(PrefKeyTotalCoins);
            PlayerPrefs.Save();
            totalCoinsCollected = 0;
        }

        // ------------------------------------------------------------------ //
        //  Internal
        // ------------------------------------------------------------------ //

        private void ResetCombo()
        {
            comboMultiplier = 1;
            comboKillCount = 0;
            OnComboChanged?.Invoke(comboMultiplier);
        }

        private void SpawnScorePopup(int points, Vector3 worldPosition)
        {
            if (scorePopupPrefab == null) return;

            Vector3 pos = worldPosition + (Vector3)popupOffset;
            GameObject popup = Instantiate(scorePopupPrefab, pos, Quaternion.identity);

            ScorePopup popupComponent = popup.GetComponent<ScorePopup>();
            if (popupComponent != null)
            {
                popupComponent.Initialize(points, comboMultiplier);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }

    /// <summary>
    /// Lightweight component placed on the score popup prefab.
    /// Animates upward and fades out, then self-destructs.
    /// </summary>
    public class ScorePopup : MonoBehaviour
    {
        [SerializeField] private TextMesh textMesh;
        [SerializeField] private float riseDuration = 0.8f;
        [SerializeField] private float riseDistance = 1.2f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private float elapsed;
        private Vector3 startPosition;
        private Color baseColor;

        public void Initialize(int points, int combo)
        {
            if (textMesh == null)
                textMesh = GetComponent<TextMesh>();

            if (textMesh == null)
                textMesh = GetComponentInChildren<TextMesh>();

            if (textMesh != null)
            {
                string label = combo > 1
                    ? $"+{points}  x{combo}"
                    : $"+{points}";
                textMesh.text = label;
                baseColor = textMesh.color;
            }

            startPosition = transform.position;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);

            transform.position = startPosition + Vector3.up * (riseDistance * t);

            if (textMesh != null)
            {
                Color c = baseColor;
                c.a = fadeCurve.Evaluate(t);
                textMesh.color = c;
            }

            if (elapsed >= riseDuration)
            {
                Destroy(gameObject);
            }
        }
    }
}
