#!/bin/bash

RELEASE="Debug"
IS_AOT=true
dotnet publish -r osx-arm64 -o dist -p:PublishAot=$IS_AOT -c $RELEASE src/Moongate.Server \
  && ./dist/Moongate.Server "$@" \
  && rm -Rf moongate \
  && rm -Rf dist
