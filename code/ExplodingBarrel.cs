using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox;

public class ExplodingBarrel : ModelEntity
{
    float barrelLaunchForce = 50000;

    float maxForce = 5000;
    float forceMaxDistance = 3000;

    float maxDamage = 350;
    float damageMaxDistance = 400;

    // Should all barrels in this barrels range explode
    //  in the same tick? (At the same time)
    // Having them explode at the same time will cause all
    //  barrels to go straight up in the air.
    bool allBarrelsExplodeInSameTick = false;

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/red_barrel_small.vmdl");
        SetupPhysicsFromModel(PhysicsMotionType.Dynamic);

        Health = 20;
    }

    override public void TakeDamage(DamageInfo info)
    {
        if (Game.IsClient)
            return;

        if (LifeState == LifeState.Dead)
            return;

        Health -= info.Damage;

        if (allBarrelsExplodeInSameTick)
        {
            if (Health <= 0)
                explode();
        }
    }

    [Event.Tick]
    void tick()
    {
        if (Game.IsClient)
            return;
        if (!allBarrelsExplodeInSameTick)
        {
            if (Health <= 0)
                explode();
        }
    }

    float sqrtMaxAndDistance(float maxValue, float maxDistance, float distance)
    {
        float result;

        float D = MathF.Sqrt(maxDistance)/maxValue;
        result = maxValue - (MathF.Sqrt(distance) / D);

        return result;
    }

    void explode()
    {
        if (LifeState == LifeState.Dead)
            return;

        LifeState = LifeState.Dead;
        Health = 0;

        foreach (var entity in Entity.All.ToList())
        {
            if (entity == null)
                continue;
            if (entity.PhysicsGroup == null)
                continue;
            if (entity == this)
                continue;

            Vector3 difference = entity.Position - Position;
            Vector3 direction = difference.Normal;
            float distance = difference.Length;

            float force = sqrtMaxAndDistance(maxForce, forceMaxDistance, distance);
            if (force > 0)
                entity.PhysicsGroup.ApplyImpulse(direction * force);

            DamageInfo damage = new DamageInfo();
            damage.Attacker = this;
            damage.Damage = sqrtMaxAndDistance(maxDamage, damageMaxDistance, distance);
            damage.Force = direction * force;
            if (damage.Damage > 0)
                entity.TakeDamage(damage);
        }

        Sound.FromEntity("sound/break_plastic_barrel_01.sound", this);
        Particles p = Particles.Create("particles/explosion/barrel_explosion/explosion_barrel.vpcf");
        p.SetPosition(0, Position + (Rotation.Up*5));

        setExploded();
    }

    void setExploded()
    {
        SetModel("models/red_barrel_small_bottom.vmdl");
        SetupPhysicsFromModel(PhysicsMotionType.Dynamic);

        ModelEntity top = new ModelEntity();
        top.SetModel("models/red_barrel_small_top.vmdl");
        top.SetupPhysicsFromModel(PhysicsMotionType.Dynamic);
        top.Position = Position + (Rotation.Up * 5);
        top.Rotation = Rotation;
        top.PhysicsGroup.ApplyImpulse(Rotation.Up * barrelLaunchForce);
    }
}
