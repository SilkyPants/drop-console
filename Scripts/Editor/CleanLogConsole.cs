#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CleanLogConsole : EditorWindow
{
    Rect 
        upperPanel, 
        lowerPanel, 
        resizer, 
        menuBar;

    int
        logInfoCount = 0,
        logWarningCount = 0,
        logErrorCount = 0,
        flags;

    float 
        sizeRatio = .5f,
        resizerHeight = 1.5f,
        menuBarHeight = 20f;

    bool 
        isResizing = false, 
        collapse = false,
        clearOnPlay = false,
        errorPause = false,
        showLog = false,
        showWarnings = false,
        showErrors = false;

    Vector2
        upperPanelScroll = Vector2.zero,
        lowerPanelScroll = Vector2.zero;

    Texture2D
        errorIcon,
        errorIconSmall,
        warningIcon,
        warningIconSmall,
        infoIcon,
        infoIconSmall,
        boxBgOdd,
        boxBgEven,
        boxBgSelected,
        icon;

    GUIStyle
        resizerStyle,
        boxStyle,
        textAreaStyle;

    List<LogEntry> logs = new List<LogEntry>();
    LogEntry selectedEntry = null;

    [MenuItem ("Window/Clean Log Console")]
    public static void OpenWindow ()
    {
        var window = GetWindow<CleanLogConsole> ();
        window.titleContent = new GUIContent ("Clean Log Console");
    }

    void OnEnable()
    {
        errorIcon = EditorGUIUtility.Load("icons/console.erroricon.png") as Texture2D;
        warningIcon = EditorGUIUtility.Load("icons/console.warnicon.png") as Texture2D;
        infoIcon = EditorGUIUtility.Load("icons/console.infoicon.png") as Texture2D;

        errorIconSmall = EditorGUIUtility.Load("icons/console.erroricon.sml.png") as Texture2D;
        warningIconSmall = EditorGUIUtility.Load("icons/console.warnicon.sml.png") as Texture2D;
        infoIconSmall = EditorGUIUtility.Load("icons/console.infoicon.sml.png") as Texture2D;

        resizerStyle = new GUIStyle();
        resizerStyle.normal.background = EditorGUIUtility.Load("icons/d_AvatarBlendBackground.png") as Texture2D;

        boxStyle = new GUIStyle();
        boxStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

        boxBgOdd = new Texture2D(1, 1);
        boxBgOdd.SetPixel(0, 0, new Color(.8f, .8f, .8f));

        boxBgEven = new Texture2D(1, 1);
        boxBgOdd.SetPixel(0, 0, new Color(.7f, .7f, .7f));

        boxBgOdd = EditorGUIUtility.Load("builtin skins/darkskin/images/cn entrybackodd.png") as Texture2D;
        boxBgEven = EditorGUIUtility.Load("builtin skins/darkskin/images/cnentrybackeven.png") as Texture2D;
        boxBgSelected = EditorGUIUtility.Load("builtin skins/darkskin/images/menuitemhover.png") as Texture2D;

        textAreaStyle = new GUIStyle();
        textAreaStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        textAreaStyle.normal.background = boxBgOdd;

        ClearLog();

        AddEvents();
    }

    void OnDisable()
    {
        RemoveEvents();
    }

    void OnDestroy()
    {
        RemoveEvents();
    }

    void AddEvents()
    {
        CleanLog.OnLoggedEvent += OnLoggedEvent;

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    void RemoveEvents()
    {
        CleanLog.OnLoggedEvent -= OnLoggedEvent;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (clearOnPlay && state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearLog();
            Repaint();
        }
    }

    private void OnLoggedEvent(LogEntry logEntry)
    {
        logs.Add(logEntry);

        switch(logEntry.LogType)
        {
            case CleanLog.LogType.Assert:
            case CleanLog.LogType.Error:
            case CleanLog.LogType.Exception:
                logErrorCount++;

                if (errorPause && EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.isPaused = true;
                }

                break;

            case CleanLog.LogType.Warning:
                logWarningCount++;
                break;

            case CleanLog.LogType.Info:
                logInfoCount++;
                break;
        }

        Repaint();
    }

    void OnGUI ()                     
    {
        DrawMenuBar ();
        DrawUpperPanel ();
        DrawLowerPanel ();
        DrawResizer ();

        ProcessEvents (Event.current);

        if (GUI.changed) Repaint ();
    }

    private void DrawMenuBar()
    {
        menuBar = new Rect(0, 0, position.width, menuBarHeight);

        GUILayout.BeginArea(menuBar, EditorStyles.toolbar);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("Clear"), EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            ClearLog();
        }

        GUILayout.Space(5);

        collapse = GUILayout.Toggle(collapse, new GUIContent("Collapse"), EditorStyles.toolbarButton, GUILayout.Width(80));
        clearOnPlay = GUILayout.Toggle(clearOnPlay, new GUIContent("Clear On Play"), EditorStyles.toolbarButton, GUILayout.Width(80));
        errorPause = GUILayout.Toggle(errorPause, new GUIContent("Error Pause"), EditorStyles.toolbarButton, GUILayout.Width(80));

        GUILayout.FlexibleSpace();

        if (CleanLog.LogTagsEnumerationType != null)
        {
            var tagStrings = Enum.GetNames(CleanLog.LogTagsEnumerationType);
            flags = EditorGUILayout.MaskField("Tags", flags, tagStrings, EditorStyles.toolbarDropDown, GUILayout.MinWidth(80));
        }

        showLog = GUILayout.Toggle(showLog, new GUIContent(logInfoCount.ToString(), infoIconSmall), EditorStyles.toolbarButton, GUILayout.MinWidth(40));
        showWarnings = GUILayout.Toggle(showWarnings, new GUIContent(logWarningCount.ToString(), warningIconSmall), EditorStyles.toolbarButton, GUILayout.MinWidth(40));
        showErrors = GUILayout.Toggle(showErrors, new GUIContent(logErrorCount.ToString(), errorIconSmall), EditorStyles.toolbarButton, GUILayout.MinWidth(40));
        
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawUpperPanel ()
    {
        upperPanel = new Rect (0, menuBarHeight, position.width, (position.height * sizeRatio) - menuBarHeight);

        GUILayout.BeginArea (upperPanel);
        upperPanelScroll = GUILayout.BeginScrollView(upperPanelScroll);

        bool isOdd = false;
        foreach (var log in logs)
        {
            if (DrawBox(log.Message, log.LogType, isOdd, log == selectedEntry))
            {
                selectedEntry = log;
                GUI.changed = true;
            }

            isOdd = !isOdd;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea ();
    }

    void DrawLowerPanel ()
    {
        lowerPanel = new Rect (0, (position.height * sizeRatio) + resizerHeight, position.width, (position.height * (1 - sizeRatio)) - resizerHeight);

        GUILayout.BeginArea (lowerPanel);
        lowerPanelScroll = GUILayout.BeginScrollView(lowerPanelScroll);

        if (selectedEntry != null)
        {
            GUILayout.TextArea(selectedEntry.Message, textAreaStyle);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea ();
    }

    void DrawResizer ()
    {
        resizer = new Rect (0, (position.height * sizeRatio) - resizerHeight, position.width, resizerHeight * 2);

        GUILayout.BeginArea (new Rect (resizer.position + (Vector2.up * resizerHeight), new Vector2 (position.width, 2)), resizerStyle);
        GUILayout.EndArea ();

        EditorGUIUtility.AddCursorRect (resizer, MouseCursor.ResizeVertical);
    }

    bool DrawBox(string content, CleanLog.LogType boxType, bool isOdd, bool isSelected)
    {
        if (isSelected)
        {
            boxStyle.normal.background = boxBgSelected;
        }
        else
        {
            boxStyle.normal.background = isOdd ? boxBgOdd : boxBgEven;
        }

        switch (boxType)
        {
            case CleanLog.LogType.Exception:
            case CleanLog.LogType.Assert:
            case CleanLog.LogType.Error:
                icon = errorIcon;
                break;

            case CleanLog.LogType.Warning:
                icon = warningIcon;
                break;

            case CleanLog.LogType.Info:
            default:
                icon = infoIcon;
                break;
        }

        return GUILayout.Button(new GUIContent(content, icon), boxStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30));
    }

    void ProcessEvents (Event current)
    {
        switch (current.type) {

        case EventType.MouseDown:
            if (current.button == 0 && resizer.Contains (current.mousePosition)) {
                isResizing = true;
            }
            break;

        case EventType.MouseUp:
            isResizing = false;
            break;

        default:
            break;
        }

        Resize (current);
    }

    void Resize (Event current)
    {
        if (isResizing) {
            sizeRatio = current.mousePosition.y / position.height;
            Repaint ();
        }
    }

    void ClearLog()
    {
        logs = new List<LogEntry>();
        selectedEntry = null;

        logInfoCount = 0;
        logWarningCount = 0;
        logErrorCount = 0;
    }
}

#endif