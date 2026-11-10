using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Menu
{
    public interface IMainMenuEventsHandler
    {
        public Action<string> OnPlayButtonClicked { get; set; }
    }
    public class MainMenu : MonoBehaviour, IMainMenuEventsHandler
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button playButton;

        [Header("Settings")]
        [SerializeField] private int maxNameLength = 16;
        [SerializeField] private string defaultName = "Player";
        
        public Action<string> OnPlayButtonClicked { get; set; }
        
        public void Setup()
        {
            menuPanel.SetActive(true);
            nameInputField.characterLimit = maxNameLength;
            nameInputField.onValueChanged.AddListener(OnNameChanged);

            string savedName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(savedName))
            {
                nameInputField.text = savedName;
            }

            UpdatePlayButtonState();
            playButton.onClick.AddListener(OnPlayClicked);

            nameInputField.Select();
            nameInputField.ActivateInputField();
        }
        
        private void OnNameChanged(string newName)
        {
            UpdatePlayButtonState();
        }

        private void OnPlayClicked()
        {
            string trimmedName = nameInputField.text.Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                trimmedName = defaultName;
            }

            PlayerPrefs.SetString("PlayerName", trimmedName);
            PlayerPrefs.Save();

            OnPlayButtonClicked?.Invoke(trimmedName);
        }
        
        private void UpdatePlayButtonState()
        {
            playButton.interactable = true;
        }
        
        public void Clear()
        {
            menuPanel.SetActive(false);

            nameInputField.onValueChanged.RemoveListener(OnNameChanged);
            playButton.onClick.RemoveListener(OnPlayClicked);
        }
    }
}