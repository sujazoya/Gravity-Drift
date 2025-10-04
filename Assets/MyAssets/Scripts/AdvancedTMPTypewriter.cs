using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TypingEvent : UnityEvent<string> { }

public class AdvancedTMPTypewriter : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] public TMP_Text targetText;
    [SerializeField] private bool startOnEnable = true;

    [Header("Typing Settings")]
    [SerializeField] public float charactersPerSecond = 30f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typeSounds;
    [SerializeField] private float soundVolume = 0.5f;
    [SerializeField] private bool randomPitch = true;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.1f;

    [Header("Visual Effects")]
    [SerializeField] private bool useShakeEffect = false;
    [SerializeField] private float shakeIntensity = 1f;
    [SerializeField] private bool useColorEffect = false;
    [SerializeField] private Color typingColor = Color.yellow;
    [SerializeField] private Color finalColor = Color.white;

    [Header("Cursor Settings")]
    [SerializeField] private bool showCursor = true;
    [SerializeField] private string cursorChar = "|";
    [SerializeField] private float cursorBlinkSpeed = 0.5f;

    [Header("Events")]
    public TypingEvent OnCharacterTyped;
    public UnityEvent OnTypingStarted;
    public UnityEvent OnTypingCompleted;
    public UnityEvent OnTypingPaused;
    public UnityEvent OnTypingResumed;

    // Runtime variables
    private string[] textPages;
    private int currentPageIndex = 0;
    private bool isTyping = false;
    private bool isPaused = false;
    private Coroutine typingCoroutine;
    private Coroutine cursorCoroutine;
    private Vector3 originalPosition;
    private Color originalColor;

    // Character delay multipliers
    private readonly Dictionary<char, float> delayMultipliers = new Dictionary<char, float>
    {
        { '.', 3f }, { '!', 3f }, { '?', 3f }, { ',', 2f }, { ';', 2f }, { ':', 2f },
        { '\n', 2f }, { '\r', 2f }, { ' ', 0.5f }
    };

    void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (targetText != null)
        {
            originalPosition = targetText.rectTransform.localPosition;
            originalColor = targetText.color;
        }
    }

    void OnEnable()
    {
        if (startOnEnable && !isTyping)
        {
            StartTyping();
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        isTyping = false;
        isPaused = false;
    }

    // --- Public API ---

    public void SetText(string text) => SetText(new string[] { text });

    public void SetText(string[] pages)
    {
        textPages = pages;
        currentPageIndex = 0;
        targetText.text = textPages[0];
        targetText.maxVisibleCharacters = 0;
    }

    public void StartTyping()
    {
        if (targetText == null) return;
        if (textPages == null || textPages.Length == 0)
            textPages = new string[] { targetText.text };

        if (isTyping) return;

        targetText.maxVisibleCharacters = 0;
        typingCoroutine = StartCoroutine(TypingRoutine());
        if (showCursor) cursorCoroutine = StartCoroutine(CursorRoutine());

        OnTypingStarted?.Invoke();
    }

    public void StopTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (cursorCoroutine != null) StopCoroutine(cursorCoroutine);

        isTyping = false;
        if (targetText != null)
            targetText.maxVisibleCharacters = targetText.textInfo.characterCount;
    }

    public void PauseTyping()
    {
        isPaused = true;
        OnTypingPaused?.Invoke();
    }

    public void ResumeTyping()
    {
        isPaused = false;
        OnTypingResumed?.Invoke();
    }

    public void SkipCurrentPage()
    {
        if (isTyping && targetText != null)
        {
            StopCoroutine(typingCoroutine);
            targetText.maxVisibleCharacters = targetText.textInfo.characterCount;
            CompleteCurrentPage();
        }
    }

    public void SkipToEnd()
    {
        StopTyping();
        if (textPages == null || textPages.Length == 0) return;

        currentPageIndex = textPages.Length - 1;
        targetText.text = textPages[currentPageIndex];
        targetText.maxVisibleCharacters = targetText.textInfo.characterCount;
        OnTypingCompleted?.Invoke();
    }

    public void NextPage()
    {
        if (textPages == null) return;

        if (currentPageIndex < textPages.Length - 1)
        {
            currentPageIndex++;
            StartTyping();
        }
    }

    public void PreviousPage()
    {
        if (textPages == null) return;

        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            StartTyping();
        }
    }

    public void SetTypingSpeed(float newSpeed) => charactersPerSecond = newSpeed;

    // --- Coroutines ---

    private IEnumerator TypingRoutine()
    {
        isTyping = true;
        isPaused = false;

        string currentPage = textPages[currentPageIndex];
        targetText.text = currentPage;
        targetText.ForceMeshUpdate();

        int totalCharacters = targetText.textInfo.characterCount;
        int visibleCount = 0;

        while (visibleCount < totalCharacters)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            TMP_CharacterInfo charInfo = targetText.textInfo.characterInfo[visibleCount];

            if (!charInfo.isVisible)
            {
                visibleCount++;
                continue;
            }

            float baseDelay = 1f / charactersPerSecond;
            float curveTime = (float)visibleCount / totalCharacters;
            float curveMultiplier = speedCurve.Evaluate(curveTime);
            float characterDelay = baseDelay * curveMultiplier;

            if (delayMultipliers.ContainsKey(charInfo.character))
                characterDelay *= delayMultipliers[charInfo.character];

            if (useShakeEffect) ApplyShakeEffect();
            if (useColorEffect) ApplyColorEffect(visibleCount, totalCharacters);

            PlayTypeSound(charInfo.character);

            targetText.maxVisibleCharacters = visibleCount + 1;
            OnCharacterTyped?.Invoke(charInfo.character.ToString());

            visibleCount++;
            yield return new WaitForSeconds(characterDelay);
        }

        CompleteCurrentPage();
    }

    private IEnumerator CursorRoutine()
    {
        bool cursorVisible = true;

        while (isTyping)
        {
            if (cursorVisible)
                targetText.text = textPages[currentPageIndex] + "<alpha=#00>" + cursorChar;
            else
                targetText.text = textPages[currentPageIndex] + cursorChar;

            cursorVisible = !cursorVisible;
            yield return new WaitForSeconds(cursorBlinkSpeed);
        }

        targetText.text = textPages[currentPageIndex];
    }

    // --- Helpers ---

    private void CompleteCurrentPage()
    {
        if (useShakeEffect)
            targetText.rectTransform.localPosition = originalPosition;

        if (useColorEffect)
            targetText.color = finalColor;

        isTyping = false;
        if (cursorCoroutine != null) StopCoroutine(cursorCoroutine);

        OnTypingCompleted?.Invoke();
    }

    private void ApplyShakeEffect()
    {
        Vector3 shakeOffset = new Vector3(
            Random.Range(-shakeIntensity, shakeIntensity),
            Random.Range(-shakeIntensity, shakeIntensity),
            0
        );
        targetText.rectTransform.localPosition = originalPosition + shakeOffset;
    }

    private void ApplyColorEffect(int currentIndex, int totalCharacters)
    {
        float progress = (float)currentIndex / totalCharacters;
        targetText.color = Color.Lerp(typingColor, finalColor, progress);
    }

    private void PlayTypeSound(char character)
    {
        if (audioSource != null && typeSounds != null && typeSounds.Length > 0 && character != ' ')
        {
            if (randomPitch)
                audioSource.pitch = Random.Range(minPitch, maxPitch);

            AudioClip sound = typeSounds[Random.Range(0, typeSounds.Length)];
            audioSource.PlayOneShot(sound, soundVolume);
        }
    }

    // --- Properties ---

    public bool IsTyping => isTyping;
    public bool IsPaused => isPaused;
    public int CurrentPage => currentPageIndex + 1;
    public int TotalPages => textPages?.Length ?? 0;

    // --- Quick Static Typing ---

    public static void TypeText(TMP_Text textComponent, string text, float speed = 30f, UnityAction onComplete = null)
    {
        if (textComponent == null || string.IsNullOrEmpty(text)) return;

        AdvancedTMPTypewriter typewriter = textComponent.gameObject.AddComponent<AdvancedTMPTypewriter>();
        typewriter.targetText = textComponent;
        typewriter.charactersPerSecond = speed;
        typewriter.startOnEnable = false;

        typewriter.StartCoroutine(TypeTextRoutine(typewriter, text, onComplete));
    }

    private static IEnumerator TypeTextRoutine(AdvancedTMPTypewriter typewriter, string text, UnityAction onComplete)
    {
        yield return null;

        typewriter.SetText(text);
        typewriter.StartTyping();

        if (onComplete != null)
        {
            typewriter.OnTypingCompleted.AddListener(() =>
            {
                onComplete();
                Destroy(typewriter);
            });
        }
    }
    public float Progress
    {
        get
        {
            if (targetText == null || textPages == null || textPages.Length == 0)
                return 0f;

            int total = targetText.textInfo.characterCount;
            if (total == 0) return 0f;

            return (float)targetText.maxVisibleCharacters / total;
        }
    }

}
