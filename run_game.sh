dotnet build

#!/bin/sh
for (( c=1; c<=1; c++ ))
do
    ./halite --replay-directory replays/ -vvv --width 32 --height 32 "dotnet bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 40 --height 40 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 48 --height 48 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 56 --height 56 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 64 --height 64 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    
    #./halite --replay-directory replays/ -vvv --width 32 --height 32 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 40 --height 40 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 48 --height 48 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    
    #./halite --replay-directory replays/ -vvv --width 32 --height 32 "dotnet Halite3/bin/Debug/derp5/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 40 --height 40 "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 48 --height 48 "dotnet Halite3/bin/Debug/derp5/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 56 --height 56 "dotnet Halite3/bin/Debug/derp5/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
    #./halite --replay-directory replays/ -vvv --width 64 --height 64 "dotnet Halite3/bin/Debug/derp5/MyBot.dll" "dotnet Halite3/bin/Debug/netcoreapp2.0/MyBot.dll"
done