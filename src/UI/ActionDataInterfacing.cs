﻿using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.Enums;
using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace ActionEffectRange.UI
{
    public static class ActionDataInterfacing
    {
        public static IEnumerable<Lumina.Excel.Sheets.Action> GetAllPartialMatchActionExcelRows(string input, bool alsoMatchId, int maxCount, bool playerCombatActionOnly, Func<Lumina.Excel.Sheets.Action, bool> filter)
            => ActionData.ActionExcelSheet?
                .Where(row => (row.Name.ToString().Contains(input, StringComparison.CurrentCultureIgnoreCase) || alsoMatchId && row.RowId.ToString().Contains(input)) && (!playerCombatActionOnly || ActionData.IsPlayerCombatAction(row)) && filter(row))
                .Take(maxCount) ?? [];

        public static string GetActionDescription(Lumina.Excel.Sheets.Action row)
        {
            var classjobRow = row.ClassJob.Value;
            var classjob = classjobRow.RowId > 0
                ? $" [{classjobRow.Abbreviation}]" : string.Empty;
            var pvp = row.IsPvP ? " [PvP]" : string.Empty;
            return $"#{row.RowId} {row.Name}{classjob}{pvp}";
        }

        public static string GetAoETypeLabel(ActionAoEType type)
            => type switch
            {
                ActionAoEType.None => "N/A",
                ActionAoEType.Circle or ActionAoEType.Circle2 => "Circle",
                ActionAoEType.Cone or ActionAoEType.Cone2 => "Cone",
                ActionAoEType.Line or ActionAoEType.Line2 => "Line",
                ActionAoEType.GT => "Circle (GT)",
                ActionAoEType.DashAoE => "Dash (Line)",
                ActionAoEType.Donut => "Donut",
                _ => "?"
            };

        public static string GetAoETypeLabel(byte castType)
            => GetAoETypeLabel((ActionAoEType)castType);

        public static ActionAoEType[] AoETypeSelections
            => new ActionAoEType[]
            {
                ActionAoEType.Circle,
                ActionAoEType.Cone,
                ActionAoEType.Line,
                ActionAoEType.DashAoE,
                ActionAoEType.Donut,
                ActionAoEType.GT
            };

        public static ActionHarmfulness[] ActionHarmfulnessesSelections
            => Enum.GetValues<ActionHarmfulness>();

        public static float DegToCycle(float deg) => deg / 360;
        
        public static float CycleToDeg(float cycle) => cycle * 360;

        public static float DegToRad(float deg)
            => MathF.PI * deg / 180;

        public static float RadToDeg(float rad)
            => 180 * rad / MathF.PI;
    }
}
