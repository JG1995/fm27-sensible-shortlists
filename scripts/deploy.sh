#!/usr/bin/env bash
# Builds the plugin and copies it into the game's BepInEx plugins folder.
# The game loads plugins at startup, so restart it afterwards.
set -euo pipefail

cd "$(dirname "$0")/.."

game_dir="${FM27_GAME_DIR:-/mnt/c/Program Files (x86)/Steam/steamapps/common/Football Manager 27}"
plugin_dir="$game_dir/BepInEx/plugins/SensibleShortlists"
build_dir="src/SensibleShortlists.Plugin/bin/Release/net6.0"

if [ ! -d "$game_dir/BepInEx/plugins" ]; then
  echo "BepInEx plugins folder not found under: $game_dir" >&2
  echo "Install BepInEx 6 IL2CPP into FM27, or set FM27_GAME_DIR." >&2
  exit 1
fi

dotnet build src/SensibleShortlists.Plugin --configuration Release --nologo --verbosity quiet

mkdir -p "$plugin_dir"
if ! cp "$build_dir/SensibleShortlists.dll" "$build_dir/SensibleShortlists.Core.dll" "$plugin_dir/"; then
  echo "Copy failed. If the game is running it holds the plugin files open: close it and deploy again." >&2
  exit 1
fi

echo "Deployed to $plugin_dir"
echo "Restart the game to load it."
