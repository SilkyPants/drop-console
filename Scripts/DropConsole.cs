using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;

public delegate string ConsoleCommandCallback(params string[] args);

public static class Extensions {

    public static string ToHex(this Color32 color) {
        string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
        return hex;
    }
}

class ConsoleCommand
{

    public string Command
    {
        get;
        private set;
    }

    public string HelpText
    {
        get;
        private set;
    }

    public ConsoleCommandCallback Invoke
    {
        get;
        private set;
    }

    public ConsoleCommand(string command, ConsoleCommandCallback invoke, string helpText)
    {
        Command = command;
        Invoke = invoke;
        HelpText = helpText;
    }
}

public class DropConsole : MonoBehaviour
{
    List<string> consoleCommandHistory = new List<string>();
    Dictionary<string, ConsoleCommand> consoleCommandRepository = new Dictionary<string, ConsoleCommand>();

    [Header("Console Properties")]
    [SerializeField] Font consoleFont = null;

    [SerializeField]
    [Range(10, 28)] 
    int consoleFontSize = 18;
    public float animationTime = 0.1f;
    public bool clearOnHide = false;
    public KeyCode consoleToggleKey = KeyCode.BackQuote;
    
    public bool hideOnLostFocus = true;
    
    [SerializeField] Sprite errorSprite;
    [SerializeField] Sprite warningSprite;

    [Header("UI Components")]
    [SerializeField] RectTransform consolePanel;
    [SerializeField] Transform consoleLogParent;
    [SerializeField] InputField consoleInput;
    [SerializeField] Color32 errorColor = new Color(0.9f, 0f, 0f);
    [SerializeField] Color32 warningColor = new Color(1f, 0.9f, 0f);

    [SerializeField] CanvasGroup indicatorsGroup = null;
    [SerializeField] Image warningIndicator = null;
    [SerializeField] Image errorIndicator = null;

    [SerializeField] CanvasGroup modalGroup = null;

    float panelHeight;

    public bool isConsoleShown = false;
    bool canProcessBackgroundInput = true;
    int currentCommandIndex = -1;

    Coroutine animateConsoleCoroutine;

    bool IsModifierKeyDown
    {
        get
        { 
            return Input.GetKeyDown(KeyCode.LeftShift)
            || Input.GetKeyDown(KeyCode.RightShift)
            || Input.GetKeyDown(KeyCode.LeftControl)
            || Input.GetKeyDown(KeyCode.RightControl)
            || Input.GetKeyDown(KeyCode.LeftAlt)
            || Input.GetKeyDown(KeyCode.RightAlt);
        }
    }

    #region Static methods

    static void CheckForInstance()
    {

        if (Instance == null) {
            CreateConsoleObjects();
        }
    }


    static void CreateConsoleObjects()
    {

        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var consoleObject = new GameObject("Drop Down Console");

        var consoleCanvas = consoleObject.AddComponent<Canvas>();
        consoleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        consoleCanvas.sortingOrder = 32767;

        consoleObject.AddComponent<CanvasScaler>();
        consoleObject.AddComponent<GraphicRaycaster>();

        var console = consoleObject.AddComponent<DropConsole>();

        //Create the rest of the UI programatically
        // Indicators
        var indicatorsObject = new GameObject("Indicators");
        indicatorsObject.transform.SetParent(consoleObject.transform, false);

        var indicators = indicatorsObject.AddComponent<RectTransform>();

        indicators.anchorMin = new Vector2(0f, 1f);
        indicators.anchorMax = Vector2.one;
        indicators.pivot = new Vector2(0.5f, 1f);
        indicators.sizeDelta = new Vector2(0f, 64f);
        indicators.anchoredPosition = Vector2.zero;

        var indicatorsLayout = indicatorsObject.AddComponent<HorizontalLayoutGroup>();
        indicatorsLayout.padding = new RectOffset(0, 0, 0, 0);
        indicatorsLayout.spacing = 4;
        indicatorsLayout.childAlignment = TextAnchor.MiddleRight;
        indicatorsLayout.childControlWidth = true;
        indicatorsLayout.childControlHeight = true;
        indicatorsLayout.childForceExpandWidth = true;
        indicatorsLayout.childForceExpandHeight = true;

        console.indicatorsGroup = indicatorsObject.AddComponent<CanvasGroup>();
        console.indicatorsGroup.interactable = false;
        console.indicatorsGroup.blocksRaycasts = false;

        var indicatorSpacer = new GameObject("Spacer");
        indicatorSpacer.transform.SetParent(indicatorsObject.transform, false);
        indicatorSpacer.AddComponent<LayoutElement>().flexibleWidth = float.MaxValue;

        var warningIndicator = new GameObject("Warning Indicator");
        warningIndicator.transform.SetParent(indicatorsObject.transform, false);
        warningIndicator.AddComponent<LayoutElement>().preferredWidth = 64f;
        console.warningIndicator = warningIndicator.AddComponent<Image>();

        var errorIndicator = new GameObject("Error Indicator");
        errorIndicator.transform.SetParent(indicatorsObject.transform, false);
        errorIndicator.AddComponent<LayoutElement>().preferredWidth = 64f;
        console.errorIndicator = errorIndicator.AddComponent<Image>();

        // Panel
        var panelObject = new GameObject("Console Panel");
        panelObject.transform.SetParent(consoleObject.transform, false);

        var panel = panelObject.AddComponent<RectTransform>();
        console.consolePanel = panel;

        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(0.5f, 0f);
        panel.sizeDelta = new Vector2(0f, 400f);
        panel.anchoredPosition = Vector2.zero;

        var panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.8f);

        panelObject.AddComponent<VerticalLayoutGroup>();

        /// -------- Creating scrolling log --------

        var scrollViewObject = new GameObject("Scroll View");
        scrollViewObject.AddComponent<RectTransform>();
        scrollViewObject.transform.SetParent(panelObject.transform, false);

        scrollViewObject.AddComponent<LayoutElement>().preferredHeight = 400f;

        var scrollRect = scrollViewObject.AddComponent<ScrollRect>();
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -3;

        var scrollViewportObject = new GameObject("Scroll Viewport");
        scrollRect.viewport = scrollViewportObject.AddComponent<RectTransform>();
        scrollViewportObject.transform.SetParent(scrollViewObject.transform, false);

        var scrollViewportImage = scrollViewportObject.AddComponent<Image>();
        scrollViewportImage.color = Color.white;
        scrollViewportObject.AddComponent<Mask>().showMaskGraphic = false;

        var scrollContentObject = new GameObject("Content");
        var scrollContentRect = scrollContentObject.AddComponent<RectTransform>();
        scrollContentObject.transform.SetParent(scrollViewportObject.transform, false);

        console.consoleLogParent = scrollContentObject.transform;

        scrollContentRect.anchorMin = Vector2.zero;
        scrollContentRect.anchorMax = new Vector2(1f, 0f);
        scrollContentRect.pivot = new Vector2(0.5f, 0f);
        scrollContentRect.offsetMax = Vector2.zero;
        scrollContentRect.offsetMin = Vector2.zero;

        scrollRect.content = scrollContentRect;

        scrollContentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollContentVertLayout = scrollContentObject.AddComponent<VerticalLayoutGroup>();
        scrollContentVertLayout.padding = new RectOffset(4, 4, 4, 4);
        scrollContentVertLayout.spacing = 1;
        scrollContentVertLayout.childAlignment = TextAnchor.LowerCenter;

        var vertScrollbarObject = new GameObject("Vertical Scrollbar");
        var vertScrollbarRect = vertScrollbarObject.AddComponent<RectTransform>();
        vertScrollbarObject.transform.SetParent(scrollViewObject.transform, false);
        vertScrollbarObject.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        vertScrollbarRect.anchorMin = new Vector2(1f, 0f);
        vertScrollbarRect.anchorMax = Vector2.one;
        vertScrollbarRect.pivot = Vector2.one;
        vertScrollbarRect.offsetMax = new Vector2(0f, 0f);
        vertScrollbarRect.offsetMin = new Vector2(-4f, 0f);

        var vertScrollbar = vertScrollbarObject.AddComponent<Scrollbar>(); 
        vertScrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollRect.verticalScrollbar = vertScrollbar;

        var slidingAreaObject = new GameObject("Sliding Area");
        var slidingAreaRect = slidingAreaObject.AddComponent<RectTransform>();
        slidingAreaObject.transform.SetParent(vertScrollbarObject.transform, false);

        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(10f, 10f);
        slidingAreaRect.offsetMax = new Vector2(-10f, -10f);

        var handleObject = new GameObject("Handle");
        var handleRect = handleObject.AddComponent<RectTransform>();
        handleObject.transform.SetParent(slidingAreaObject.transform, false);
        vertScrollbar.targetGraphic = handleObject.AddComponent<Image>();

        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(-10f, -10f);
        handleRect.offsetMax = new Vector2(10f, 10f);

        vertScrollbar.handleRect = handleRect;
        /// -------- END Creating scrolling log --------

        /// -------- Creating text input --------

        var inputObject = new GameObject("Console Input");
        inputObject.AddComponent<RectTransform>();
        inputObject.transform.SetParent(panelObject.transform, false);

        var inputImage = inputObject.AddComponent<Image>();
        inputImage.color = new Color(1f, 1f, 1f, 0.125f);

        var inputField = inputObject.AddComponent<InputField>();
        inputField.lineType = InputField.LineType.MultiLineNewline;

        console.consoleInput = inputField;

        var inputLayout = inputObject.AddComponent<LayoutElement>();
        inputLayout.minHeight = 40f;

        var placeholderObject = new GameObject("Placeholder");
        var placeholderRect = placeholderObject.AddComponent<RectTransform>();
        placeholderObject.transform.SetParent(inputObject.transform, false);

        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10f, 6f);
        placeholderRect.offsetMax = new Vector2(-10f, -7f);

        var placeholderText = placeholderObject.AddComponent<Text>();
        placeholderText.font = font;
        placeholderText.fontStyle = FontStyle.Normal;
        placeholderText.text = "Enter command or type <b>help</b> for a list of available commands";
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.color = new Color(1f, 1f, 1f, 0.5f);

        var inputTextObject = new GameObject("Input Text");
        var inputTextRect = inputTextObject.AddComponent<RectTransform>();
        inputTextObject.transform.SetParent(inputObject.transform, false);

        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = new Vector2(10f, 6f);
        inputTextRect.offsetMax = new Vector2(-10f, -7f);

        var inputText = inputTextObject.AddComponent<Text>();
        inputText.font = font;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;

        inputField.placeholder = placeholderText;
        inputField.textComponent = inputText;

        /// -------- END Creating text input --------

        //consoleObject.hideFlags = HideFlags.HideAndDontSave;

        if (UnityEngine.EventSystems.EventSystem.current == null) {
            var eventSystem = new GameObject("Event System");
            eventSystem.transform.SetParent(consoleObject.transform);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    public static void RegisterCommand(string command, ConsoleCommandCallback invoke, string helpText = "")
    {

        CheckForInstance();

        var newCommand = new ConsoleCommand(command, invoke, helpText);
        Instance.RegisterCommand(newCommand);
    }

    public static void AddToLog(string format, params string[] args)
    {
		
        CheckForInstance();

        Instance.AddToLogAndUpdate(string.Format(format, args));
    }

    public static void AddToLog(string message)
    {

        CheckForInstance();

        Instance.AddToLogAndUpdate(message);
    }

    #endregion

    static DropConsole Instance
    {
        get;
        set;
    }

    public static bool IsConsoleOpen
    {
        get { return Instance.isConsoleShown; }
    }

    void Awake()
    {

        if (Instance != null) {
            CleanLog.LogError("Cannot have multiple instances of Drop Console.");

            this.enabled = false;

            return;
        }

        Instance = this;

        DontDestroyOnLoad(this.gameObject);

        RegisterCommand("help", ListAllCommands, "Lists all registered console commands");
        RegisterCommand("clear", ClearLists, "[log|cmds|all] Clears console log, command history, or both");
        RegisterCommand("echo", EchoString, "Echoes a string to the console");
        RegisterCommand("loadScene", LoadScene, "Loads a scene added to the project via Build Settings");
        RegisterCommand("screenshot", TakeScreenshot, "filename [supersize] Saves a screenshot. Supersize is a factor to increase the resolution");
        RegisterCommand("version", PrintVersion, "Prints the current application version");
    }

    void Start()
    {

        if (consolePanel == null || consoleInput == null) {

            CleanLog.LogError("UI Components are not set up!");

            this.enabled = false;

            return;
        }

        panelHeight = consolePanel.rect.height;

        consolePanel.anchoredPosition = Vector2.zero;

        consoleInput.onValidateInput += ValidateConsoleInput;
		consoleInput.onEndEdit.AddListener(EndEditConsoleInput);

        if (consoleFont != null) {
            consoleInput.textComponent.font = consoleFont;
            consoleInput.placeholder.GetComponent<Text>().font = consoleFont;

            consoleInput.textComponent.fontSize = consoleFontSize;
            consoleInput.placeholder.GetComponent<Text>().fontSize = consoleFontSize;
        }

        if (errorSprite != null && warningSprite != null) {
            errorIndicator.sprite = errorSprite;
            errorIndicator.color = errorColor;

            warningIndicator.sprite = warningSprite;
            warningIndicator.color = warningColor;
        }

        warningIndicator.gameObject.SetActive(false);
        errorIndicator.gameObject.SetActive(false);

        Input.multiTouchEnabled = true;

        ParseCommand("version");

        RegisterLogging();
    }

    void Update()
    {
        if (consoleInput.isActiveAndEnabled && isConsoleShown) {
            if (consoleInput.isFocused) {
				if (Input.GetKeyDown(KeyCode.UpArrow) && !IsModifierKeyDown) {

                    SkipToCommandHistoryIndex(Mathf.Min(currentCommandIndex + 1, consoleCommandHistory.Count - 1));

                } else if (Input.GetKeyDown(KeyCode.DownArrow) && !IsModifierKeyDown) {
                    
                    SkipToCommandHistoryIndex(Mathf.Max(currentCommandIndex - 1, -1));
                }
            }
        }

        bool showConsole = Input.GetKeyUp(consoleToggleKey);

        if (Input.touchSupported && Input.multiTouchEnabled) {
            showConsole |= (Input.touchCount == 4 && Input.GetTouch(0).tapCount == 2);
        }

        if (showConsole) {
            if (canProcessBackgroundInput) ToggleConsoleShown();
            else canProcessBackgroundInput = true;
        }
    }

    #region Console visiblity methods

    public void ToggleConsoleShown(bool animate = true)
    {
        if (consolePanel.gameObject.activeInHierarchy == false) return;

        isConsoleShown = !isConsoleShown;

        if (animateConsoleCoroutine != null) StopCoroutine(animateConsoleCoroutine);
		
        animateConsoleCoroutine = StartCoroutine(AnimateConsoleShown(animate));
    }

    IEnumerator AnimateConsoleShown(bool animate)
    {
        Vector3 start = consolePanel.anchoredPosition;
        Vector3 end = new Vector3(0, !isConsoleShown ? 0 : -panelHeight, 0);

        float indicatorStart = indicatorsGroup.alpha;
        float indicatorEnd = !isConsoleShown ? 1f : 0f;

        float animTime = animate ? animationTime : 0f;

        modalGroup.blocksRaycasts = false;

        if (isConsoleShown) {
            consoleInput.ActivateInputField();
            yield return new WaitForFixedUpdate();
            consoleInput.MoveTextEnd(false);

        } else {
            consoleInput.DeactivateInputField();

            if (clearOnHide) {
                consoleInput.text = string.Empty;
            }
        }

        if (animTime > 0 && !start.Equals(end)) {

            float moveDelta = Mathf.Abs(end.y - start.y);
            float animProgress = animTime * (moveDelta / panelHeight);
            float t = 0;

            while (t <= animProgress) {

                consolePanel.anchoredPosition = Vector3.Lerp(start, end, t / animProgress);
                indicatorsGroup.alpha = Mathf.Lerp(indicatorStart, indicatorEnd, t / animProgress);

                if (modalGroup != null) {
                    modalGroup.alpha = 1 - indicatorsGroup.alpha;
                }

                t += Time.fixedDeltaTime; // Goes from 0 to 1, incrementing by step each time

                yield return new WaitForFixedUpdate();
            }
        }

        consolePanel.anchoredPosition = end;
        indicatorsGroup.alpha = indicatorEnd;
        
        if (modalGroup != null) {
            modalGroup.alpha = 1 - indicatorsGroup.alpha;
            modalGroup.blocksRaycasts = isConsoleShown;
        }

        if (isConsoleShown)
        {
            errorIndicator.gameObject.SetActive(false);
            warningIndicator.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Text events

    private void EndEditConsoleInput(string text) 
    {
        if (hideOnLostFocus && string.IsNullOrEmpty(text)) 
            ToggleConsoleShown();
    }

    private char ValidateConsoleInput(string text, int charIndex, char addedChar)
    {
        char validChar = '\0';

        if (addedChar == (char)consoleToggleKey) {
            canProcessBackgroundInput = false;
            ToggleConsoleShown();
        } else if (addedChar == '\n') {
            
            text = text.Trim();

            if (string.IsNullOrEmpty(text) == false) {

                // Store the command - even if it fails
                consoleCommandHistory.Insert(0, text);
                currentCommandIndex = -1;

                ParseCommand(text);
            }

            // Clear the console
            consoleInput.text = string.Empty;
            consoleInput.ActivateInputField();
        } else if (addedChar == '\t') {
            ShowAutocompleteOptions(text);
        } else {
            validChar = addedChar;
        }

        return validChar;
    }

    #endregion

    #region Core Methods

    void ShowAutocompleteOptions(string text)
    {
        var commands = consoleCommandRepository.Keys.Where(x => x.StartsWith(text)).ToArray();

        if (commands.Length == 1) {
            consoleInput.text = commands[0] + " ";
        }
        else if (commands.Length > 1) {
            ParseCommand("echo " + string.Join(" ", commands));
        }
    }

    void SkipToCommandHistoryIndex(int index)
    {
        if (index >= 0 && index < consoleCommandHistory.Count) {

            consoleInput.text = consoleCommandHistory[index];
            consoleInput.MoveTextEnd(false);
        } else {

            consoleInput.text = string.Empty;
            index = -1;
        }

        currentCommandIndex = index;
    }

    void ClearLogHistory() 
    {
        foreach (Transform child in consoleLogParent) {
            Destroy(child.gameObject);
        }
    }

    void AddToLogAndUpdate(string message)
    {
        if (!string.IsNullOrEmpty(message)) {

            var consoleLogObject = new GameObject("Console Log");
            consoleLogObject.AddComponent<RectTransform>();
            consoleLogObject.transform.SetParent(consoleLogParent, false);

            var consoleLogText = consoleLogObject.AddComponent<Text>();
            consoleLogText.font = consoleFont;
            consoleLogText.fontSize = consoleFontSize;
            consoleLogText.alignment = TextAnchor.LowerLeft;
            consoleLogText.text = message;

        }
    }

    void RegisterLogging()
    {
        CleanLog.OnLoggedEvent += delegate (LogEntry logEntry)
        {
            string echo = logEntry.Message;

            //          if (string.IsNullOrEmpty (tag) == false) {
            //              echo = "<b>[" + tag + "]</b> " + echo;
            //          }

            switch (logEntry.LogType) {
            case CleanLog.LogType.Error:
            case CleanLog.LogType.Exception:
            case CleanLog.LogType.Assert:
                echo = "<color=#" + errorColor.ToHex() + ">" + echo + "</color>";
                errorIndicator.gameObject.SetActive(true);
                break;

            case CleanLog.LogType.Warning:

                echo = "<color=#" + warningColor.ToHex() + ">" + echo + "</color>";
                warningIndicator.gameObject.SetActive(true);

                break;

            default:
                break;
            }

            ParseCommand("echo " + echo);
        };

        Application.logMessageReceived += delegate (string logString, string stackTrace, UnityEngine.LogType type)
        {
            string echo = logString;

            switch (type) {

            case UnityEngine.LogType.Error:
            case UnityEngine.LogType.Exception:
            case UnityEngine.LogType.Assert:

                echo = "<color=#" + errorColor.ToHex() + ">" + echo + "</color>";
                errorIndicator.gameObject.SetActive(true);

                break;

            case UnityEngine.LogType.Warning:

                echo = "<color=#" + warningColor.ToHex() + ">" + echo + "</color>";
                warningIndicator.gameObject.SetActive(true);

                break;

            default:
                break;
            }

            ParseCommand("echo " + echo);
        };
    }

    void RegisterCommand(ConsoleCommand newCommand)
    {

        if (consoleCommandRepository.ContainsKey(newCommand.Command) == false) {

            consoleCommandRepository.Add(newCommand.Command, newCommand);
        }
    }

    void ParseCommand(string text)
    {
        if (string.IsNullOrEmpty(text) == false) {

            string command = string.Empty;
            string[] args = { };

            if (text.Contains(" ")) {
                // Split the commands
                var parts = new List<string>(text.Split(' '));
                command = parts[0];

                if (parts.Count > 1) {
                    args = parts.GetRange(1, parts.Count - 1).ToArray();
                }

            } else {
                command = text;
            }

            if (!string.IsNullOrEmpty(command)) {
                AddToLog(ExecuteCommand(command, args));
            }
        }
    }

    string ExecuteCommand(string command, params string[] args)
    {
        if (consoleCommandRepository.ContainsKey(command)) {

            ConsoleCommand cmd = consoleCommandRepository[command];

            return cmd.Invoke(args);
        }

        return "Error executing command '" + command + "'";
    }

    #endregion

    #region Core Command Methods

    string ListAllCommands(params string[] args)
    {
        var longestCommand = consoleCommandRepository.Values.Max(x => x.Command.Length);
        var maxNumTabs = Mathf.FloorToInt(longestCommand / 4f);


        StringBuilder commandList = new StringBuilder("\nCommands Listing\n----------------\n\n");

        foreach (string key in consoleCommandRepository.Keys) {
            var command = consoleCommandRepository[key];
            var tabsCount = (maxNumTabs - Mathf.FloorToInt(command.Command.Length / 4f)) + 1;

            commandList.AppendFormat("<b>{0}</b>{2}- {1}\n", command.Command, command.HelpText, new String('\t', tabsCount));
        }

        return commandList.ToString();
    }

    string ClearLists(params string[] args)
    {

        var logToClear = "all";
        var returnMsg = string.Empty;

        if (args.Length > 0) {
            logToClear = args[0];
        }

        if (logToClear.Equals("all")) {
            ClearLogHistory();
            consoleCommandHistory.Clear();

        } else if (logToClear.Equals("log")) {
            ClearLogHistory();

        } else if (logToClear.Equals("cmd")) {
            consoleCommandHistory.Clear();

            returnMsg = "Command history cleared";

        } else {
            returnMsg = "Unknown argument '" + logToClear + "'";
        }

        return returnMsg;
    }

    string PrintVersion(params string[] args)
    {
        return string.Format("{0} v{1}", Application.productName, Application.version);
    }

    string EchoString(params string[] args)
    {

        if (args.Length > 0) {			
            return string.Join(" ", args);
        }

        return string.Empty;
    }

    string LoadScene(params string[] args)
    {
        if (args.Length > 0) {

            var sceneName = args[0];
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);

            return string.Format("Scene '{0}' loaded.", sceneName);
        }

        return "Need to give a scene name to load!";
    }

    string TakeScreenshot(params string[] args)
    {

        string filename = "Screenshot " + DateTime.Now.ToString("yy-MM-dd hh-mm-ss");
        int superSize = 0;

        if (args.Length > 0) {
			
            filename = args[0];

            if (args.Length > 1 && !int.TryParse(args[1], out superSize)) {
                return "Supersize parameter must be a number!";
            }
        }

        filename += ".png";

        StartCoroutine(CaptureScreenshot(filename, superSize));

        return "Screenshot saved as " + Application.persistentDataPath + "/" + filename;
    }

    IEnumerator CaptureScreenshot(string filename, int superSize)
    {

        consolePanel.anchoredPosition = Vector2.zero;

        yield return new WaitForEndOfFrame();

        ScreenCapture.CaptureScreenshot(filename, superSize);

        yield return null;

        consolePanel.anchoredPosition = new Vector2(0, -panelHeight);
    }

    #endregion
}
