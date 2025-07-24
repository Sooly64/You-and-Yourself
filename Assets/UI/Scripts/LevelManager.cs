using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string MainMenuSceneName = "MainMenu";
    [SerializeField] private string[] LevelSceneNames = {"SampleScene"};
    
    /* Level Transition Functions */
    public void LoadMainMenu() {
        SceneManager.LoadScene(MainMenuSceneName);
    }
    public void LoadLevel(int levelNumber) {
        if(levelNumber < 1 || levelNumber > LevelSceneNames.Length) {
            Debug.LogError("Invalid level number: " + levelNumber);
            return;
        }
        SceneManager.LoadScene(LevelSceneNames[levelNumber - 1]);
    }
    public void goToNextLevel() {
        string currentSceneName = SceneManager.GetActiveScene().name;
        /* Get the next level number (-1 if not found) */
        int currentLevel = getLevelNumber(currentSceneName); 
        if(currentLevel < 0) {
            Debug.LogError("Currently in a non-level scene: " + currentSceneName);
            return;
        }
        /* if we are on the last level, go to the main menu. (To be overhauled later) */
        if(currentLevel >= LevelSceneNames.Length) {
            LoadMainMenu();
        } else {
            LoadLevel(currentLevel + 1);
        }
    }
    private int getLevelNumber(string sceneName) {
        for(int i = 0; i < LevelSceneNames.Length; i++) {
            if(LevelSceneNames[i] == sceneName) {
                return i + 1;
            }
        }
        return -1;
    }
}
