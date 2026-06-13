#!/bin/bash

set -e

PATH=$PATH:/opt/homebrew/bin/dotnet
export PATH
DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.300/libexec
export DOTNET_ROOT

dotnet build runtime/sdl/CivOne.SDL.csproj -c DebugMacOS 
dotnet build runtime/sdl/CivOne.SDL.csproj -c ReleaseMacOS

#runtime/sdl/bin/Debug/net10.0/CivOne.SDL
runtime/sdl/bin/Release/net10.0/CivOne.SDL
