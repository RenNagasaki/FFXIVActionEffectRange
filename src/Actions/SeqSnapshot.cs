﻿using ActionEffectRange.Helpers;
using System.Numerics;
using System;

namespace ActionEffectRange.Actions
{
    public class SeqSnapshot
    {
        public readonly ushort Seq;
        public readonly Vector3 PlayerPosition;
        public readonly float PlayerRotation;
        public readonly ulong TargetObjectId;
        public readonly Vector3 TargetPosition;
        public readonly ulong PetObjectId;
        public readonly Vector3 PetPosition;
        public readonly float PetRotation;
        public readonly DateTime CreatedTime = DateTime.Now;
        public double ElapsedSeconds 
            => (DateTime.Now - CreatedTime).TotalSeconds;
        public bool HadValidTarget => TargetObjectId != 0
            && TargetObjectId != InvalidGameObjectId;
        public bool WasPetPresent => PetObjectId != 0 
            && PetObjectId != InvalidGameObjectId;

        public SeqSnapshot(ushort seq, Vector3 playerPos, float playerRotation,
            ulong targetObjId, Vector3 targetPos,
            ulong petObjId, Vector3 petPos, float petRotation)
        {
            Seq = seq;
            PlayerPosition = playerPos;
            PlayerRotation = playerRotation;
            TargetObjectId = targetObjId;
            TargetPosition = targetPos;
            PetObjectId = petObjId;
            PetPosition = petPos;
            PetRotation = petRotation;
        }

        public SeqSnapshot(ushort seq, ulong targetObjId, Vector3 targetPos)
            : this(seq, LocalPlayer?.Position ?? new(), LocalPlayer?.Rotation ?? 0,
                  targetObjId, targetPos,
                  PetWatcher.GetPetObjectId(),
                  PetWatcher.GetPetPosition(),
                  PetWatcher.GetPetRotation())
        { }

        public SeqSnapshot(ushort seq)
            : this(seq, ClientState.LocalPlayer?.TargetObjectId ?? 0, LocalPlayer?.Position ?? new())
        { }

        public SeqSnapshot(ushort seq, ulong targetObjId, Vector3 targetPos,
            ulong petObjId, Vector3 petPos, float petRotation)
            : this(seq, LocalPlayer?.Position ?? new(), LocalPlayer?.Rotation ?? 0,
                  targetObjId, targetPos, petObjId, petPos, petRotation)
        { }

        public SeqSnapshot(ushort seq, ulong petObjId, Vector3 petPos, float petRotation)
            : this(seq, petObjId, petPos, petObjId, petPos, petRotation)
        { }
    }
}
