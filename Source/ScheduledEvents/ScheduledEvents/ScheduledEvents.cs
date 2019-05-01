using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ScheduledEvents
{

    public class IncidentTarget
    {
        public static readonly IncidentTarget MAP = new IncidentTarget(0, "fair.ScheduledEvents.MapEvent", "Map_PlayerHome", true, e => 
        {
            return Find.Maps.Select(m => (IIncidentTarget)m);
            //return Enumerable.Repeat<IIncidentTarget>(Find.AnyPlayerHomeMap, 1);
        });
        public static readonly IncidentTarget WORLD = new IncidentTarget(1, "fair.ScheduledEvents.WorldEvent", "World", false, e =>
        {
            return Enumerable.Repeat<IIncidentTarget>(Find.World, 1);
        });
        public static readonly IncidentTarget CARAVAN = new IncidentTarget(2, "fair.ScheduledEvents.CaravanEvent", "Caravan", true, e => 
        {
            return Find.WorldObjects.Caravans.Select(c => (IIncidentTarget)c);
        });

        public static IEnumerable<IncidentTarget> Values
        {
            get
            {
                yield return MAP;
                yield return WORLD;
                yield return CARAVAN;
            }
        }

        // Custom save/load logic
        public static void Look(ref IncidentTarget value, string label)
        {
            int id = value == null ? -1 : value.id;
            Scribe_Values.Look(ref id, label, default(int), true);
            IncidentTarget found = Values.FirstOrDefault((target) => target.id == id);
            value = found;
        }

        public readonly int id;
        public readonly string label;
        public readonly string targetDefName;
        public readonly bool hasTargetSelector;
        public readonly Func<ScheduledEvent, IEnumerable<IIncidentTarget>> targetGetter;
        IncidentTarget(int id, string label, string targetDefName, bool hasTargetSelector, Func<ScheduledEvent, IEnumerable<IIncidentTarget>> targetGetter)
        {
            this.id = id;
            this.label = label;
            this.targetDefName = targetDefName;
            this.targetGetter = targetGetter;
            this.hasTargetSelector = hasTargetSelector;
        }

        public IncidentTargetTagDef GetTargetTag()
        {
            return DefDatabase<IncidentTargetTagDef>.AllDefs.FirstOrDefault(e => e.defName.Equals(targetDefName));
        }

        public IEnumerable<IncidentDef> GetAllIncidentDefs()
        {
            IncidentTargetTagDef targetDef = GetTargetTag();
            if (targetDef == null) return Enumerable.Empty<IncidentDef>();
            return DefDatabase<IncidentDef>.AllDefs.Where(e => e.TargetTagAllowed(targetDef));
        }

        public IEnumerable<IIncidentTarget> GetCurrentTarget(ScheduledEvent e)
        {
            return targetGetter.Invoke(e);
        }
        
    }

    public class TargetSelector
    {
        public static readonly TargetSelector EVERY = new TargetSelector(0, "fair.ScheduledEvents.SelEvery", (targets, action) => 
        {
            foreach (IIncidentTarget target in targets) action.Invoke(target);
        });
        public static readonly TargetSelector RANDOM_ONE = new TargetSelector(1, "fair.ScheduledEvents.SelRandomOne", (targets, action) => 
        {
            if (targets.Count() == 0) return;
            IIncidentTarget target = targets.RandomElementWithFallback();
            if (target != null) action.Invoke(target);

        });
        public static readonly TargetSelector FIRST = new TargetSelector(2, "fair.ScheduledEvents.SelFirst", (targets, action) =>
        {
            IIncidentTarget target = targets.FirstOrFallback(null);
            if (target != null) action.Invoke(target);
        });

        public static IEnumerable<TargetSelector> Values
        {
            get
            {
                yield return EVERY;
                yield return RANDOM_ONE;
                yield return FIRST;
            }
        }

        // Custom save/load logic
        public static void Look(ref TargetSelector value, string label)
        {
            int id = value.id;
            Scribe_Values.Look(ref id, label, default(int), true);
            TargetSelector found = Values.FirstOrDefault((sel) => sel.id == id);
            if (found == null) found = EVERY; // Default value
            value = found;
        }

        public readonly int id;
        public readonly string label;
        public readonly Action<IEnumerable<IIncidentTarget>, Action<IIncidentTarget>> action;
        private TargetSelector(int id, string label, Action<IEnumerable<IIncidentTarget>, Action<IIncidentTarget>> action)
        {
            this.id = id;
            this.label = label;
            this.action = action;
        }

        public void RunOn(IEnumerable<IIncidentTarget> targets, Action<IIncidentTarget> action)
        {
            this.action.Invoke(targets, action);
        }
        
    }

    public class IntervalScale
    {
        public static readonly IntervalScale HOURS = new IntervalScale(0, "fair.ScheduledEvents.Hours", GenDate.TicksPerHour);
        public static readonly IntervalScale DAYS = new IntervalScale(1, "fair.ScheduledEvents.Days", GenDate.TicksPerDay);
        public static readonly IntervalScale SEASONS = new IntervalScale(2, "fair.ScheduledEvents.Seasons", GenDate.TicksPerSeason);
        public static readonly IntervalScale YEARS = new IntervalScale(3, "fair.ScheduledEvents.Years", GenDate.TicksPerYear);

        public static IEnumerable<IntervalScale> Values
        {
            get
            {
                yield return HOURS;
                yield return DAYS;
                yield return SEASONS;
                yield return YEARS;
            }
        }

        // Custom save/load logic
        public static void Look(ref IntervalScale value, string label)
        {
            int id = value.id;
            Scribe_Values.Look(ref id, label, default(int), true);
            IntervalScale found = Values.FirstOrDefault((scale) => scale.id == id);
            if (found == null) found = HOURS; // Default value
            value = found;
        }

        public readonly int id;
        public readonly string label;
        public readonly int ticksPerUnit;
        IntervalScale(int id, string label, int ticksPerUnit)
        {
            this.id = id;
            this.label = label;
            this.ticksPerUnit = ticksPerUnit;
        }

    }

    public class ScheduledEvents : Mod
    {
        protected Vector2 settingsScrollPos = new Vector2();

        public ScheduledEvents(ModContentPack content) : base(content)
        {
            // Load settings?
            GetSettings<ScheduledEventsSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            GUI.BeginGroup(inRect);

            int y = 10;
            int labelHeight = 25;
            int entryHeight = 30;
            int textWidth = 300;
            int entryWidth = 100;
            int scaleWidth = 100;

            Rect outRect = new Rect(0, 0, inRect.width, inRect.height);
            int rowHeight = labelHeight + 35 + (entryHeight + 5) * 2 + 10;
            Rect scrollView = new Rect(0, 0, inRect.width - 20, 10 + rowHeight * ScheduledEventsSettings.events.Count + 35);
            Widgets.BeginScrollView(outRect, ref this.settingsScrollPos, scrollView);
            for (int i = 0; i < ScheduledEventsSettings.events.Count; i++)
            {
                ScheduledEvent e = ScheduledEventsSettings.events[i];
                Rect headerLabel = new Rect(0, y, textWidth, labelHeight);
                Widgets.Label(headerLabel, "fair.ScheduledEvents.SettingLabel".Translate(e.incidentName, e.incidentTarget.label.Translate()));

                if (e.incidentTarget.hasTargetSelector)
                {
                    Rect selectorButton = new Rect(textWidth, y, 200, labelHeight);
                    if (Widgets.ButtonText(selectorButton, e.targetSelector.label.Translate(e.incidentTarget.label.Translate())))
                    {
                        List<FloatMenuOption> list = new List<FloatMenuOption>();
                        foreach (TargetSelector sel in TargetSelector.Values)
                        {
                            list.Add(new FloatMenuOption(sel.label.Translate(e.incidentTarget.label.Translate()), delegate
                            {
                                e.targetSelector = sel;
                                base.WriteSettings();
                                base.DoSettingsWindowContents(scrollView); // Update button text
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(list));
                    }
                }

                y += labelHeight;

                Utils.DrawScaleSetting(0, y, textWidth, entryWidth, entryHeight, scaleWidth, "fair.ScheduledEvents.SettingRunEvery".Translate(), e.intervalScale.label.Translate(), ref e.interval, 1, (scale) =>
                {
                    e.intervalScale = scale;
                    base.WriteSettings();
                    base.DoSettingsWindowContents(scrollView); // Update button text
                });
                y += entryHeight + 5;

                Utils.DrawScaleSetting(0, y, textWidth, entryWidth, entryHeight, scaleWidth, "fair.ScheduledEvents.SettingOffsetBy".Translate(), e.offsetScale.label.Translate(), ref e.offset, 0, (scale) =>
                {
                    e.offsetScale = scale;
                    base.WriteSettings();
                    base.DoSettingsWindowContents(scrollView); // Update button text
                });
                y += entryHeight + 5;

                Rect removeButton = new Rect(0, y, 200, 30);
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 0.3f, 0.35f);
                if (Widgets.ButtonText(removeButton, "fair.ScheduledEvents.RemoveEvent".Translate()))
                {
                    ScheduledEventsSettings.events.RemoveAt(i);
                    base.WriteSettings();
                    base.DoSettingsWindowContents(scrollView); // Update window
                }
                GUI.color = oldColor;
                y += 35;

                Widgets.DrawLineHorizontal(0, y, scrollView.width);
                y += 10;
            }

            int addButtonX = 0;
            
            foreach (IncidentTarget target in IncidentTarget.Values)
            {
                Rect addButton = new Rect(addButtonX, y, 200, 30);
                if (Widgets.ButtonText(addButton, "fair.ScheduledEvents.AddEvent".Translate(target.label.Translate())))
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();

                    //Utils.LogMessage("Events: " + DefDatabase<IncidentDef>.AllDefs.Count());

                    foreach (IncidentDef incident in from d in target.GetAllIncidentDefs() orderby d.defName select d)
                    {
                        list.Add(new FloatMenuOption(incident.defName, delegate
                        {
                            ScheduledEventsSettings.events.Add(new ScheduledEvent(target, incident.defName));
                            Utils.LogDebug("Added scheduled " + target.label.Translate() + " event");
                            base.WriteSettings();
                            base.DoSettingsWindowContents(inRect); // Update window contents
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(list));
                }
                addButtonX += 205;
            }

            Rect debugLogging = new Rect(addButtonX, y, 200, 30);
            Widgets.CheckboxLabeled(debugLogging, "fair.ScheduledEvents.DebugLogging".Translate(), ref ScheduledEventsSettings.logDebug);
            y += 30;

            Widgets.EndScrollView();

            GUI.EndGroup();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "fair.ScheduledEvents.Title".Translate();
        }
    }


    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            Utils.LogMessage("Loaded " + ScheduledEventsSettings.events.Count + " events from settings.");
        }
    }
}
