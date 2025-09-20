﻿using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace ActionEffectRange.Drawing.Types
{
    public class CircleAoEDrawData : DrawData
    {
        public readonly Vector3 Centre;
        public readonly int Radius;

        public CircleAoEDrawData(Vector3 centre, byte baseEffectRange, 
            byte xAxisModifier, uint ringColour, uint fillColour)
            : base(ringColour, fillColour)
        {
            Centre = centre;
            Radius = baseEffectRange + xAxisModifier;
        }

        public override unsafe void Draw(ImDrawListPtr drawList)
        {
            if (Config.LargeDrawOpt == 1 && Radius >= Config.LargeThreshold) 
                return;  // no draw large

            var points = new Vector2[Config.NumSegments];
            var seg = 2 * MathF.PI / Config.NumSegments;
            for (int i = 0; i < Config.NumSegments; i++)
            {
                camera.WorldToScreen(new(
                    Centre.X + Radius * MathF.Sin(i * seg),
                    Centre.Y,
                    Centre.Z + Radius * MathF.Cos(i * seg)), out var p);

                points[i] = p;
                drawList.PathLineTo(p);
            }

            if (Config.Filled 
                && (Config.LargeDrawOpt == 0 || Radius < Config.LargeThreshold))
            {
                drawList.PathFillConvex(FillColour);
                foreach (var p in points)
                    if (!float.IsNaN(p.X)) drawList.PathLineTo(p);
            }
            if (Config.OuterRing)
                drawList.PathStroke(RingColour, ImDrawFlags.Closed, Config.Thickness);
            drawList.PathClear();
        }
    }
}
