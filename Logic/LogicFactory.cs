namespace Halite3.Logic {
    /// This setup will make it easier to switch logic mid-game
    /// or to use different logic based on map size, available halite, and number of players
    public static class LogicFactory {
        public static Logic GetCollectLogic() {
            return new CollectLogic5();
        }

        public static Logic GetDropoffLogic() {
            if(GameInfo.Me.id.id == 0)
                return new DropoffLogic();
            else
                return new DropoffLogic2();
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

        /* /public static Logic GetProximityLogic() {
            return new FakeLogic();//ProximityLogic();
        }*/
    }
}