namespace Halite3.Logic {
    /// This setup will make it easier to switch logic mid-game
    /// or to use different logic based on map size, available halite, and number of players
    public static class LogicFactory {
        public static Logic GetCollectLogic() {
            return new CollectLogic();
        }

        public static Logic GetDropoffLogic() {
            return new DropoffLogic();
        }

        public static Logic GetEndOfGameLogic() {
            return new EndOfGameLogic();
        }

        public static Logic GetCombatLogic() {
            //if(GameInfo.PlayerCount == 2)
                return new TwoPlayerCombatLogic();
            //else
             //   return new EmptyLogic();
        }
    }
}