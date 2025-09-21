using FFXIVClientStructs.FFXIV.Client.Game;
using System.Runtime.InteropServices;

namespace ActionEffectRange.Helpers
{
    internal unsafe static class ActionManagerHelper
    {
        private static readonly ActionManager* ActionMgrPtr;

        public static ushort CurrentSeq => ActionMgrPtr != null
            ? ActionMgrPtr->LastUsedActionSequence : (ushort)0;
        public static ushort LastRecievedSeq => ActionMgrPtr != null
            ? ActionMgrPtr->LastHandledActionSequence : (ushort)0;

        public static bool IsCasting => ActionMgrPtr != null
            && ActionMgrPtr->CastActionType != 0; 
        public static uint CastActionId => ActionMgrPtr != null
            ? ActionMgrPtr->CastActionId : 0u;
        public static ulong CastTargetObjectId => ActionMgrPtr != null
            ? ActionMgrPtr->CastTargetId.Id : 0u;
        public static float CastTargetPosX => ActionMgrPtr != null
            ? ActionMgrPtr->CastTargetPosition.X : 0f;
        public static float CastTargetPosY => ActionMgrPtr != null
            ? ActionMgrPtr->CastTargetPosition.Y : 0f;
        public static float CastTargetPosZ => ActionMgrPtr != null
            ? ActionMgrPtr->CastTargetPosition.Z : 0f;
        // The player rotation when castings
        public static float CastRotation => ActionMgrPtr != null
            ? ActionMgrPtr->CastRotation : 0f;
        
        static ActionManagerHelper()
        {
            ActionMgrPtr = ActionManager.Instance();
            if (ActionMgrPtr == null)
                PluginLog.Warning("Ptr to ActionManager is 0");
        }
    }
}
