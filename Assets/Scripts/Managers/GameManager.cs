using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunAndGun
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        LevelComplete
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Lives")]
        [SerializeField] private int maxLives = 3;
        [SerializeField] private int startingLives = 3;

        [Header("Scenes")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string[] levelScenes;

        [Header("Settings")]
        [SerializeField] private float gameOverDelay = 2f;

        private const string SaveKeyUnlockedLevel = "RG_UnlockedLevel";
        private const string SaveKeyTotalScore = "RG_TotalScore";
        private const string SaveKeyLives = "RG_Lives";

        private GameState currentState = GameState.MainMenu;
        private int currentLevelIndex;
        private int lives;
        private int totalScore;
        private int highestUnlockedLevel;

        public GameState CurrentState => currentState;
        public int CurrentLevelIndex => currentLevelIndex;
        public int Lives => lives;
        public int MaxLives => maxLives;
        public int TotalScore => totalScore;
        public int HighestUnlockedLevel => highestUnlockedLevel;
        public int LevelCount => levelScenes != null ? levelScenes.Length : 0;

        public event Action<GameState, GameState> OnGameStateChanged;
        public event Action<int> OnLivesChanged;
        public event Action<int> OnScoreChanged;
        public event Action<int> OnLevelUnlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == mainMenuScene)
            {
                SetState(GameState.MainMenu);
            }
        }

        public void SetState(GameState newState)
        {
            if (currentState == newState) return;

            GameState previousState = currentState;
            currentState = newState;

            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 1f;
                    break;
                case GameState.LevelComplete:
                    Time.timeScale = 1f;
                    break;
                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    break;
            }

            OnGameStateChanged?.Invoke(previousState, newState);
        }

        public void StartNewGame()
        {
            lives = startingLives;
            totalScore = 0;
            currentLevelIndex = 0;
            OnLivesChanged?.Invoke(lives);
            OnScoreChanged?.Invoke(totalScore);
            LoadLevel(0);
        }

        public void LoadLevel(int levelIndex)
        {
            if (levelScenes == null || levelIndex < 0 || levelIndex >= levelScenes.Length)
            {
                Debug.LogWarning($"[GameManager] Invalid level index: {levelIndex}");
                return;
            }

            currentLevelIndex = levelIndex;
            SceneManager.LoadScene(levelScenes[levelIndex]);
            SetState(GameState.Playing);
        }

        public void CompleteLevel()
        {
            SetState(GameState.LevelComplete);

            int nextLevel = currentLevelIndex + 1;
            if (nextLevel > highestUnlockedLevel && nextLevel < LevelCount)
            {
                highestUnlockedLevel = nextLevel;
                OnLevelUnlocked?.Invoke(highestUnlockedLevel);
            }

            SaveProgress();
        }

        public void LoadNextLevel()
        {
            int nextLevel = currentLevelIndex + 1;
            if (nextLevel < LevelCount)
            {
                LoadLevel(nextLevel);
            }
            else
            {
                ReturnToMainMenu();
            }
        }

        public void RestartLevel()
        {
            SetState(GameState.Playing);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            SetState(GameState.MainMenu);
            SceneManager.LoadScene(mainMenuScene);
        }

        public void AddScore(int points)
        {
            totalScore += points;
            OnScoreChanged?.Invoke(totalScore);
        }

        public void LoseLife()
        {
            lives--;
            OnLivesChanged?.Invoke(lives);

            if (lives <= 0)
            {
                Invoke(nameof(TriggerGameOver), gameOverDelay);
            }
        }

        public void GainLife()
        {
            if (lives < maxLives)
            {
                lives++;
                OnLivesChanged?.Invoke(lives);
            }
        }

        private void TriggerGameOver()
        {
            SetState(GameState.GameOver);
        }

        public void SaveProgress()
        {
            PlayerPrefs.SetInt(SaveKeyUnlockedLevel, highestUnlockedLevel);
            PlayerPrefs.SetInt(SaveKeyTotalScore, totalScore);
            PlayerPrefs.SetInt(SaveKeyLives, lives);
            PlayerPrefs.Save();
        }

        public void LoadProgress()
        {
            highestUnlockedLevel = PlayerPrefs.GetInt(SaveKeyUnlockedLevel, 0);
            totalScore = PlayerPrefs.GetInt(SaveKeyTotalScore, 0);
            lives = PlayerPrefs.GetInt(SaveKeyLives, startingLives);
        }

        public void ResetProgress()
        {
            PlayerPrefs.DeleteKey(SaveKeyUnlockedLevel);
            PlayerPrefs.DeleteKey(SaveKeyTotalScore);
            PlayerPrefs.DeleteKey(SaveKeyLives);
            PlayerPrefs.Save();

            highestUnlockedLevel = 0;
            totalScore = 0;
            lives = startingLives;
        }

        public bool IsLevelUnlocked(int levelIndex)
        {
            return levelIndex <= highestUnlockedLevel;
        }

        public string GetLevelSceneName(int levelIndex)
        {
            if (levelScenes == null || levelIndex < 0 || levelIndex >= levelScenes.Length)
                return string.Empty;
            return levelScenes[levelIndex];
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveProgress();
            }
        }

        private void OnApplicationQuit()
        {
            SaveProgress();
        }
    }
}
