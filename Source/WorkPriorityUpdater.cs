﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace WorkManager
{
    [UsedImplicitly]
    public class WorkPriorityUpdater : MapComponent
    {
        private readonly IEnumerable<WorkTypeDef> _commonWorkTypes = new HashSet<WorkTypeDef>
        {
            DefDatabase<WorkTypeDef>.GetNamed("Firefighter"),
            DefDatabase<WorkTypeDef>.GetNamed("Patient"),
            DefDatabase<WorkTypeDef>.GetNamed("PatientBedRest"),
            DefDatabase<WorkTypeDef>.GetNamed("BasicWorker")
        };

        private readonly IEnumerable<WorkTypeDef> _mentalWorkTypes = new HashSet<WorkTypeDef>
        {
            DefDatabase<WorkTypeDef>.GetNamed("Hauling"), DefDatabase<WorkTypeDef>.GetNamed("Cleaning")
        };

        private int _currentHour = -1;

        public WorkPriorityUpdater(Map map) : base(map) { }

        private void AssignDoctors()
        {
            var doctors = map.mapPawns.FreeColonistsSpawned.Where(pawn =>
                    !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && !pawn.Dead && !pawn.Downed && !pawn.InMentalState)
                .ToList();
            if (!doctors.Any()) { return; }
            var doctorCount = 0;
            var maxSkillValue =
                (int) Math.Floor(doctors.Max(pawn => pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDefOf.Doctor)));
            #if DEBUG
            Log.Message($"Work Manager: Max doctoring skill value = '{maxSkillValue}'", true);
            #endif
            foreach (var pawn in doctors.Where(pawn =>
                pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDefOf.Doctor) >= maxSkillValue))
            {
                #if DEBUG
                Log.Message($"Work Manager: Assigning '{pawn.LabelShort}' as a doctor", true);
                #endif
                pawn.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
                doctorCount++;
            }
            if (Settings.AssignMultipleDoctors)
            {
                {
                    var patientCount = map.mapPawns.FreeColonistsSpawned.Count(pawn => pawn.Downed);
                    #if DEBUG
                    Log.Message($"Work Manager: Patient count = '{patientCount}'", true);
                    #endif
                    while (doctorCount < patientCount)
                    {
                        var doctor = doctors
                            .OrderByDescending(pawn => pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDefOf.Doctor))
                            .FirstOrDefault(pawn => pawn.workSettings.GetPriority(WorkTypeDefOf.Doctor) == 0);
                        if (doctor == null) { break; }
                        #if DEBUG
                        Log.Message($"Work Manager: Assigning '{doctor.LabelShort}' as a doctor", true);
                        #endif
                        doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
                        doctorCount++;
                    }
                }
            }
        }

        private void AssignHunters()
        {
            var hunters = map.mapPawns.FreeColonistsSpawned.Where(pawn =>
                    !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) && !pawn.Dead && !pawn.Downed &&
                    !pawn.InMentalState &&
                    !pawn.story.traits.HasTrait(TraitDefOf.Brawler) &&
                    (pawn.skills.GetSkill(SkillDefOf.Shooting).passion >
                     pawn.skills.GetSkill(SkillDefOf.Melee).passion ||
                     pawn.skills.GetSkill(SkillDefOf.Shooting).passion ==
                     pawn.skills.GetSkill(SkillDefOf.Melee).passion &&
                     pawn.skills.GetSkill(SkillDefOf.Shooting).Level >= pawn.skills.GetSkill(SkillDefOf.Melee).Level))
                .ToList();
            if (!hunters.Any()) { return; }
            var maxSkillValue =
                (int) Math.Floor(hunters.Max(pawn => pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDefOf.Hunting)));
            #if DEBUG
            Log.Message($"Work Manager: Max hunting skill value = '{maxSkillValue}'", true);
            #endif
            foreach (var pawn in hunters.Where(pawn =>
                pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDefOf.Hunting) >= maxSkillValue))
            {
                #if DEBUG
                Log.Message($"Work Manager: Assigning '{pawn.LabelShort}' as a hunter", true);
                #endif
                pawn.workSettings.SetPriority(WorkTypeDefOf.Hunting, 1);
            }
        }

        private void AssignLeftoverWorkTypes()
        {
            #if DEBUG
            Log.Message("-- Work Manager: Assigning leftover work types... --", true);
            #endif
            var pawns = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => !pawn.Dead && !pawn.Downed && !pawn.InMentalState).ToList();
            if (!pawns.Any()) { return; }
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(o =>
                !_commonWorkTypes.Contains(o) && o != WorkTypeDefOf.Doctor && o != WorkTypeDefOf.Hunting).ToList();
            const int priority = 4;
            if (Settings.AssignAllWorkTypes)
            {
                foreach (var pawn in pawns)
                {
                    foreach (var workType in workTypes.Where(w =>
                        !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.GetPriority(w) == 0))
                    {
                        #if DEBUG
                        Log.Message(
                            $"Work Manager: Setting {pawn.LabelShort}'s priority of '{workType.labelShort}' to {priority}",
                            true);
                        #endif
                        pawn.workSettings.SetPriority(workType, priority);
                    }
                }
            }
            else
            {
                var leftoverWorkTypes = workTypes.Where(w => !pawns.Any(p => p.workSettings.GetPriority(w) > 0));
                foreach (var workType in leftoverWorkTypes)
                {
                    var pawn = pawns.OrderBy(p => workTypes.Count(w => p.workSettings.GetPriority(w) > 0)).First();
                    #if DEBUG
                    Log.Message(
                        $"Work Manager: Setting {pawn.LabelShort}'s priority of '{workType.labelShort}' to {priority}",
                        true);
                    #endif
                    pawn.workSettings.SetPriority(workType, priority);
                }
            }
            #if DEBUG
            Log.Message("---------------------", true);
            #endif
        }

        private void AssignWorkersByPassion()
        {
            #if DEBUG
            Log.Message("-- Work Manager: Assigning workers by passion... --", true);
            #endif
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(pawn =>
                !pawn.Dead && !pawn.Downed && !pawn.InMentalState).ToList();
            if (!pawns.Any()) { return; }
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(o =>
                !_commonWorkTypes.Contains(o) && o != WorkTypeDefOf.Doctor && o != WorkTypeDefOf.Hunting).ToList();
            foreach (var pawn in pawns)
            {
                foreach (var workType in workTypes.Where(workType =>
                    !pawn.WorkTypeIsDisabled(workType) && pawn.workSettings.GetPriority(workType) == 0))
                {
                    int priority;
                    switch (pawn.skills.MaxPassionOfRelevantSkillsFor(workType))
                    {
                        case Passion.Major:
                            priority = 2;
                            #if DEBUG
                            Log.Message(
                                $"Work Manager: Setting {pawn.LabelShort}'s priority of '{workType.labelShort}' to {priority} (passion = {Passion.Major.ToString()})",
                                true);
                            #endif
                            pawn.workSettings.SetPriority(workType, priority);
                            break;
                        case Passion.Minor:
                            priority = 3;
                            #if DEBUG
                            Log.Message(
                                $"Work Manager: Setting {pawn.LabelShort}'s priority of '{workType.labelShort}' to {priority} (passion = {Passion.Minor.ToString()})",
                                true);
                            #endif
                            pawn.workSettings.SetPriority(workType, priority);
                            break;
                    }
                }
            }
            #if DEBUG
            Log.Message("---------------------", true);
            #endif
        }

        private void AssignWorkersBySkill()
        {
            #if DEBUG
            Log.Message("-- Work Manager: Assigning workers by skill... --", true);
            #endif
            var pawns = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => !pawn.Dead && !pawn.Downed && !pawn.InMentalState).ToList();
            if (!pawns.Any()) { return; }
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(o =>
                !_commonWorkTypes.Contains(o) && o != WorkTypeDefOf.Doctor && o != WorkTypeDefOf.Hunting &&
                o.relevantSkills.Any());
            const int priority = 1;
            foreach (var workType in workTypes)
            {
                var maxSkillValue =
                    (int) Math.Floor(pawns.Max(pawn => pawn.skills.AverageOfRelevantSkillsFor(workType)));
                foreach (var pawn in pawns.Where(o => o.skills.AverageOfRelevantSkillsFor(workType) >= maxSkillValue))
                {
                    #if DEBUG
                    Log.Message(
                        $"Work Manager: Setting {pawn.LabelShort}'s priority of '{workType.labelShort}' to {priority} (skill = {pawn.skills.AverageOfRelevantSkillsFor(workType)}, max = {maxSkillValue})",
                        true);
                    #endif
                    pawn.workSettings.SetPriority(workType, priority);
                }
            }
            #if DEBUG
            Log.Message("---------------------", true);
            #endif
        }

        private void AssignWorkForIdlePawns()
        {
            #if DEBUG
            Log.Message("-- Work Manager: Assigning work for idle pawns... --", true);
            #endif
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(pawn =>
                !pawn.Dead && !pawn.Downed && !pawn.InMentalState && !pawn.Drafted && pawn.mindState.IsIdle).ToList();
            if (!pawns.Any()) { return; }
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(o =>
                !_commonWorkTypes.Contains(o) && o != WorkTypeDefOf.Doctor && o != WorkTypeDefOf.Hunting).ToList();
            const int priority = 4;
            foreach (var pawn in pawns)
            {
                foreach (var workType in workTypes.Where(w =>
                    !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.GetPriority(w) == 0))
                {
                    #if DEBUG
                    Log.Message(
                        $"Work Manager: Setting {pawn.LabelShort}'s priority of '{workType.labelShort}' to {priority}",
                        true);
                    #endif
                    pawn.workSettings.SetPriority(workType, priority);
                }
            }
            #if DEBUG
            Log.Message("---------------------", true);
            #endif
        }

        private void AssignWorkForMentalPawns()
        {
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(
                pawn => !pawn.Dead && !pawn.Downed && pawn.InMentalState);
            foreach (var pawn in pawns)
            {
                var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(o =>
                    !_commonWorkTypes.Contains(o) && o != WorkTypeDefOf.Doctor);
                foreach (var workType in workTypes)
                {
                    pawn.workSettings.SetPriority(workType, _mentalWorkTypes.Contains(workType) ? 2 : 3);
                }
            }
        }

        private void AssignWorkPriorities()
        {
            var pawns = map.mapPawns.FreeColonistsSpawned;
            if (!pawns.Any()) { return; }
            ResetWorkPriorities();
            AssignDoctors();
            AssignHunters();
            AssignWorkersBySkill();
            AssignWorkersByPassion();
            AssignLeftoverWorkTypes();
            AssignWorkForIdlePawns();
            AssignWorkForMentalPawns();
            foreach (var pawn in pawns) { pawn.workSettings.Notify_UseWorkPrioritiesChanged(); }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if ((Find.TickManager.TicksGame + GetHashCode()) % 60 != 0 || GenLocalDate.HourOfDay(map) == _currentHour)
            {
                return;
            }
            #if DEBUG
            Log.Message("----- Work Manager: Updating work priorities... -----", true);
            #endif
            _currentHour = GenLocalDate.HourOfDay(map);
            AssignWorkPriorities();
            #if DEBUG
            Log.Message("----------------------------------------------------", true);
            #endif
        }

        private void ResetWorkPriorities()
        {
            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                #if DEBUG
                Log.Message($"Work Manager: Resetting work priorities for '{pawn.LabelShort}'", true);
                #endif
                pawn.workSettings.DisableAll();
                if (pawn.Dead || pawn.Downed) { continue; }
                foreach (var workType in _commonWorkTypes) { pawn.workSettings.SetPriority(workType, 1); }
            }
        }
    }
}