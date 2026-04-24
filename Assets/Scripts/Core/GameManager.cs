using UnityEngine;
using UnityEngine.SceneManagement;

namespace LeiTing.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        [SerializeField] private GameState currentState = GameState.Boot;
        [SerializeField] private int score;

        public GameState CurrentState => currentState;
        public int Score => score;

        public void Initialize()
        {
            currentState = GameState.Ready;
            score = 0;
        }

        public void StartGame()
        {
            currentState = GameState.Playing;
        }

        public void AddScore(int amount)
        {
            score += Mathf.Max(0, amount);
        }

        public void WinGame()
        {
            currentState = GameState.Victory;
        }

        public void LoseGame()
        {
            currentState = GameState.Defeat;
        }

        public void RestartCurrentScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
