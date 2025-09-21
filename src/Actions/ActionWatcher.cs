using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.EffectRange;
using ActionEffectRange.Drawing;
using ActionEffectRange.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Vector3Struct = FFXIVClientStructs.FFXIV.Common.Math.Vector3;

namespace ActionEffectRange.Actions
{
    internal static class ActionWatcher
    {
        private const float SeqExpiry = 2.5f; // this is arbitrary...

        private static uint lastSendActionSeq = 0;
        private static uint lastReceivedMainSeq = 0;

        private static readonly ActionSeqRecord playerActionSeqs = new(5);
        private static readonly HashSet<ushort> skippedSeqs = new();

        // Send what is executed; won't be called if queued but not yet executed
        //  or failed to execute (e.g. cast cancelled)
        // Information here also more accurate than from UseAction; handles combo/proc
        //  and target issues esp. with other plugins being used.
        // Not called for GT actions
        private delegate void SendActionDelegate(ulong targetObjectId, 
            byte actionType, uint actionId, ushort sequence, long a5, long a6, long a7, long a8, long a9);
        private static readonly Hook<SendActionDelegate>? SendActionHook;
        private static void SendActionDetour(ulong targetObjectId, 
            byte actionType, uint actionId, ushort sequence, long a5, long a6, long a7, long a8, long a9)
        {
            SendActionHook!.Original(targetObjectId, actionType, actionId, sequence, a5, a6, a7, a8, a9);

            try
            {
                LogUserDebug($"SendAction => target={targetObjectId:X}, " +
                    $"action={actionId}, type={actionType}, seq={sequence}");
#if DEBUG
                PluginLog.Debug($"** SendAction: targetId={targetObjectId:X}, " +
                    $"actionType={actionType}, actionId={actionId}, seq={sequence}, " +
                    $"a5={a5:X}, a6={a6:X}, a7={a7:X}, a8={a8:X}, a9={a9:X}");
                PluginLog.Debug($"** ---AcMgr: currentSeq{ActionManagerHelper.CurrentSeq}, " +
                    $"lastRecSeq={ActionManagerHelper.LastRecievedSeq}");
#endif
                lastSendActionSeq = sequence;

                if (!ShouldProcessAction(actionType, actionId))
                {
                    skippedSeqs.Add(sequence);
                    return;
                }

                var actionCategory = ActionData.GetActionCategory(actionId);
                if (!ShouldDrawForActionCategory(actionCategory))
                {
                    LogUserDebug($"---Skip action#{actionId}: " +
                        $"Not drawing for actions of category {actionCategory}");
                    skippedSeqs.Add(sequence);
                    return;
                }

                if (targetObjectId == 0 || targetObjectId == InvalidGameObjectId)
                {
                    LogUserDebug($"---Skip: Invalid target #{targetObjectId}");
                    return;
                }
                else if (targetObjectId == LocalPlayer!.EntityId)
                {
                    var snapshot = new SeqSnapshot(sequence);
                    playerActionSeqs.Add(new(actionId, snapshot, false));
                }
                else
                {
                    var target = ObejctTable.SearchById((uint)targetObjectId);
                    if (target != null)
                    {
                        var snapshot = new SeqSnapshot(sequence,
                            target.GameObjectId, target.Position);
                        playerActionSeqs.Add(new(actionId, snapshot, false));
                    }
                    else
                    {
                        PluginLog.Error($"Cannot find valid target #{targetObjectId:X} for action#{actionId}");
                        return;
                    }
                }
            } 
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }
        }

        private static readonly Hook<ActionManager.Delegates.UseActionLocation>? UseActionLocationHook;
        
        private static unsafe bool UseActionLocationDetour(ActionManager* actionManager, ActionType actionType, uint actionId, ulong targetObjectId, Vector3* location, uint param, byte a7)
        {
            var ret = UseActionLocationHook!.Original(actionManager, 
                actionType, actionId, targetObjectId, location, param, a7);
            try
            {
#if DEBUG
                PluginLog.Debug($"** UseActionLocation: actionType={actionType}, " +
                    $"actionId={actionId}, targetId={targetObjectId:X}, " +
                    $"loc={*location} " +
                    $"param={param}; ret={ret}");
#endif
                if (!ret) return ret;

                var seq = ActionManagerHelper.CurrentSeq;

                // Skip if already processed in SendAction; these are not GT actions
                if (seq == lastSendActionSeq) return ret;

                LogUserDebug($"UseActionLocation => " +
                    $"Possible GT action #{actionId}, type={actionType};" +
                    $"Seq={ActionManagerHelper.CurrentSeq}");

                if (!Config.DrawGT)
                {
                    LogUserDebug($"---Skip: Config: disabed for GT actions");
                    skippedSeqs.Add(seq);
                    return ret;
                }

                if (!ShouldProcessAction(actionType, actionId))
                {
                    skippedSeqs.Add(seq);
                    return ret;
                }

                var actionCategory = ActionData.GetActionCategory(actionId);
                if (!ShouldDrawForActionCategory(actionCategory))
                {
                    LogUserDebug($"---Skip action#{actionId}: " +
                        $"Not drawing for actions of category {actionCategory}");
                    skippedSeqs.Add(seq);
                    return ret;
                }

                // NOTE: Should've checked if the action could be mapped to some pet/pet-like actions
                // but currently none for those actions if we've reached here so just omit it for now

                playerActionSeqs.Add(new(actionId, new(seq), false));
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }
            return ret;
        }

        // useType == 0 when queued;
        // If queued action not executed immediately,
        //  useType == 1 when this function is called later to actually execute the action
        private static readonly Hook<ActionManager.Delegates.UseAction>? UseActionHook;
        // Detour used mainly for processing draw-when-casting
        // When applicable, drawing is triggered immediately
        
        private static unsafe bool UseActionDetour(ActionManager* actionManager, ActionType actionType, uint actionId, ulong targetObjectId, uint param, ActionManager.UseActionMode useType, uint pvp, bool* a8)
        {
            var ret = UseActionHook!.Original(actionManager, 
                actionType, actionId, targetObjectId, param, useType, pvp, a8);

            try
            {
                LogUserDebug($"UseAction => actionType={actionType}, " +
                    $"actionId={actionId}, targetId={targetObjectId}");
#if DEBUG
                PluginLog.Debug($"** UseAction: param={param}, useType={useType}, pvp={pvp}; " +
                    $"ret={ret}; CurrentSeq={ActionManagerHelper.CurrentSeq}");
#endif
                if (!DrawWhenCasting) return ret;

                if (!ActionManagerHelper.IsCasting)
                {
                    LogUserDebug($"---Skip: not casting");
                    return ret;
                }

                if (!ret)
                {
                    LogUserDebug($"---Skip: not drawing on useType={useType} && ret={ret}");
                    return ret;
                }

                var castActionId = ActionManagerHelper.CastActionId;

                if (!ShouldProcessAction(actionType, castActionId))
                    return ret;

                var actionIdsToDraw = new List<uint> { castActionId };
                if (ActionData.TryGetActionWithAdditionalEffects(
                    castActionId, out var additionals))
                    actionIdsToDraw.AddRange(additionals);

                foreach (var a in actionIdsToDraw)
                {
                    var erdata = EffectRangeDataManager.NewData(a);
                    if (erdata == null)
                    {
                        PluginLog.Error($"Cannot get data for action#{a}");
                        continue;
                    }

                    if (!ShouldDrawForActionCategory(erdata.Category, true))
                    {
                        LogUserDebug($"---Skip action#{erdata.ActionId}: " +
                            $"Not drawing for actions of category {erdata.Category}");
                        continue;
                    }

                    erdata = EffectRangeDataManager.CustomiseEffectRangeData(erdata);
                    if (!CheckShouldDrawPostCustomisation(erdata)) continue;

                    var seq = ActionManagerHelper.CurrentSeq;
                    float rotation = ActionManagerHelper.CastRotation;

                    if (erdata.IsGTAction)
                    {
                        var targetPos = new Vector3(
                            ActionManagerHelper.CastTargetPosX,
                            ActionManagerHelper.CastTargetPosY,
                            ActionManagerHelper.CastTargetPosZ);
                        LogUserDebug($"UseAction => Triggering draw-when-casting, " +
                            $"CastingActionId={castActionId}, GT action, " +
                            $"CastPosition={targetPos}, CastRotation={rotation}");
                        LogUserDebug($"---Adding DrawData for action #{castActionId} " +
                            $"from player, using cast position info");
                        EffectRangeDrawing.AddEffectRangeToDraw(seq,
                            DrawTrigger.Casting, erdata, LocalPlayer!.Position,
                            targetPos, rotation);
                    }
                    else
                    {
                        var castTargetId = ActionManagerHelper.CastTargetObjectId;
                        LogUserDebug($"UseAction => Triggering draw-when-casting, " +
                            $"CastingActionId={castActionId}, " +
                            $"CastTargetObjectId={castTargetId}, CastRotation={rotation}");

                        IGameObject? target = null;
                        if (castTargetId == LocalPlayer!.GameObjectId)
                            target = LocalPlayer;
                        else if (castTargetId != 0 
                            && castTargetId != InvalidGameObjectId)
                            target = ObejctTable.SearchById(castTargetId);

                        if (target != null)
                        {
                            LogUserDebug($"---Adding DrawData for action #{castActionId} " +
                                $"from player, using cast position info");
                            // We do not have GT actions here
                            EffectRangeDrawing.AddEffectRangeToDraw(
                                ActionManagerHelper.CurrentSeq, DrawTrigger.Casting, 
                                erdata, LocalPlayer!.Position, target.Position, rotation);
                        }
                        else LogUserDebug($"---Failed: Target #{castTargetId:X} not found");
                    }
                }
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }

            return ret;
        }

        
        private static readonly Hook<ActionEffectHandler.Delegates.Receive>? ReceiveActionEffectHook;
        
        //private static void ReceiveActionEffectDetour(ulong sourceObjectId, IntPtr sourceActor, IntPtr position, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail)
        private static unsafe void ReceiveActionEffectDetour(uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds)
        {
            ReceiveActionEffectHook!.Original(casterEntityId, casterPtr, 
                targetPos, header, effects, targetEntityIds);

            try
            {
#if DEBUG
                PluginLog.Debug($"** ReceiveActionEffect: src={casterEntityId:X}, " +
                    $"pos={targetPos->ToString()}; " +
                    $"AcMgr: CurrentSeq={ActionManagerHelper.CurrentSeq}, " +
                    $"LastRecSeq={ActionManagerHelper.LastRecievedSeq}");
#endif

                LogUserDebug($"ReceiveActionEffect => " +
                    $"source={casterEntityId:X}, target={header->AnimationTargetId.ObjectId:X}, " +
                    $"action={header->ActionId}, seq={header->SourceSequence}");
#if DEBUG
                PluginLog.Debug($"** ---effectHeader: target={header->AnimationTargetId:X}, " +
                    $"action={header->ActionId}, unkObjId={header->BallistaEntityId:X}, " +
                    $"seq={header->SourceSequence}, unk={header->RotationInt:X}");
#endif

                if (header->SourceSequence > 0)
                {
                    lastReceivedMainSeq = header->SourceSequence;
                    if (skippedSeqs.Contains(header->SourceSequence))
                    {
                        LogUserDebug($"---Skip: not processing Seq#{header->SourceSequence}");
                        return;
                    }
                }

                if (!IsPlayerLoaded)
                {
                    LogUserDebug($"---Skip: PC not loaded");
                    return;
                }

                if (casterEntityId != LocalPlayer!.EntityId
                    && (!PetWatcher.HasPetPresent 
                        || PetWatcher.GetPetEntityId() != casterEntityId))
                {
                    LogUserDebug($"---Skip: Effect triggered by others");
                    return;
                }

                var erdata = EffectRangeDataManager.NewData(header->ActionId);
                if (erdata == null)
                {
                    PluginLog.Error($"Cannot get data for action#{header->ActionId}");
                    return;
                }

                // Some additional effects (e.g. #29706 additional effect for Pneuma pvp)
                // have ActionCategory=0
                if (!ShouldDrawForActionCategory(erdata.Category, true))
                {
                    LogUserDebug($"---Skip action#{erdata.ActionId}: " +
                        $"Not drawing for actions of category {erdata.Category}");
                    return;
                }

                erdata = EffectRangeDataManager.CustomiseEffectRangeData(erdata);

                if (!CheckShouldDrawPostCustomisation(erdata)) return;

                var mainSeq = header->SourceSequence > 0
                        ? header->SourceSequence : lastReceivedMainSeq;

                if (casterEntityId == LocalPlayer!.EntityId)
                {
                    // Source is pc

                    // TODO: config on/off: auto triggered effects
                    //  (such as effects on time elapsed, receiving damage, ...)
                    bool drawForAuto = true; // placeholder

                    ActionSeqInfo? seqInfo = null;

                    // For additional effects, received data always has seq=0
                    // Match seq using predefined mapping and some heuristics
                    if (!ActionData.ShouldNotUseCachedSeq(header->ActionId))
                        seqInfo = FindRecordedSeqInfo(header->SourceSequence, header->ActionId);

                    if (seqInfo != null)
                    {
                        // Additional effect may have different target (e.g. self vs targeted enemy)
                        Vector3 trgtPos = erdata.IsGTAction
                            ? *targetPos
                            : (header->AnimationTargetId.Id == LocalPlayer.TargetObjectId
                                ? seqInfo.SeqSnapshot.TargetPosition
                                : seqInfo.SeqSnapshot.PlayerPosition);

                        LogUserDebug($"---Adding DrawData for action #{header->ActionId} " +
                            $"from player, using SeqSnapshot#{seqInfo.Seq}");
                        EffectRangeDrawing.AddEffectRangeToDraw(seqInfo.Seq,
                            DrawTrigger.Used, erdata, seqInfo.SeqSnapshot.PlayerPosition,
                            trgtPos, seqInfo.SeqSnapshot.PlayerRotation);
                    }
                    else if (drawForAuto)
                    {
                        LogUserDebug($"---Adding DrawData for action #{header->ActionId} " +
                            $"from player, using current position info");

                        if (erdata.IsGTAction)
                            EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                DrawTrigger.Used, erdata, LocalPlayer!.Position,
                                *targetPos,
                                LocalPlayer!.Rotation);
                        else
                        {
                            IGameObject? target = null;
                            if (header->AnimationTargetId.ObjectId == casterEntityId) // Self-targeting
                                target = LocalPlayer;
                            else if (header->AnimationTargetId.ObjectId != 0
                                && header->AnimationTargetId.ObjectId != InvalidGameObjectId)
                                target = ObejctTable.SearchById(header->AnimationTargetId.ObjectId);

                            if (target != null)
                            {
                                EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                    DrawTrigger.Used, erdata, LocalPlayer!.Position,
                                    target.Position, LocalPlayer!.Rotation);
                            }
                            else LogUserDebug($"---Failed: Target #{header->AnimationTargetId.Id:X} not found");
                        }
                    }
                    else LogUserDebug($"---Skip: Not drawing for auto-triggered action #{header->ActionId}");
                }
                else
                {
                    // Source may be player's pet/pet-like object

                    // NOTE: Always use current position infos here.
                    // Due to potential delay, info snapshot at the time
                    //  player action is used is not accurate for pet actions as well.
                    // E.g., when pet is moving, any other action will be delayed;
                    //  once the pet is settled on a location, the character positions
                    //  are snapshot and pet action is processed based on this snapshot;
                    // but this also means the snapshot produced when player used
                    //  the "parent" action is already out-of-date.

                    // Just ignore if the pet is no longer present at this point (e.g. due to delay).
                    // Not very common as the game already defers removing pet objects
                    //  possibly to account for delays
                    if (PetWatcher.HasPetPresent
                        && PetWatcher.GetPetEntityId() == casterEntityId)
                    {
                        if (PetWatcher.IsCurrentPetACNPet() && !Config.DrawACNPets)
                        {
                            LogUserDebug($"---Skip: Drawing for action#{header->ActionId} " +
                                "from ACN/SMN/SCH pets configured OFF");
                            return;
                        }
                        if (PetWatcher.IsCurrentPetNonACNNamedPet()
                            && !Config.DrawSummonedCompanions)
                        {
                            LogUserDebug($"---Skip: Drawing for action#{header->ActionId} " +
                                "from summoned companions of non-ACN based jobs configured OFF");
                            return;
                        }
                        if (PetWatcher.IsCurrentPetNameless()
                            && !Config.DrawGT)
                        {
                            // Assuming all nameless pets are ground placed objects ...
                            LogUserDebug($"---Skip: Drawing for action#{header->ActionId} " +
                                "from possibly ground placed object configured OFF");
                            return;
                        }

                        // TODO: Check if the effect is auto-triggered if it is from placed object?
                        // (Assuming placed obj does not move, cached seq snapshot can be used.)
                        // (Configurable opt)

                        LogUserDebug($"---Add DrawData for action #{header->ActionId} " +
                            $"from pet / pet-like object #{casterEntityId:X}, using current position info");

                        if (erdata.IsGTAction)
                            EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                DrawTrigger.Used, erdata, LocalPlayer!.Position,
                                *targetPos,
                                LocalPlayer!.Rotation);
                        else
                        {
                            IGameObject? target = null;
                            if (header->AnimationTargetId == casterEntityId) // Pet self-targeting
                                target = PetWatcher.GetPet();
                            else if (header->AnimationTargetId == LocalPlayer.TargetObjectId)
                                target = LocalPlayer;
                            else if (header->AnimationTargetId != 0
                                && header->AnimationTargetId != InvalidGameObjectId)
                                target = ObejctTable.SearchById((uint)header->AnimationTargetId);

                            if (target != null)
                            {
                                var source = PetWatcher.GetPet();
                                EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                    DrawTrigger.Used, erdata,
                                    PetWatcher.GetPetPosition(),
                                    target.Position, PetWatcher.GetPetRotation());
                            }
                            else LogUserDebug($"---Failed: Target #{header->AnimationTargetId:X} not found");
                        }
                    }
                    else LogUserDebug($"---Skip: source actor #{casterEntityId:X} not matching pc or pet");
                }
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }
        }


        #region Checks

        private static bool ShouldProcessAction(byte actionType, uint actionId)
        {
            if (!IsPlayerLoaded)
            {
                LogUserDebug($"---Skip: PC not loaded");
                return false;
            }
            if (!ShouldProcessActionType(actionType) 
                || !ShouldProcessAction(actionId))
            {
                LogUserDebug($"---Skip: Not processing " +
                    $"action#{actionId}, ActionType={actionType}");
                return false;
            }
            return true;
        }

        private static bool ShouldProcessActionType(uint actionType) 
            => actionType == 0x1 || actionType == 0xE; // pve 0x1, pvp 0xE

        private static bool ShouldProcessAction(ActionType actionType, uint actionId)
        {
            if (!IsPlayerLoaded)
            {
                LogUserDebug($"---Skip: PC not loaded");
                return false;
            }
            if (!ShouldProcessActionType(actionType) 
                || !ShouldProcessAction(actionId))
            {
                LogUserDebug($"---Skip: Not processing " +
                             $"action#{actionId}, ActionType={actionType}");
                return false;
            }
            return true;
        }

        private static bool ShouldProcessActionType(ActionType actionType) 
            => actionType == ActionType.Action || actionType == ActionType.PvPAction; // pve 0x1, pvp 0xE

        private static bool ShouldProcessAction(uint actionId)
            => !ActionData.IsActionBlacklisted(actionId);


        private static bool ShouldDrawForActionCategory(
            Enums.ActionCategory actionCategory, bool allowCateogry0 = false)
            => ActionData.IsCombatActionCategory(actionCategory)
            || Config.DrawEx && ActionData.IsSpecialOrArtilleryActionCategory(actionCategory)
            || allowCateogry0 && actionCategory == 0;

        // Only check for circle and donut in Large EffectRange check
        private static bool ShouldDrawForEffectRange(EffectRangeData data)
            => data.EffectRange > 0 
            && (!(data is CircleAoEEffectRangeData || data is DonutAoEEffectRangeData) 
                || Config.LargeDrawOpt != 1 
                || data.EffectRange < Config.LargeThreshold);

        // Note: will not draw for `None` (=0)
        private static bool ShouldDrawForHarmfulness(EffectRangeData data)
            => EffectRangeDataManager.IsHarmfulAction(data) && Config.DrawHarmful
            || EffectRangeDataManager.IsBeneficialAction(data) && Config.DrawBeneficial;


        private static bool CheckShouldDrawPostCustomisation(EffectRangeData data)
        {
            if (!ShouldDrawForEffectRange(data))
            {
                LogUserDebug($"---Skip action #{data.ActionId}: " +
                    $"Not drawing for actions of effect range = {data.EffectRange}");
                return false;
            }

            if (!ShouldDrawForHarmfulness(data))
            {
                LogUserDebug($"---Skip action #{data.ActionId}: " +
                    $"Not drawing for harmful/beneficial actions = {data.Harmfulness}");
                return false;
            }

            return true;
        }

        #endregion


        private static ActionSeqInfo? FindRecordedSeqInfo(
            ushort receivedSeq, uint receivedActionId)
        {
            foreach (var seqInfo in playerActionSeqs)
            {
                if (IsSeqExpired(seqInfo)) continue;
                if (receivedSeq > 0) // Primary effects from player actions
                {
                    if (receivedSeq == seqInfo.Seq)
                    {
                        LogUserDebug($"---* Recorded sequence matched");
                        return seqInfo;
                    }
                }
                else if (ActionData.AreRelatedPlayerTriggeredActions(
                    seqInfo.ActionId, receivedActionId))
                {
                    LogUserDebug($"---* Related recorded sequence found");
                    return seqInfo;
                }
            }
            LogUserDebug($"---* No recorded sequence matched");
            return null;
        }

        private static void ClearSeqRecordCache()
        {
            playerActionSeqs.Clear();
            skippedSeqs.Clear();
        }

        private static bool IsSeqExpired(ActionSeqInfo info)
            => info.ElapsedSeconds > SeqExpiry;

        private static void OnClassJobChangedClearCache(uint classJobId)
            => ClearSeqRecordCache();

        private static void OnTerritoryChangedClearCache(ushort terr)
            => ClearSeqRecordCache();


        static unsafe ActionWatcher()
        {
            UseActionHook ??= InteropProvider.HookFromAddress<ActionManager.Delegates.UseAction>(
                ActionManager.Addresses.UseAction.Value, UseActionDetour);
            UseActionLocationHook ??= InteropProvider.HookFromAddress<ActionManager.Delegates.UseActionLocation>(
                ActionManager.Addresses.UseActionLocation.Value, UseActionLocationDetour);
            
            ReceiveActionEffectHook = InteropProvider.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                ActionEffectHandler.MemberFunctionPointers.Receive, ReceiveActionEffectDetour
            );
            SendActionHook ??= InteropProvider.HookFromAddress<SendActionDelegate>(
                SigScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B E9 41 0F B7 D9"), 
                SendActionDetour);

            PluginLog.Information("ActionWatcher init:\n" +
                $"\tUseActionHook @{UseActionHook?.Address ?? IntPtr.Zero:X}\n" +
                $"\tUseActionLoactionHook @{UseActionLocationHook?.Address ?? IntPtr.Zero:X}\n" +
                $"\tReceiveActionEffectHook @{ReceiveActionEffectHook?.Address ?? IntPtr.Zero:X}\n" +
                $"\tSendActionHook @{SendActionHook?.Address ?? IntPtr.Zero:X}");
        }

        public static void Enable()
        {
            UseActionHook?.Enable();
            UseActionLocationHook?.Enable();
            SendActionHook?.Enable();
            ReceiveActionEffectHook?.Enable();

            ClientState.TerritoryChanged += OnTerritoryChangedClearCache;
            ClassJobWatcher.ClassJobChanged += OnClassJobChangedClearCache;
        }

        public static void Disable()
        {
            UseActionHook?.Disable();
            UseActionLocationHook?.Disable();
            SendActionHook?.Disable();
            ReceiveActionEffectHook?.Disable();

            ClientState.TerritoryChanged -= OnTerritoryChangedClearCache;
            ClassJobWatcher.ClassJobChanged -= OnClassJobChangedClearCache;
        }

        public static void Dispose()
        {
            Disable();
            UseActionHook?.Dispose();
            UseActionLocationHook?.Dispose();
            SendActionHook?.Dispose();
            ReceiveActionEffectHook?.Dispose();
        }

    }
}
