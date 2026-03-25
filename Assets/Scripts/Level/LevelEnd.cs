using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace RunAndGun
{
    /// <summary>
    /// Level completion trigger. When the player enters this zone, the level
    /// is marked complete with a score tally, star rating, and next-level unlock.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelEnd : MonoBehaviour
    {
        [Header("Star Rating Thresholds")]
        [Tooltip("Time in seconds to earn the time star (finish under this).")]
        [SerializeField] private float timeThreshold = 120f;
        [Tooltip("Minimum score to earn the score star.")]
        [SerializeField] private int scoreThreshold = 5000;
        [Tooltip("Minimum health percentage (0-1) to earn the health star.")]
        [SerializeField] [Range(0f, 1f)] private float healthThreshold = 0.5f;

        [Header("Score Tally")]
        [SerializeField] private int timeBonusPerSecond = 10;
        [SerializeField] private int healthBonusPerPoint = 5;
        [SerializeField] private int completionBonus = 1000;

        [Header("Tally Animation")]
        [SerializeField] private float tallyTickInterval = 0.02f;
        [SerializeField] private int tallyPointsPerTick = 50;
        [SerializeField] private AudioClip tallyTickSound;
        [SerializeField] private AudioClip tallyCompleteSound;

        [Header("Visuals")]
        [SerializeField] private GameObject levelEndEffect;

        [Header("Events")]
        [SerializeField] private UnityEvent onLevelComplete;
        [SerializeField] private UnityEvent<int> onStarRatingCalculated;
        [SerializeField] private UnityEvent<int> onFinalScoreCalculated;
        [SerializeField] private UnityEvent<int> onTallyTick;

        private bool triggered;
        private int starRating;
        private int finalScore;
        private int tallyScore;

        public int StarRating => starRating;
        public int FinalScore => finalScore;
        public bool IsTriggered => triggered;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered) return;
            if (!other.CompareTag("Player")) return;

            triggered = true;
            CompleteLevelSequence(other.gameObject);
        }

        private void CompleteLevelSequence(GameObject player)
        {
            // Show visual effect
            if (levelEndEffect != null)
            {
                levelEndEffect.SetActive(true);
            }

            // Calculate star rating
            starRating = CalculateStarRating(player);
            onStarRatingCalculated?.Invoke(starRating);

            // Calculate final score
            finalScore = CalculateFinalScore(player);
            onFinalScoreCalculated?.Invoke(finalScore);

            // Add score to game manager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(finalScore);
            }

            // Start tally animation
            StartCoroutine(ScoreTallyRoutine());

            // Fire level complete event
            onLevelComplete?.Invoke();

            // Notify level manager
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ReachLevelEnd();
            }

            // Save star rating
            SaveStarRating();
        }

        private int CalculateStarRating(GameObject player)
        {
            int stars = 0;

            // Time star
            float elapsed = LevelManager.Instance != null ? LevelManager.Instance.ElapsedTime : 0f;
            if (elapsed > 0f && elapsed <= timeThreshold)
            {
                stars++;
            }

            // Score star
            int currentScore = GameManager.Instance != null ? GameManager.Instance.TotalScore : 0;
            if (currentScore >= scoreThreshold)
            {
                stars++;
            }

            // Health star
            IDamageable playerHealth = player.GetComponent<IDamageable>();
            if (playerHealth != null && playerHealth.MaxHealth > 0f)
            {
                float healthPercent = playerHealth.CurrentHealth / playerHealth.MaxHealth;
                if (healthPercent >= healthThreshold)
                {
                    stars++;
                }
            }
            else
            {
                // If no health component, check lives
                if (GameManager.Instance != null &&
                    GameManager.Instance.Lives >= GameManager.Instance.MaxLives)
                {
                    stars++;
                }
            }

            return Mathf.Clamp(stars, 0, 3);
        }

        private int CalculateFinalScore(GameObject player)
        {
            int score = completionBonus;

            // Time bonus: remaining time converted to points
            float elapsed = LevelManager.Instance != null ? LevelManager.Instance.ElapsedTime : 0f;
            float timeRemaining = Mathf.Max(0f, timeThreshold - elapsed);
            score += Mathf.RoundToInt(timeRemaining) * timeBonusPerSecond;

            // Health bonus
            IDamageable playerHealth = player.GetComponent<IDamageable>();
            if (playerHealth != null)
            {
                score += Mathf.RoundToInt(playerHealth.CurrentHealth) * healthBonusPerPoint;
            }

            return score;
        }

        private IEnumerator ScoreTallyRoutine()
        {
            tallyScore = 0;
            WaitForSeconds tickWait = new WaitForSeconds(tallyTickInterval);

            while (tallyScore < finalScore)
            {
                int increment = Mathf.Min(tallyPointsPerTick, finalScore - tallyScore);
                tallyScore += increment;

                onTallyTick?.Invoke(tallyScore);

                if (tallyTickSound != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(tallyTickSound, 0.3f);
                }

                yield return tickWait;
            }

            tallyScore = finalScore;
            onTallyTick?.Invoke(tallyScore);

            if (tallyCompleteSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(tallyCompleteSound);
            }
        }

        private void SaveStarRating()
        {
            int levelIndex = LevelManager.Instance != null ? LevelManager.Instance.LevelIndex : 0;
            string key = $"RG_Stars_{levelIndex}";
            int existingStars = PlayerPrefs.GetInt(key, 0);

            if (starRating > existingStars)
            {
                PlayerPrefs.SetInt(key, starRating);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Get the best star rating for a level from saved data.
        /// </summary>
        public static int GetSavedStarRating(int levelIndex)
        {
            return PlayerPrefs.GetInt($"RG_Stars_{levelIndex}", 0);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.84f, 0f, 0.4f);

            Collider2D col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.offset, box.size);
                Gizmos.DrawWireCube(box.offset, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }

            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "LEVEL END");
        }
#endif
    }
}
