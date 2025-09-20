using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.Enums;

namespace ActionEffectRange.Actions.EffectRange
{
    public class BidirectedLineAoEEffectRangeData : EffectRangeData
    {
        public float RotationOffset;
        public byte Width => XAxisModifier;

        public BidirectedLineAoEEffectRangeData(uint actionId, 
            uint actionCategory, bool isGT, ActionHarmfulness harmfulness, 
            sbyte range, byte effectRange, byte xAxisModifier, byte castType,
            float rotationOffset = 0, bool isOriginal = false)
            : base(actionId, actionCategory, isGT, harmfulness,
                  range, effectRange, xAxisModifier, castType, isOriginal)
        {
            RotationOffset = rotationOffset;
        }

        public BidirectedLineAoEEffectRangeData(
            Lumina.Excel.Sheets.Action actionRow, float rotationOffset = 0)
            : this(actionRow.RowId, actionRow.ActionCategory.RowId, actionRow.TargetArea,
                  ActionData.GetActionHarmfulness(actionRow), actionRow.Range, 
                  actionRow.EffectRange, actionRow.XAxisModifier, actionRow.CastType, 
                  rotationOffset, isOriginal: true)
        { }

        protected override string AdditionalFieldsToString()
            => $"RotationOffset: {RotationOffset}";
    }
}
