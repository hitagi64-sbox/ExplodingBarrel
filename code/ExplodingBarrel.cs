using Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox;

[Library("ent_exploding_barrel"), HammerEntity]
[EditorModel("models/red_barrel_small.vmdl")]
public partial class ExplodingBarrel : ModelEntity
{
    const float barrelLaunchForce = 50000;

    [Property(Title = "Maximum push force")]
    public float maxForce { get; set; } = 5000;
    [Property(Title = "Maximum push range")]
    public float forceMaxDistance { get; set; } = 3000;

    [Property(Title = "Maximum damage")]
    public float maxDamage { get; set; } = 350;
    [Property(Title = "Maximum damage range")]
    public float damageMaxDistance { get; set; } = 400;

    [Property(Title = "Minimum speed for the barrel to deal damage")]
    public float physicalDamageMinSpeed { get; set; } = 300;
    [Property(Title = "Damage divider (velocity/...=damage)")]
    public float physicalDamageDivider { get; set; } = 20;

    [Property(Title = "Speed at which the barrel will blow up when it collides")]
    public float physicalDamageBlowUpSpeed { get; set; } = 900;

    // Should all barrels in this barrels range explode
    //  in the same tick? (At the same time)
    // Having them explode at the same time will cause all
    //  barrels to go straight up in the air.
    const bool allBarrelsExplodeInSameTick = false;

    // A health of 20 requires about 3 bullet shots using
    //  the pistol to explode the barrel.
    [Property(Title = "Starting health of the barrel")]
    public float startingHealth { get; set; } = 20;

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/red_barrel_small.vmdl");
        SetupPhysicsFromModel(PhysicsMotionType.Dynamic);

        if (!Game.IsClient)
            Health = startingHealth;
    }

    override public void TakeDamage(DamageInfo info)
    {
        if (Game.IsClient)
            return;

        if (LifeState == LifeState.Dead)
            return;

        float dmg = info.Damage;

        // Make it more resistant against physical impact.
        if (info.HasTag("physics_impact"))
            dmg /= 2;

        Health -= dmg;

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
        // State will be set to dead if it has exploded.
        // If it has already exploded, don't explode it again.
        if (LifeState == LifeState.Dead)
            return;

        // Set the barrel to dead and health to 0.
        LifeState = LifeState.Dead;
        Health = 0;

        // Go through each entity in the world and if it's
        //  in the area, apply an impulse and damage to it.
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
            damage.Position = entity.Position;
            damage.Tags = new HashSet<string>{"blast"};
            if (damage.Damage > 0)
                entity.TakeDamage(damage);
        }

        // Create sound and particles.
        Sound.FromEntity("sound/break_plastic_barrel_01.sound", this);
        Particles p = Particles.Create("particles/explosion/barrel_explosion/explosion_barrel.vpcf");
        p.SetPosition(0, Position + (Rotation.Up*5));

        setExploded();
    }

    void setExploded()
    {
        // Set entity to bottom part of an exploded barrel.
        // Replace model instead of spawning entity because
        //  then we can check wether the barrel exploded in
        //  another script. (Might be useful for someone)
        SetModel("models/red_barrel_small_bottom.vmdl");
        SetupPhysicsFromModel(PhysicsMotionType.Dynamic);

        // Create a new entity for the top part.
        ModelEntity top = new ModelEntity();
        top.SetModel("models/red_barrel_small_top.vmdl");
        top.SetupPhysicsFromModel(PhysicsMotionType.Dynamic);
        top.Position = Position + (Rotation.Up * 5);
        top.Rotation = Rotation;
        top.PhysicsGroup.ApplyImpulse(Rotation.Up * barrelLaunchForce);
    }

    override protected void OnPhysicsCollision(CollisionEventData eventData)
    {
        if (Game.IsClient)
            return;
        if (eventData.Other.Entity == null)
            return;
        if (eventData.Other.Entity == this) 
            return;

        // If there is an impact, deal damage to whatever we hit.
        if (eventData.Velocity.Length > physicalDamageMinSpeed)
        {
            DamageInfo damage = new DamageInfo();
            damage.Damage = eventData.Velocity.Length/physicalDamageDivider;
            damage.Force = eventData.Velocity.Length/10.0f;
            damage.Attacker = this;
            damage.Position = Position;// Position is required for breaking glass.
            damage.Tags = new HashSet<string>{"physics_impact"};
            eventData.Other.Entity.TakeDamage(damage);
        }

        // If the impact is hard enough, blow up the barrel.
        if (eventData.Velocity.Length > physicalDamageBlowUpSpeed)
        {
            DamageInfo damage2 = new DamageInfo();
            damage2.Damage = startingHealth + 10;// Add 10 just to be sure it blows up.
            damage2.Attacker = this;
            TakeDamage(damage2);
        }

    }
}
