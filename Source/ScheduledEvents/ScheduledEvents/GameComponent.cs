using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScheduledEvents
{
    public class SEGameComponent : GameComponent
    {

        private readonly Game game;
        private readonly List<TickEvent> events;

        public SEGameComponent(Game game) : base()
        {
            this.game = game;
            this.events = new List<TickEvent>();
        }

        public override void FinalizeInit()
        {
            // For some reason, currentTick during this is not correct
            ReloadEvents();
            base.FinalizeInit();
        }

        public void ReloadEvents()
        {
            events.Clear();
            int currentTick = this.game.tickManager.TicksAbs;
            Utils.LogDebug("Loading scheduled events...");
            foreach (ScheduledEvent e in ScheduledEventsSettings.events)
            {
                int nextEventTick = e.GetNextEventTick(currentTick);
                if (nextEventTick <= 0)
                {
                    Utils.LogDebug(e.incidentName + " event has invalid next tick");
                    continue;
                }
                Utils.LogDebug($"Event {e.incidentName} will happen on {GenDate.HourOfDay(nextEventTick, 0)}h, {GenDate.DateFullStringAt(nextEventTick, Vector2.zero)}");
                TickEvent.AddToList(events, nextEventTick, e);
            }
        }

        public override void GameComponentTick()
        {
            int currentTick = this.game.tickManager.TicksAbs;
            TickEvent nextEvent = events.FirstOrDefault();
            if (nextEvent != null && nextEvent.tick <= currentTick)
            {
                Utils.LogDebug($"Firing scheduled {nextEvent.e.incidentName} event!");
                // Remove from list
                events.Remove(nextEvent);
                int nextEventTick = nextEvent.e.GetNextEventTick(currentTick);

                IncidentDef incident = nextEvent.e.GetIncident();
                if (incident != null)
                {
                    IEnumerable<IIncidentTarget> targets = nextEvent.e.incidentTarget.GetCurrentTarget(nextEvent.e);
                    if (targets.Count() > 0)
                    {
                        foreach (IIncidentTarget target in targets)
                        {
                            if (incident.TargetAllowed(target))
                            {
                                // This is basically taken from Dialog_DebugActionMenu (debug menu source)
                                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, target);
                                if (incident.pointsScaleable)
                                {
                                    StorytellerComp stComp = Find.Storyteller.storytellerComps.First((StorytellerComp x) => x is StorytellerComp_OnOffCycle || x is StorytellerComp_RandomMain);
                                    parms = stComp.GenerateParms(incident.category, parms.target);
                                }
                                incident.Worker.TryExecute(parms);
                            }
                            else
                            {
                                Utils.LogDebugWarning($"Event target was invalid");
                            }
                        }
                    }
                    else
                    {
                        Utils.LogDebugWarning($"Event found 0 targets");
                    }
                }
                else
                {
                    Utils.LogWarning($"Could not fire event, since it could not find an IncidentDef");
                }
                Utils.LogDebug($"Next event will happen on {GenDate.HourOfDay(nextEventTick, 0)}h, {GenDate.DateFullStringAt(nextEventTick, Vector2.zero)}");
                //Utils.LogDebug($"Hours until: {(nextEventTick - currentTick) / GenDate.TicksPerHour}");
                TickEvent.AddToList(events, nextEventTick, nextEvent.e);
            }
            //Current.Game.storyteller.TryFire()
            base.GameComponentTick();
        }

    }
}
