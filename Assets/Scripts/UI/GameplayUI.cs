using UnityEngine;
using UnityEngine.UI;

namespace RunAndGun
{
    /// <summary>
    /// Heads-up display rendered during gameplay: health bar, weapon info,
    /// score, lives, coins, and a toggleable boss health bar.
    /// </summary>
    public class GameplayUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Health Bar")]
        [SerializeField] private Image healthBarFill;
        [SerializeField] private float healthBarSmoothSpeed = 5f;
        [SerializeField] private Gradient healthBarGradient;

        [Header("Weapon Info")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Text ammoText;

        [Header("Score")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;

        [Header("Lives")]
        [SerializeField] private Text livesText;
        [SerializeField] private Image[] lifeIcons;

        [Header("Coins")]
        [SerializeField] private Text coinText;

        [Header("Boss Health Bar")]
        [SerializeField] private GameObject bossHealthBarRoot;
        [SerializeField] private Image bossHealthBarFill;
        [SerializeField] private Text bossNameText;
        [SerializeField] private float bossBarSmoothSpeed = 4f;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //

        private float targetHealthPercent = 1f;
        private float displayedHealthPercent = 1f;
        private float targetBossPercent;
        private float displayedBossPercent;
        private int displayedScore;
        private int targetScore;
        private float scoreCountSpeed = 800f;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void OnEnable()
        {
            SubscribeEvents();
            HideBossBar();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            SmoothHealthBar();
            SmoothBossBar();
            SmoothScoreCounter();
        }

        // ------------------------------------------------------------------ //
        //  Event wiring
        // ------------------------------------------------------------------ //

        private void SubscribeEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLivesChanged += HandleLivesChanged;
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
                ScoreManager.Instance.OnCoinsChanged += HandleCoinsChanged;
                ScoreManager.Instance.OnComboChanged += HandleComboChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLivesChanged -= HandleLivesChanged;
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
                ScoreManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                ScoreManager.Instance.OnComboChanged -= HandleComboChanged;
            }
        }

        // ------------------------------------------------------------------ //
        //  Health bar (player)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Call from player health component whenever HP changes.
        /// </summary>
        public void SetHealth(float currentHp, float maxHp)
        {
            targetHealthPercent = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        }

        private void SmoothHealthBar()
        {
            if (healthBarFill == null) return;

            displayedHealthPercent = Mathf.MoveTowards(
                displayedHealthPercent, targetHealthPercent,
                healthBarSmoothSpeed * Time.deltaTime);

            healthBarFill.fillAmount = displayedHealthPercent;

            if (healthBarGradient != null)
            {
                healthBarFill.color = healthBarGradient.Evaluate(displayedHealthPercent);
            }
        }

        // ------------------------------------------------------------------ //
        //  Weapon info
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Update the weapon icon and ammo readout. Pass ammo &lt; 0 for infinite display.
        /// </summary>
        public void SetWeaponInfo(Sprite icon, int currentAmmo, bool infiniteAmmo)
        {
            if (weaponIcon != null)
            {
                weaponIcon.sprite = icon;
                weaponIcon.enabled = icon != null;
            }

            if (ammoText != null)
            {
                ammoText.text = infiniteAmmo ? "\u221E" : currentAmmo.ToString();
            }
        }

        // ------------------------------------------------------------------ //
        //  Score
        // ------------------------------------------------------------------ //

        private void HandleScoreChanged(int newScore, int delta)
        {
            targetScore = newScore;
        }

        private void SmoothScoreCounter()
        {
            if (scoreText == null) return;

            if (displayedScore != targetScore)
            {
                float step = scoreCountSpeed * Time.deltaTime;
                if (displayedScore < targetScore)
                    displayedScore = Mathf.Min(displayedScore + Mathf.CeilToInt(step), targetScore);
                else
                    displayedScore = Mathf.Max(displayedScore - Mathf.CeilToInt(step), targetScore);

                scoreText.text = displayedScore.ToString("N0");
            }
        }

        private void HandleComboChanged(int multiplier)
        {
            if (comboText == null) return;

            if (multiplier > 1)
            {
                comboText.gameObject.SetActive(true);
                comboText.text = $"x{multiplier}";
            }
            else
            {
                comboText.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ //
        //  Lives
        // ------------------------------------------------------------------ //

        private void HandleLivesChanged(int lives)
        {
            if (livesText != null)
                livesText.text = lives.ToString();

            if (lifeIcons != null)
            {
                for (int i = 0; i < lifeIcons.Length; i++)
                {
                    if (lifeIcons[i] != null)
                        lifeIcons[i].enabled = i < lives;
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Coins
        // ------------------------------------------------------------------ //

        private void HandleCoinsChanged(int coins)
        {
            if (coinText != null)
                coinText.text = coins.ToString();
        }

        // ------------------------------------------------------------------ //
        //  Boss health bar
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Show the boss health bar with the given name.
        /// </summary>
        public void ShowBossBar(string bossName)
        {
            if (bossHealthBarRoot != null)
                bossHealthBarRoot.SetActive(true);

            if (bossNameText != null)
                bossNameText.text = bossName;

            targetBossPercent = 1f;
            displayedBossPercent = 1f;

            if (bossHealthBarFill != null)
                bossHealthBarFill.fillAmount = 1f;
        }

        /// <summary>
        /// Update the boss health bar percentage.
        /// </summary>
        public void SetBossHealth(float currentHp, float maxHp)
        {
            targetBossPercent = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        }

        /// <summary>
        /// Hide the boss health bar.
        /// </summary>
        public void HideBossBar()
        {
            if (bossHealthBarRoot != null)
                bossHealthBarRoot.SetActive(false);

            targetBossPercent = 0f;
            displayedBossPercent = 0f;
        }

        private void SmoothBossBar()
        {
            if (bossHealthBarFill == null) return;
            if (bossHealthBarRoot != null && !bossHealthBarRoot.activeSelf) return;

            displayedBossPercent = Mathf.MoveTowards(
                displayedBossPercent, targetBossPercent,
                bossBarSmoothSpeed * Time.deltaTime);

            bossHealthBarFill.fillAmount = displayedBossPercent;
        }
    }
}
