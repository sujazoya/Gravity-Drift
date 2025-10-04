using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class TypeWriterTextWithTMP : MonoBehaviour
{
 [SerializeField] private AdvancedTMPTypewriter typewriter;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private Button nextButton;

    private string[] dialoguePages =
    {
        "Hello, traveler! Welcome to our village.",
        "We've been expecting you for a long time.",
        "The ancient prophecy speaks of your arrival..."
    };

    private string speakerName = "Elder";

    void Start()
    {
        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        typewriter.SetText(dialoguePages);
        typewriter.OnTypingCompleted.AddListener(OnPageComplete);
        typewriter.OnCharacterTyped.AddListener(OnCharacterTyped);

        nextButton.onClick.AddListener(OnNextButtonClick);
        nextButton.gameObject.SetActive(false);

        typewriter.StartTyping();
    }

    private void OnPageComplete()
    {
        nextButton.gameObject.SetActive(true);
    }

    private void OnCharacterTyped(string character)
    {
        // Example: play effects on punctuation
        if (character == "!" || character == "?")
            Debug.Log("Strong emotion!");
    }

    private void OnNextButtonClick()
    {
        nextButton.gameObject.SetActive(false);
        typewriter.NextPage();
    }

    // Quick one-time usage example
    public void ShowQuickMessage(string message)
    {
        AdvancedTMPTypewriter.TypeText(typewriter.GetComponent<TMP_Text>(), message, 50f, () =>
        {
            Debug.Log("Quick message done!");
        });
    }
}