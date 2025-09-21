namespace ActionEffectRange
{
    internal class Game
    {
        public static bool IsPlayerLoaded 
            => ClientState.LocalContentId != 0 && ClientState.LocalPlayer != null;

        public static Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter?
            LocalPlayer => ClientState.LocalPlayer;

        public static bool IsPvPZone
            => ClientState.TerritoryType > 0 && (DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?
                .GetRow(ClientState.TerritoryType).IsPvpZone ?? false);

        public const uint InvalidGameObjectId
            = 0xE0000000;

    }
}
