using System;
using UnityEngine;

namespace AntennaSleep
{
    static class Logging
    {
        public static void Log(object message)
        {
            Debug.Log("[AntennaSleep] " + message);
        }

        public static void Warn(object message)
        {
            Debug.LogWarning("[AntennaSleep] " + message);
        }

        public static void Error(object message)
        {
            Debug.LogError("[AntennaSleep] " + message);
        }

        public static void Exception(string message, Exception e)
        {
            Error(message + " (" + e.GetType().Name + ") " + e.Message + ": " + e.StackTrace);
        }

        public static void Exception(Exception e)
        {
            Error("(" + e.GetType().Name + ") " + e.Message + ": " + e.StackTrace);
        }

        public static string ToString(Part part)
        {
            return part.partInfo.title;
        }
    }
}
