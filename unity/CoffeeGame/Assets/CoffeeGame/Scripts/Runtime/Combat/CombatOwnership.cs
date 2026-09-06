using UnityEngine;

namespace CoffeeGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatOwnership : MonoBehaviour
    {
        [SerializeField] private GameObject owner;

        public GameObject Owner => owner;

        public void Initialize(GameObject value)
        {
            owner = value;
        }

        public static void Assign(GameObject target, GameObject owner)
        {
            if (target == null)
            {
                return;
            }

            CombatOwnership ownership = target.GetComponent<CombatOwnership>();
            if (ownership == null)
            {
                ownership = target.AddComponent<CombatOwnership>();
            }

            ownership.Initialize(owner);
        }

        public static GameObject Resolve(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            CombatOwnership ownership = target.GetComponentInParent<CombatOwnership>();
            return ownership != null && ownership.owner != null ? ownership.owner : target;
        }
    }
}
