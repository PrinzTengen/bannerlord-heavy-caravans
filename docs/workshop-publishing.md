# HeavyCaravans auf Steam Workshop veröffentlichen

Basiert auf der offiziellen TaleWorlds-Doku:
https://moddocs.bannerlord.com/steam-workshop/uploading_updating_mod/

Vorbereitete Dateien liegen in `publishing/` (`WorkshopCreate.xml` für den
ersten Upload, `WorkshopUpdate.xml` als Vorlage für spätere Updates).

## Voraussetzungen

- [ ] Steam läuft und ist eingeloggt
- [ ] Steam Cloud ist für Bannerlord aktiviert (Bannerlord in der Steam-Bibliothek
      → Rechtsklick → Eigenschaften → Allgemein)
- [x] Vorschaubild vorhanden: `publishing/preview.png` (1024x1024, ~715 KB,
      selbst designt als HTML/CSS + per Headless-Edge zu PNG gerendert, s.
      `publishing/preview.html` für die Quelle). `WorkshopCreate.xml` zeigt
      bereits darauf.

## Erster Upload

1. `publishing/WorkshopCreate.xml` öffnen (Notepad/Notepad++), Beschreibung/Tags
   bei Bedarf anpassen, Bildpfad setzen.
2. Im Ordner `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client`
   in die Adressleiste `cmd` eintippen, Enter.
3. Im Konsolenfenster:

   ```text
   TaleWorlds.MountAndBlade.SteamWorkshop.exe D:\bannerlord_mod_carawan\publishing\WorkshopCreate.xml
   ```

4. Keine Fehlermeldung = erfolgreich hochgeladen. Die Item-URL/ID findest du
   danach in deiner Steam-Bibliothek unter "Workshop" → "Deine Objekte", oder
   über die Steam-Benachrichtigung nach dem Upload.
5. Die Item-ID in `publishing/WorkshopUpdate.xml` eintragen (`ItemId Value="..."`)
   - die brauchst du für jedes künftige Update.

## Updates (nach dem ersten Upload)

1. `publishing/WorkshopUpdate.xml` öffnen, `ChangeNotes` auf das aktuelle
   Update anpassen.
2. Gleicher Befehl wie oben, nur mit `WorkshopUpdate.xml` statt `WorkshopCreate.xml`.

## Wichtig zum Testen

Solange `Modules/HeavyCaravans` im Spiel-Ordner existiert (aktuell per Junction
auf dieses Repo verlinkt, s. `phases/phase-01-devlog.md`), lädt das Spiel
**immer diese lokale Version**, nie die Workshop-Version - auch nicht nach
einem Abo. Um wirklich die hochgeladene Workshop-Version zu testen, müsste die
Junction/der `Modules/HeavyCaravans`-Ordner vorübergehend entfernt werden
(offizieller Hinweis aus der TaleWorlds-Doku, nicht Bannerlord-Coop-spezifisch).
Für den normalen Entwicklungsalltag ist das nicht nötig.

## Sichtbarkeit

Aktuell `FriendsOnly` in `WorkshopCreate.xml` (mit Max abgestimmt: Mod ist noch
in aktivem Test, ein Kollege soll testen können). Auf `Public` umstellen,
sobald Phase 16-22 durch sind - entweder direkt in der XML vor dem nächsten
Update-Upload, oder jederzeit über die Steam-Workshop-Seite des Items selbst.
