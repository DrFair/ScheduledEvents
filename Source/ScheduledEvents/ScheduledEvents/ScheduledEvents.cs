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
        public static readonly IncidentTarget MAP = new IncidentTarget(0, "fair.ScheduledEvents.MapEvent", "Map_PlayerHome", e => 
        {
            return Find.Maps.Select(m => (IIncidentTarget)m);
            //return Enumerable.Repeat<IIncidentTarget>(Find.AnyPlayerHomeMap, 1);
        });
        public static readonly IncidentTarget WORLD = new IncidentTarget(1, "fair.ScheduledEvents.WorldEvent", "World", e =>
        {
            return Enumerable.Repeat<IIncidentTarget>(Find.World, 1);
        });
        public static readonly IncidentTarget CARAVAN = new IncidentTarget(2, "fair.ScheduledEvents.CaravanEvent", "Caravan", e => 
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
        public readonly Func<ScheduledEvent, IEnumerable<IIncidentTarget>> targetGetter;
        IncidentTarget(int id, string label, string targetDefName, Func<ScheduledEvent, IEnumerable<IIncidentTarget>> targetGetter)
        {
            this.id = id;
            this.label = label;
            this.targetDefName = targetDefName;
            this.targetGetter = targetGetter;
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
                Rect headerLabel = new Rect(0, y, scrollView.width, labelHeight);
                Widgets.Label(headerLabel, "fair.ScheduledEvents.SettingLabel".Translate(e.incidentName, e.incidentTarget.label.Translate()));
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

                if (Widgets.ButtonText(removeButton, "fair.ScheduledEvents.RemoveEvent".Translate()))
                {
                    ScheduledEventsSettings.events.RemoveAt(i);
                    base.WriteSettings();
                    base.DoSettingsWindowContents(scrollView); // Update window
                }
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
