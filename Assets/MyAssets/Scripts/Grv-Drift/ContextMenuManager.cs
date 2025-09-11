using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System;

namespace zoya.game
{



    public class ContextMenuManager : MonoBehaviour
    {
        public static ContextMenuManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject contextMenuPanel;
        [SerializeField] private Transform optionsContainer;
        [SerializeField] private GameObject contextOptionPrefab;
        [SerializeField] private TMP_Text titleText;

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.2f;

        private CanvasGroup _canvasGroup;
        private readonly List<GameObject> _currentOptions = new List<GameObject>();

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
                return;
            }

            _canvasGroup = contextMenuPanel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = contextMenuPanel.AddComponent<CanvasGroup>();

            HideContextMenu();
        }

        public static void ShowContextMenu(string title, ContextMenuOption[] options)
        {
            if (Instance == null)
            {
                Debug.LogWarning("ContextMenuManager instance not found!");
                return;
            }

            Instance.ShowMenu(title, options);
        }

        public static void HideContextMenu()
        {
            if (Instance != null)
            {
                Instance.HideMenu();
            }
        }

        private void ShowMenu(string title, ContextMenuOption[] options)
        {
            // Set title
            titleText.text = title;

            // Clear previous options
            ClearOptions();

            // Create new options
            foreach (var option in options)
            {
                CreateOptionButton(option);
            }

            // Position near mouse
            PositionMenuNearMouse();

            // Show menu
            contextMenuPanel.SetActive(true);
            StartCoroutine(FadeMenu(0f, 1f));
        }

        private void CreateOptionButton(ContextMenuOption option)
        {
            GameObject optionObj = Instantiate(contextOptionPrefab, optionsContainer);
            Button button = optionObj.GetComponent<Button>();
            TMP_Text label = optionObj.GetComponentInChildren<TMP_Text>();
            Image iconImage = optionObj.transform.Find("Icon")?.GetComponent<Image>();

            label.text = option.Label;

            if (iconImage != null && option.Icon != null)
            {
                iconImage.sprite = option.Icon;
                iconImage.gameObject.SetActive(true);
            }
            else if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }

            button.onClick.AddListener(() =>
            {
                option.Action?.Invoke();
                HideMenu();
            });

            button.interactable = option.IsEnabled;

            _currentOptions.Add(optionObj);
        }

        private void PositionMenuNearMouse()
        {
            RectTransform rectTransform = contextMenuPanel.GetComponent<RectTransform>();
            Vector2 mousePosition = Input.mousePosition;

            // Ensure menu stays on screen
            float menuWidth = rectTransform.rect.width;
            float menuHeight = rectTransform.rect.height;

            float x = Mathf.Clamp(mousePosition.x, menuWidth / 2, Screen.width - menuWidth / 2);
            float y = Mathf.Clamp(mousePosition.y, menuHeight / 2, Screen.height - menuHeight / 2);

            rectTransform.position = new Vector3(x, y, 0);
        }

        private void HideMenu()
        {
            StartCoroutine(FadeMenu(1f, 0f, () =>
            {
                contextMenuPanel.SetActive(false);
                ClearOptions();
            }));
        }

        private void ClearOptions()
        {
            foreach (var option in _currentOptions)
            {
                Destroy(option);
            }
            _currentOptions.Clear();
        }

        private IEnumerator FadeMenu(float startAlpha, float targetAlpha, System.Action onComplete = null)
        {
            float elapsed = 0f;
            _canvasGroup.alpha = startAlpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
            onComplete?.Invoke();
        }
    }
    // ContextMenuOption.cs

    public class ContextMenuOption
    {
        // visible in inspector
        public string Label;
        public Sprite Icon;
        public bool IsEnabled = true;

        // callbacks can't be serialized by Unity, mark NonSerialized so inspector doesn't try.
        [NonSerialized] public Action Action;

        public ContextMenuOption() { }

        public ContextMenuOption(string label, Action action, bool isEnabled)
        {
            Label = label;
            Action = action;
            Icon = null;
            IsEnabled = isEnabled;
        }


        public ContextMenuOption(string label, Action action, Sprite icon = null, bool isEnabled = true)
        {
            Label = label;
            Action = action;
            Icon = icon;
            IsEnabled = isEnabled;
        }

        // convenience factory
        public static ContextMenuOption Create(string label, Action action, Sprite icon = null, bool isEnabled = true)
        {
            return new ContextMenuOption(label, action, icon, isEnabled);
        }
    }
}


