using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Causes the attacker to teleport near a target spot, then perform a melee attack. The client
    /// visualization moves the character locally beforehand, making the character appear to dash to the
    /// destination spot.
    ///
    /// After the ExecTime has elapsed, the character is immune to damage until the action ends.
    ///
    /// Since the "Range" field means "range when we can teleport to our target", we need another
    /// field to mean "range of our melee attack after dashing". We'll use the "Radius" field of the
    /// ActionDescription for that.
    /// </summary>
    /// <remarks>
    /// See MeleeAction for relevant discussion about targeting; we use the same concept here: preferring
    /// the chosen target, but using whatever is actually within striking distance at time of attack.
    /// </remarks>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Dash Attack Action")]
    public class DashAttackAction : Action
    {
        private Vector3 m_TargetSpot;

        private bool m_Dashed;

        // ActionUtils 의 「너무 가깝다」 거리(k_VeryCloseTeleportRange = 1 + 1)와 같다
        const float k_AdjacentTargetRange = 2f;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            var me = serverCharacter.physicsWrapper.Transform;
            // 고른 대상이 바로 옆(2m 안)이면 그 자리에서 대상을 보고 친다 — GetDashDestination 은 너무 가까운 목적지를 「바라보는 쪽 20m」로 바꿔,
            // 휴대폰(대상을 고르고 버튼)에서는 붙어 있는 적을 지나쳐 늘 헛쳤다. 마우스로 땅을 찍던 원래 조작을 전제로 한 동작이다
            if (Data.TargetIds is { Length: > 0 } && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(Data.TargetIds[0], out var target)
                && Vector3.Distance(me.position, target.transform.position) <= k_AdjacentTargetRange)
            {
                m_TargetSpot = me.position;
                var face = target.transform.position;
                face.y = me.position.y;
                me.LookAt(face);
            }
            else
            {
                // remember the exact spot we'll stop.
                m_TargetSpot = ActionUtils.GetDashDestination(me, Data.Position, true, Config.Range, Config.Range);

                // snap to face our destination. This ensures the client visualization faces the right way while "pretending" to dash
                me.LookAt(m_TargetSpot);
            }

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);

            // tell clients to visualize this action
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);

            return ActionConclusion.Continue;
        }

        public override void Reset()
        {
            base.Reset();
            m_TargetSpot = default;
            m_Dashed = false;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            return ActionConclusion.Continue;
        }

        public override void End(ServerCharacter serverCharacter)
        {
            // Anim2 contains the name of the end-loop-sequence trigger
            if (!string.IsNullOrEmpty(Config.Anim2))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim2);
            }

            // we're done, time to teleport!
            serverCharacter.Movement.Teleport(m_TargetSpot);

            // and then swing!
            PerformMeleeAttack(serverCharacter);
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            // OtherAnimatorVariable contains the name of the cancellation trigger
            if (!string.IsNullOrEmpty(Config.OtherAnimatorVariable))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.OtherAnimatorVariable);
            }

            // because the client-side visualization of the action moves the character visualization around,
            // we need to explicitly end the client-side visuals when we abort
            serverCharacter.clientCharacter.ClientCancelActionsByPrototypeIDRpc(ActionID);

        }

        public override void BuffValue(BuffableValue buffType, ref float buffedValue)
        {
            if (TimeRunning >= Config.ExecTimeSeconds && buffType == BuffableValue.PercentDamageReceived)
            {
                // we suffer no damage during the "dash" (client-side pretend movement)
                buffedValue = 0;
            }
        }

        private void PerformMeleeAttack(ServerCharacter parent)
        {
            // perform a typical melee-hit. But note that we are using the Radius field for range, not the Range field!
            IDamageable foe = MeleeAction.GetIdealMeleeFoe(Config.IsFriendly ^ parent.IsNpc,
                parent.physicsWrapper.DamageCollider,
                                                            Config.Radius, 0.0f,
                                                            (Data.TargetIds != null && Data.TargetIds.Length > 0 ? Data.TargetIds[0] : 0));

            if (foe != null)
            {
                foe.ReceiveHitPoints(parent, -Config.Amount);
            }
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            if (m_Dashed) { return ActionConclusion.Stop; } // we're done!

            return ActionConclusion.Continue;
        }
    }
}
