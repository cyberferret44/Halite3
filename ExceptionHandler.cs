using System;
namespace Halite3 {
    public static class ExceptionHandler {
        // using this class for local debugging
        // will allow me to detect unexpected states with exceptions
        // without having those exceptions occur on the server 
        public static void Raise(string message) {
            if(GameInfo.IsLocal) {
                throw new Exception(message);
            }
        }
    }
}