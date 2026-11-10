using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class logicScript : MonoBehaviour
{
    public int playerScore;
    public Text scoreText;
    public Text shadowScoreText;
    // public Text highScoreText;
    public GameObject gameOverScreen;

    // void Start()
    // {
    //     highScoreText.text = "High Score: " + GetHighScore().ToString();
    // }

    [ContextMenu("Increase Score")]
    public void addScore(int scoreToAdd)
    {
        playerScore += scoreToAdd;
        scoreText.text = playerScore.ToString();
        shadowScoreText.text = playerScore.ToString();
    }

    public void restartGame()
    {
        Debug.Log("Restart game called");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void gameOver()
    {
        gameOverScreen.SetActive(true);

        // if (playerScore > GetHighScore())
        // {
        //     SetHighScore(playerScore);
        // }
    }

    // public int GetHighScore()
    // {
    //     return PlayerPrefs.GetInt("HighScore", 0);
    // }

    // public void SetHighScore(int newHighScore)
    // {
    //     PlayerPrefs.SetInt("HighScore", newHighScore);
    //     PlayerPrefs.Save();
    // }
}
