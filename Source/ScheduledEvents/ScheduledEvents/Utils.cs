using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScheduledEvents
{
    public static class Utils
    {

        public static void LogMessage(string message)
        {
            Log.Message("[ScheduledEvents]: " + message);
        }

        public static void LogWarning(string message)
        {
            Log.Warning("[ScheduledEvents]: " + message);
        }

        public static void LogDebug(string message)
        {
            if (ScheduledEventsSettings.logDebug)
            {
                Log.Message("[ScheduledEvents DEBUG]: " + message);
            }
        }

        public static void LogDebugWarning(string message)
        {
            if (ScheduledEventsSettings.logDebug)
            {
                Log.Warning("[ScheduledEvents DEBUG]: " + message);
            }
        }

        public static void ScribeCustomList<T>(ref List<T> list, string label, Action<T> saver, Func<T> loader, IExposable caller)
        {
            Scribe.EnterNode("events");
            try
            {
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    foreach (T e in list)
                    {
                        Scribe.EnterNode("li");
                        try
                        {
                            saver.Invoke(e);
                        }
                        finally
                        {
                            Scribe.ExitNode();
                        }
                    }
                }
                else if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    XmlNode curXmlParent = Scribe.loader.curXmlParent;
                    list = new List<T>();
                    foreach (object obj in curXmlParent.ChildNodes)
                    {
                        XmlNode subNode = (XmlNode)obj;
                        XmlNode oldXmlParent = Scribe.loader.curXmlParent;
                        IExposable oldParent = Scribe.loader.curParent;
                        string oldPathRelToParent = Scribe.loader.curPathRelToParent;
                        Scribe.loader.curPathRelToParent = null;
                        Scribe.loader.curParent = caller;
                        Scribe.loader.curXmlParent = subNode;
                        try
                        {
                            list.Add(loader.Invoke());
                        }
                        finally
                        {
                            Scribe.loader.curXmlParent = oldXmlParent;
                            Scribe.loader.curParent = oldParent;
                            Scribe.loader.curPathRelToParent = oldPathRelToParent;
                        }
                    }
                }
            }
            finally
            {
                Scribe.ExitNode();
            }
        }

        public static void DrawScaleSetting(int x, int y, int textWidth, int entryWidth, int entryHeight, int scaleWidth, string label, string scaleLabel, ref int value, int minValue, Action<IntervalScale> setScale)
        {
            Rect labelRect = new Rect(0, y + 5, textWidth, entryHeight);
            Widgets.Label(labelRect, label);

            Rect fieldRect = new Rect(textWidth, y, entryWidth, entryHeight);
            string fieldBuffer = null; // Don't need string buffer for this
            Widgets.TextFieldNumeric(fieldRect, ref value, ref fieldBuffer, minValue, 1000);

            Rect scaleButton = new Rect(textWidth + entryWidth, y, scaleWidth, entryHeight);
            if (Widgets.ButtonText(scaleButton, scaleLabel))
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (IntervalScale scale in IntervalScale.Values)
                {
                    list.Add(new FloatMenuOption(scale.label.Translate(), delegate
                    {
                        setScale.Invoke(scale);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }
        }
    }
}
