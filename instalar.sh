#!/usr/bin/env bash
# Witch Hunt Coop — instala o quita BepInEx y el mod en Witch Hunt (Steam, Linux/Proton).
# Uso: ./instalar.sh            instala o actualiza
#      ./instalar.sh quitar     desinstala todo (BepInEx incluido)
set -euo pipefail
AQUI="$(cd "$(dirname "$0")" && pwd)"
JUEGO="${WITCH_HUNT_DIR:-$HOME/.local/share/Steam/steamapps/common/Witch Hunt}"
BEPINEX="$AQUI/descargas/bepinex"
DLL="$AQUI/bin/Release/net35/WitchHuntCoop.dll"
[ -f "$DLL" ] || DLL="$AQUI/WitchHuntCoop.dll"   # paquete sin código fuente

[ -f "$JUEGO/Witch_Hunt.exe" ] || { echo "No encuentro Witch Hunt en: $JUEGO (usa WITCH_HUNT_DIR=...)"; exit 1; }
if pgrep -i '^witch_hunt' >/dev/null; then echo "Cierra Witch Hunt primero."; exit 1; fi

if [ "${1:-}" = "quitar" ]; then
    rm -rf "$JUEGO/BepInEx" "$JUEGO/winhttp.dll" "$JUEGO/doorstop_config.ini" "$JUEGO/.doorstop_version" "$JUEGO/changelog.txt" "$JUEGO/steam_appid.txt"
    echo "Quitado. Borra también la opción de lanzamiento en Steam."
    exit 0
fi

[ -f "$BEPINEX/winhttp.dll" ] || { echo "Falta BepInEx en $BEPINEX"; exit 1; }
cp -r "$BEPINEX/." "$JUEGO/"
mkdir -p "$JUEGO/BepInEx/plugins"
cp "$DLL" "$JUEGO/BepInEx/plugins/"
echo "Instalado en: $JUEGO"
echo
echo "En Steam: Witch Hunt → Propiedades → General → Opciones de lanzamiento:"
echo '    WINEDLLOVERRIDES="winhttp=n,b" %command%'
