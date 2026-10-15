#!/bin/sh

ARCHITECTURE="x64" # Bit of a workaround for macOS on Apple Silicon
PROJECT="Venture.OS"

cosmos check
cosmos build -a $ARCHITECTURE -p $PROJECT

if [ "$1" = "-h" ] || [ "$1" = "--headless" ]; then
    echo "Running in headless mode..."
    cosmos run -a $ARCHITECTURE -p $PROJECT --headless
    exit 0
else
    cosmos run -a $ARCHITECTURE -p $PROJECT
    exit 0
fi