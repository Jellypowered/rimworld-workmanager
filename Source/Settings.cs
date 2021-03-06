﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using UnityEngine;
using Verse;

namespace WorkManager
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class Settings : ModSettings
    {
        public static bool AllCleaners = true;
        public static bool AllHaulers = true;
        public static bool AssignAllWorkTypes;
        public static bool AssignMultipleDoctors = true;
        internal static MethodInfo IsBadWorkMethod;
        public static int UpdateFrequency = 24;

        public static void DoWindowContents(Rect rect)
        {
            var options = new Listing_Standard();
            options.Begin(rect);
            var optionRect = options.GetRect(Text.LineHeight);
            var fieldRect = optionRect;
            var labelRect = optionRect;
            fieldRect.xMin = optionRect.xMax - optionRect.width * (1 / 8f);
            labelRect.xMax = fieldRect.xMin;
            TooltipHandler.TipRegion(optionRect, Resources.Strings.UpdateIntervalTooltip);
            Widgets.DrawHighlightIfMouseover(optionRect);
            Widgets.Label(labelRect, Resources.Strings.UpdateInterval);
            var updateFrequencyBuffer = UpdateFrequency.ToString();
            Widgets.TextFieldNumeric(fieldRect, ref UpdateFrequency, ref updateFrequencyBuffer, 1, 120);
            options.Gap(options.verticalSpacing);
            options.CheckboxLabeled(Resources.Strings.AssignMultipleDoctors, ref AssignMultipleDoctors,
                Resources.Strings.AssignMultipleDoctorsTooltip);
            options.CheckboxLabeled(Resources.Strings.AssignAllWorkTypes, ref AssignAllWorkTypes,
                Resources.Strings.AssignAllWorkTypesTooltip);
            options.CheckboxLabeled(Resources.Strings.AllHaulers, ref AllHaulers, Resources.Strings.AllHaulersTooltip);
            options.CheckboxLabeled(Resources.Strings.AllCleaners, ref AllCleaners,
                Resources.Strings.AllCleanersTooltip);
            options.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref UpdateFrequency, "UpdateFrequency", 24);
            Scribe_Values.Look(ref AssignMultipleDoctors, "AssignMultipleDoctors", true);
            Scribe_Values.Look(ref AssignAllWorkTypes, "AssignAllWorkTypes");
            Scribe_Values.Look(ref AllHaulers, "AllHaulers", true);
            Scribe_Values.Look(ref AllCleaners, "AllCleaners", true);
        }
    }
}