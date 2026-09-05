namespace CoffeeGame.UI
{
    public static class RivalPortraitCatalog
    {
        public const string WeaknessChallengerResource = "Art/UI/Rivals/rival_weakness_challenger_v1";
        public const string SplitInkResource = "Art/UI/Rivals/rival_split_ink_v1";

        public static string ResourcePath(string rivalId)
        {
            return rivalId == CoffeeGame.Domain.RivalCharacterIds.SplitInk
                ? SplitInkResource
                : WeaknessChallengerResource;
        }
    }
}
