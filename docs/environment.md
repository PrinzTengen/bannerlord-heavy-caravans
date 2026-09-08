# Entwicklungsumgebung - HeavyCaravans

Stand: 2026-09-08, ermittelt aus der lokalen Installation
`E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`.

| Komponente | Version | Quelle |
|---|---|---|
| Mount & Blade II: Bannerlord | v1.4.8 (Changeset 119303) | `Configs/LauncherData.xml` (Native/SandBoxCore/Sandbox = `v1.4.8.119303`); `package_info.txt` nennt zusätzlich `Environment: PC@v1.3.4` (Steam-Branch-Tag, ungenauer als die Modul-Version) |
| TAOM | v2.0.26 | `Modules/TAOM/SubModule.xml` |
| TAOM.Dependencies | v2.0.26.0 | `Configs/LauncherData.xml` |
| Bannerlord.Harmony (Modul) | 2.3.6.220 | `Bannerlord.Harmony.dll` FileVersion (lokal per Symlink bereitgestellte Kopie) |
| 0Harmony (Lib) | 2.3.6.0 | `0Harmony.dll` FileVersion |
| Bannerlord.ButterLib | 2.10.0.0 | `Bannerlord.ButterLib.dll` FileVersion |
| Coop (Testreferenz) | Bannerlord Online v1.3.5 (Autor: Vyacheslav Spirin) | `Modules/BannerlordOnline/SubModule.xml` |
| .NET SDK (lokal installiert) | 10.0.400 | `dotnet --list-sdks` |
| CI (.github/workflows) | .NET SDK 6.0.x | `ci.yml` / `release.yml` |

## Hinweis zu Coop

HeavyCaravans soll **nicht** von einer Coop-Mod abhängen. "Coop" ist ausschließlich
eine Testumgebung: die Mod muss sowohl im normalen Singleplayer als auch
zusammen mit TAOM und Bannerlord Online (als Coop-Referenz) funktionieren.
Es ist unklar, ob "Bannerlord Online" mit dem in der Planung genannten
"Joke's Bannerlord Coop" identisch ist - für die Kompatibilitätstests spielt
das keine Rolle, da keine feste Abhängigkeit entsteht.

`Modules/TAOM/SubModule.xml` referenziert zusätzlich optionale Ladereihenfolge-Hinweise
für `BannerlordTogether`, `BattleLinkMPClient` und `Coop` (`order="LoadAfterThis"`,
`optional="true"`) - TAOM selbst ist so gebaut, dass es ohne jede Coop-Mod startet.

## Load Order (Zielkonfiguration für HeavyCaravans-Tests)

```text
Native
SandBoxCore
Sandbox
CustomBattle
TAOM.Dependencies
TAOM
BannerlordOnline      (nur für Coop-Kompatibilitätstests, sonst deaktiviert)
HeavyCaravans          (immer zuletzt)
```

Quelle: `DependedModules` / `DependedModuleMetadatas` in `Modules/TAOM/SubModule.xml`
(`Native`, `SandBoxCore`, `Sandbox`, `CustomBattle` mit `order="LoadBeforeThis"`).

## .NET SDK Diskrepanz

Lokal ist .NET SDK 10.0.400 installiert, die CI pinnt 6.0.x. Das Modul-Projekt
zielt auf `net472` (bestätigt: `TaleWorlds.Core.dll` hat `ImageRuntimeVersion
v4.0.30319`, also klassisches .NET Framework), das ist von der SDK-Version
unabhängig (Multi-Targeting) und baut lokal fehlerfrei (Debug + Release).

## CI: bekannter offener Punkt

`HeavyCaravans.csproj` referenziert die TaleWorlds-DLLs per `HintPath` direkt
aus der lokalen Bannerlord-Installation (`Directory.Build.props`,
`BannerlordGameDir`). Auf dem GitHub-Actions-Runner ist Bannerlord nicht
installiert - `ci.yml` / `release.yml` schlagen deshalb aktuell fehl.
Bewusste Entscheidung (mit Max, 2026-09-08): erstmal so lassen, lokal in
VS2022 bauen. Später ggf. nachrüsten über BUTR
`Bannerlord.ReferenceAssemblies` (GitHub-Packages-Feed, benötigt Auth-Setup)
oder vendored Referenz-DLLs.

Harmony ist davon nicht betroffen: `HeavyCaravans.csproj` bezieht `Lib.Harmony`
2.3.6 ganz normal über NuGet (nuget.org, kein Auth nötig) statt über ein
Bannerlord-Modul.

## Test-Save

Im vorhandenen Savegame-Ordner
(`Documents/Mount and Blade II Bannerlord/Game Saves/`) existieren bereits u.a.
`test.sav` und `Native/save_001.sav`. Welcher Save als offizieller
HeavyCaravans-Test-Save dient (Inhalt/Modliste unbekannt, da nicht ohne
Spielstart prüfbar), muss manuell im Spiel bestätigt oder neu angelegt werden.
