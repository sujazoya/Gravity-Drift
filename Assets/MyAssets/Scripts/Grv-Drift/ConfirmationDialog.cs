using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ConfirmationDialog : MonoBehaviour
{
    public static ConfirmationDialog Instance { get; private set; }

    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private System.Action _onConfirm;

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

    public static void Show(string title, string message, System.Action onConfirm)
    {
        Instance.ShowDialog(title, message, onConfirm);
    }

    public void ShowDialog(string title, string message, System.Action onConfirm)
    {
        titleText.text = title;
        messageText.text = message;
        _onConfirm = onConfirm;

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirm);

        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(HideDialog);

        dialogPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        _onConfirm?.Invoke();
        HideDialog();
    }

    private void HideDialog()
    {
        dialogPanel.SetActive(false);
    }
    public void ShowDialogInstance(string title, string message, System.Action onConfirm)
{
    titleText.text = title;
    messageText.text = message;
    _onConfirm = onConfirm;

    confirmButton.onClick.RemoveAllListeners();
    confirmButton.onClick.AddListener(OnConfirm);

    cancelButton.onClick.RemoveAllListeners();
    cancelButton.onClick.AddListener(HideDialog);

    dialogPanel.SetActive(true);
}

}