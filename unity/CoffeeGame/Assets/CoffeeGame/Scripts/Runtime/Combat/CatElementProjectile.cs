using System;
using System.Collections.Generic;
using CoffeeGame.Actors;
using UnityEngine;

namespace CoffeeGame.Combat
{
    public enum CatProjectileElement { Fire, Wind }
    public sealed class CatElementProjectile : MonoBehaviour
    {
        private readonly HashSet<Health> hitTargets=new HashSet<Health>();
        private Vector3 direction;private float speed,age;private int damage;private GameObject source;private long attackId;
        public CatProjectileElement Element { get; private set; }
        public event Action<CatElementProjectile> Destroyed;
        public event Action<Health> Hit;
        public void Initialize(CatProjectileElement element,Vector3 travel,int amount,float velocity,GameObject owner)
        {
            Element=element;direction=travel.sqrMagnitude>.001f?travel.normalized:Vector3.forward;damage=amount;speed=Mathf.Max(.1f,velocity);source=owner;
            attackId=new DamageInfo(0,owner,Vector3.zero,Vector3.zero).AttackId;CombatOwnership.Assign(gameObject,owner);
            var fx=CatElementVfx.Spawn(element==CatProjectileElement.Fire?CatElementEffect.FireShot:CatElementEffect.WindShot,transform.position,direction,element==CatProjectileElement.Fire?.2f:.85f,2.5f,owner);
            fx.transform.SetParent(transform,true);
        }
        private void Update() => Advance(CombatClock.DeltaTime(gameObject));
        public void Advance(float seconds)
        {
            if(seconds<=0)return;float distance=speed*seconds,radius=Element==CatProjectileElement.Fire?.2f:.58f;
            Vector3 start=transform.position;transform.position+=direction*distance;age+=seconds;
            // Capsule catches both initial overlaps and travel between frames.
            foreach(var overlap in Physics.OverlapCapsule(start,transform.position,radius,~0,QueryTriggerInteraction.Collide)) {
                var target=overlap.GetComponentInParent<Health>();
                if(target==null||!target.IsAlive||!DamageFaction.CanDamage(source,target)||hitTargets.Contains(target))continue;
                if(target.ApplyDamage(new DamageInfo(damage,source,target.transform.position,direction*.55f,attackId))) {
                    hitTargets.Add(target);Hit?.Invoke(target);
                    CatElementVfx.Spawn(Element==CatProjectileElement.Fire?CatElementEffect.FireImpact:CatElementEffect.WindImpact,target.transform.position+Vector3.up*.5f,direction,.6f,.32f,source);
                    if(Element==CatProjectileElement.Fire){Destroy(gameObject);return;}
                }
            }
            if(age>=2.4f)Destroy(gameObject);
        }
        private void OnDestroy()=>Destroyed?.Invoke(this);
    }
}
