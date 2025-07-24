using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    private LevelManager levelManager;

    private void Awake() {
        levelManager = FindObjectOfType<LevelManager>();
    }
    
    public void StartGame() {
        if (levelManager != null) {
            levelManager.LoadLevel(1);
        } else {
            Debug.LogError("Cannot start game: LevelManager reference is missing");
        }
    }

    public void QuitGame() {
        Application.Quit();
    }
}
