# Hunters' Pact — Witch Hunt Co-op Mod

**Mod cooperativo para 2 jugadores de [Witch Hunt](https://store.steampowered.com/app/661790/) (BepInEx 5).**
Invita a un amigo de Steam y salgan a cazar juntos en un mundo ampliado.

> ⚠️ **Versión beta (v0.2.2).** Probado en Linux (Proton) y Windows, con invitación de Steam entre dos cuentas.
> Si algo falla, abre un *issue* contando qué pasó y adjunta tu archivo `BepInEx\LogOutput.log`.

## Qué trae

- **Cooperativo por Steam:** "Crear sala" abre la ventana de Steam para invitar a un amigo; el amigo acepta y entra. Sin IP, sin abrir puertos ni VPN.
- **Mundo compartido:** enemigos, daño, oro, cofres, tienda, eventos y viajes sincronizados. El anfitrión manda sobre monstruos y botín.
- **Caer y levantarse:** si uno cae, el compañero tiene tiempo para levantarlo; el perro también puede hacerlo.
- **Mundo ampliado:** zona norte de 1 km, castillo gótico con bóveda del tesoro, laberinto, jardín, minas y pueblos en ruinas.
- **Jefe opcional "La Madre de la Guarida":** dos fases, vuelos variados, rocas, lluvia de piedras y música propia. A su guarida se entra de a dos y la salida se sella hasta tomar la llave.
- **Armas nuevas:** escopeta de dos cañones, ballesta y bola de cristal; libros de habilidades nuevos en la tienda.
- **Eventos:** Luna de sangre, tormenta, niebla, noche oscura y noche tranquila.
- **Tres dificultades:** Normal, Difícil y Pesadilla.
- **Sonidos del compañero:** se oyen sus disparos, recargas, cambios de arma y pasos.
- **Armas del compañero a la vista:** ves en su mano el arma que lleva (pistola, mosquete, estoque, escopeta, ballesta o bola de cristal). Al apuntar o disparar la sube a la cara con las dos manos (la pistola con el brazo estirado), da el culatazo al disparar y lanza estocadas con el estoque.
- **Fuego amigo opcional:** se elige al crear la partida; con él, los disparos pueden herir al compañero.
- **Historia completa en cooperativo:** los jefes (hombre lobo, árbol de sacrificio, bruja, hijo del diablo y gusano gigante) mueren en los dos juegos y la historia avanza igual para ambos. Tras el gusano, ningún viaje exige estar juntos para no cortar la cinemática final; al terminar los créditos la partida se marca como terminada y se guarda una copia en `coop_respaldos`.
- La partida cooperativa se guarda aparte (`savefile_coop.sav`): **no toca tu partida normal**.

## Instalación

1. Descarga `HuntersPact-v0.1.0-beta.zip` desde [Releases](../../releases).
2. En Steam: clic derecho en **Witch Hunt → Administrar → Explorar archivos locales**.
3. Descomprime el zip **ahí, al lado de `Witch_Hunt.exe`** (deben quedar `winhttp.dll`, `doorstop_config.ini` y la carpeta `BepInEx`).
4. **Solo en Linux (Proton):** en Steam → Witch Hunt → Propiedades → Opciones de lanzamiento:
   `WINEDLLOVERRIDES="winhttp=n,b" %command%`
5. Activa la interfaz de Steam en el juego (Steam → Configuración → En la partida).

Los dos jugadores necesitan **el mismo paquete** y su propia copia de Witch Hunt en Steam.

## Cómo jugar

- **Anfitrión:** menú principal → **Crear sala** → elige a tu amigo en la ventana de Steam (el botón **Invitar amigo** la vuelve a abrir).
- **Invitado:** acepta la invitación en Steam. Si el juego estaba cerrado, Steam lo abre y entra solo; si estabas jugando, vuelve al menú principal.
- Con los dos conectados, el anfitrión elige **Continuar** o **Partida nueva** (dificultad, fuego amigo y modo permanente).

## Desinstalar

Borra de la carpeta del juego: `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` y la carpeta `BepInEx`.

## Compilar desde el código

Requisitos: .NET SDK y BepInEx 5.4.23 (win_x64) descomprimido en `descargas/bepinex/`.

```
dotnet build -c Release                    # versión pública
dotnet build -c Release -p:Pruebas=true    # versión de pruebas (órdenes de depuración)
```

El proyecto busca el juego en `~/.local/share/Steam/steamapps/common/Witch Hunt` (cámbialo con `-p:GameDir=...`).

## Créditos

Sonidos y licencias: ver [`sonidos/CREDITOS.txt`](sonidos/CREDITOS.txt). BepInEx © sus autores (LGPL-2.1).
Witch Hunt es de su desarrollador; este mod no incluye archivos del juego.

## Licencia

[MIT](LICENSE) © 2026 Angel

---

## English

**Hunters' Pact** is a 2-player co-op mod for Witch Hunt (BepInEx 5). Host with **Crear sala** ("Create room") and invite a Steam friend; the friend accepts the Steam invite and joins — no IP or port forwarding needed. Enemies, loot, gold, chests, shop, events and travel are synced; the world is expanded (north zone, gothic castle and vault, maze, mines) and there is an optional two-phase cave boss. You see the weapon your partner is holding, and they raise it to aim and shoot; optional friendly fire. The whole story can be played in co-op up to the ending; finished co-op saves are backed up in `coop_respaldos`. The co-op save is separate from your normal save.

**Install:** download the release zip, extract it next to `Witch_Hunt.exe` (Steam → Manage → Browse local files). On Linux/Proton add the launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`. Both players need the same version. The in-game texts are in Spanish.

**Beta:** please report bugs as GitHub issues with your `BepInEx\LogOutput.log`.
