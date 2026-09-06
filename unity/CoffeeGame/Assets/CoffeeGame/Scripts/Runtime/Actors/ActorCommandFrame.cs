using UnityEngine;

namespace CoffeeGame.Actors
{
    // Both AI and player input go through the same motor/action acceptance rules.
    public struct ActorCommandFrame
    {
        public Vector2 Move;
        public bool WorldSpace;
        public bool Jump;
        public bool Dodge;
        public bool Sword;
        public bool Magic;
        public bool Special;
        public bool GuardHeld;
        public bool GuardPressed;
    }
}
