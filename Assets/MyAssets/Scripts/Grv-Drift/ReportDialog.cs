using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace zoya.game
{



    public class ReportDialog : MonoBehaviour
    {
        public static ReportDialog Instance { get; private set; }

        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Dropdown reasonDropdown;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button cancelButton;

        private int _reportedPlayerId;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            dialogPanel.SetActive(false);
        }

        public static void Show(string playerName, int playerId)
        {
            Instance.ShowDialog(playerName, playerId);
        }

        private void ShowDialog(string playerName, int playerId)
        {
            playerNameText.text = $"Report {playerName}";
            _reportedPlayerId = playerId;

            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);

            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HideDialog);

            dialogPanel.SetActive(true);
        }

        private void OnSubmit()
        {
            string reason = reasonDropdown.options[reasonDropdown.value].text;
            Debug.Log($"Reporting player {_reportedPlayerId} for: {reason}");
            HideDialog();
        }

        private void HideDialog()
        {
            dialogPanel.SetActive(false);
        }
    }
}