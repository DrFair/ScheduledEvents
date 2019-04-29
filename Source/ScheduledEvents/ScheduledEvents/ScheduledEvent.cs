using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace ScheduledEvents
{

    public class ScheduledEvent
    {
        public bool enabled = true; // If the event is enabled
        public readonly IncidentTarget incidentTarget = null;

        // The reason we have to use name, is because when loading this, incident defs are not yet loaded.
        public readonly string incidentName = null;

        public int interval = 1; // The interval of which the events occur
        public IntervalScale intervalScale = IntervalScale.HOURS; // The interval scale

        public int offset = 0; // The offset before the events start
        public IntervalScale offsetScale = IntervalScale.HOURS; // The offset scale

        public ScheduledEvent(IncidentTarget target, string incidentName)
        {
            this.incidentTarget = target;
            this.incidentName = incidentName;
        }

        public IncidentDef GetIncident()
        {
            if (incidentName == null) return null;
            return DefDatabase<IncidentDef>.AllDefs.FirstOrDefault(e => e.defName.Equals(incidentName));
        }

        public int GetNextEventTick(int currentTick)
        {
            int intervalInTicks = intervalScale.ticksPerUnit * interval;
            if (intervalInTicks <= 0) return -1;

            int offsetTicks = offsetScale.ticksPerUnit * offset;
            if (currentTick < offsetTicks) currentTick = offsetTicks;

            int nextTick = currentTick - (currentTick % intervalInTicks) + intervalInTicks;
            nextTick += (offsetTicks % intervalInTicks);
            
            return nextTick;
        }

        public void Scribe()
        {
            Scribe_Values.Look(ref enabled, "enabled", default(bool), true);
            Scribe_Values.Look(ref interval, "interval", default(int), true);
            IntervalScale.Look(ref intervalScale, "intervalScale");
            Scribe_Values.Look(ref offset, "offset", default(int), true);
            IntervalScale.Look(ref offsetScale, "offsetScale");
        }
    }
    
    public class TickEvent
    {
        public readonly int tick;
        public readonly ScheduledEvent e;

        // Adds the scheduled event to the list sorted
        public static void AddToList(List<TickEvent> list, int tick, ScheduledEvent e)
        {
            for (int i = 0; i < list.Count; i++)
            {
                TickEvent o = list[i];
                if (tick < o.tick)
                {
                    list.Insert(i, new TickEvent(tick, e));
                    return;
                }
            }
            // If it wasn't inserted in for loop, do it here
            list.Add(new TickEvent(tick, e));
        }

        private TickEvent(int tick, ScheduledEvent e)
        {
            this.tick = tick;
            this.e = e;
        }

    }
}
