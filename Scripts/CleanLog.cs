using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using UnityEngine;

public class LogEntry
{
    public StackFrame[] StackFrames { get; private set; }
    public string ClassName { get; private set; }
    public string Message { get; private set; }
    public CleanLog.LogType LogType { get; private set; }
    public Enum LogTag { get; private set; }

    public LogEntry(CleanLog.LogType logType, string message, Enum tag, StackFrame[] stackFrame)
    {
        LogType = logType;
        Message = message;
        LogTag = tag;
        StackFrames = stackFrame;
    }
}

public static class CleanLog
{
    public delegate void CleanLogLoggedEventHandler (LogEntry logEntry);

    public static event CleanLogLoggedEventHandler OnLoggedEvent;

    public static Type LogTagsEnumerationType {
        get;
        private set;
    }

    [Flags]
    public enum LogType
    {
        Info = 1 << 0,
        Warning = 1 << 1,
        Error = 1 << 2,
        Assert = 1 << 3,
        Exception = 1 << 4,
        All = Info | Warning | Error | Assert | Exception,
    }

    public static LogType LogFilter {
        get;
        set;
    } = LogType.All;

    public static LogType LogFileFilter {
        get;
        set;
    } = LogType.All;

    public static string CurrentClass {
        get {
            var st = new StackTrace ();

            foreach (var frame in st.GetFrames ()) {
                Type frameClassType = frame.GetMethod ().DeclaringType;

                if (frameClassType != typeof (CleanLog)) {
                    return frameClassType.Name;
                }
            }

            return "{NoClass}";
        }
    }

    public static string LogFilePath {
        get { return Application.persistentDataPath + "/" + Application.productName.Replace (' ', '_') + ".log"; }
    }

    public static int MaxLinesInLog {
        get;
        private set;
    } = 500;

    public static bool LogToUnityDebug {
        get;
        private set;
    } = true;

    public static void Setup (Type logTagsEnumerationType, int maxLinesInLog, bool logToUnityDebug)
    {
        LogTagsEnumerationType = logTagsEnumerationType;
        MaxLinesInLog = maxLinesInLog;
        LogToUnityDebug = logToUnityDebug;
    }

    private static void LogToFile (string message)
    {
        // Check for rolling length
        List<string> allLines;

        if (File.Exists (LogFilePath)) {
            allLines = new List<string> (File.ReadAllLines (LogFilePath));
        } else {
            allLines = new List<string> ();
        }

        allLines.Insert (0, string.Format ("{0}\t{1}", DateTime.Now.ToString ("[dd-MM-yy hh:mm:ss.fff]"), message));

        if (allLines.Count > MaxLinesInLog) {

            allLines = allLines.GetRange (0, MaxLinesInLog);
        }

        File.WriteAllLines (LogFilePath, allLines.ToArray ());
    }

    private static void Log (LogType logType, string message, Enum tag = null)
    {
        var tagString = string.Empty;

        var st = new StackTrace();
        var logEntry = new LogEntry(logType, message, tag, st.GetFrames());

        if ((LogFilter & logType) == logType) {

            if (OnLoggedEvent != null) {
                OnLoggedEvent (logEntry);
            }

            if (tag != null) {
                tagString = string.Format ("<b>[{0}]</b> ", tag.ToString ().ToUpper ());
            }

            if (LogToUnityDebug) {

                var unityLogType = UnityEngine.LogType.Log;

                if ((LogType.Info & logType) == LogType.Info) {
                    unityLogType = UnityEngine.LogType.Log;
                } else if ((LogType.Warning & logType) == LogType.Warning) {
                    unityLogType = UnityEngine.LogType.Warning;
                } else if ((LogType.Error & logType) == LogType.Error) {
                    unityLogType = UnityEngine.LogType.Error;
                } else if ((LogType.Assert & logType) == LogType.Assert) {
                    unityLogType = UnityEngine.LogType.Assert;
                } else if ((LogType.Exception & logType) == LogType.Exception) {
                    unityLogType = UnityEngine.LogType.Exception;
                }

                UnityEngine.Debug.unityLogger.LogFormat (unityLogType, "{0}{1}: {2}", tagString, CurrentClass, message);
            }
        }

        if ((LogFileFilter & logType) == logType) {
            LogToFile (string.Format ("[{0}]\t[{1}]:\t{2}", tagString, CurrentClass, message));
        }
    }

    #region Standard Log Functions

    public static void Log (string message)
    {
        Log (LogType.Info, message);
    }

    public static void LogFormat (string format, params object [] args)
    {
        Log (LogType.Info, string.Format (format, args));
    }

    public static void LogWarning (string message)
    {
        Log (LogType.Warning, message);
    }

    public static void LogWarningFormat (string format, params object [] args)
    {
        Log (LogType.Warning, string.Format (format, args));
    }

    public static void LogError (string message)
    {
        Log (LogType.Error, message);
    }

    public static void LogErrorFormat (string format, params object [] args)
    {
        Log (LogType.Error, string.Format (format, args));
    }

    #endregion

    #region Tagged Log Funtions

    public static void Log (Enum tag, string message)
    {
        Log (LogType.Info, message, tag);
    }

    public static void LogFormat (Enum tag, string format, params object [] args)
    {
        Log (LogType.Info, string.Format (format, args), tag);
    }

    public static void LogWarning (Enum tag, string message)
    {
        Log (LogType.Warning, message, tag);
    }

    public static void LogWarningFormat (Enum tag, string format, params object [] args)
    {
        Log (LogType.Warning, string.Format (format, args), tag);
    }

    public static void LogError (Enum tag, string message)
    {
        Log (LogType.Error, message, tag);
    }

    public static void LogErrorFormat (Enum tag, string format, params object [] args)
    {
        Log (LogType.Error, string.Format (format, args), tag);
    }

    #endregion
}

