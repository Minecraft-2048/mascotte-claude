# Mascotte Claude

![La mascotte en action](sources/apercu.gif)

Une petite mascotte de bureau pour Claude : elle vit en bas à droite de l'écran, respire, cligne des yeux, se balade, et réagit à ce que fait Claude Code. L'idée vient de la mascotte animée de Codex : Claude méritait la sienne.

*A small desktop companion for Claude, inspired by the Codex pets: it sits in the corner of the screen, idles, walks around, and reflects what Claude Code is doing (working, waiting for you, done, failed). Windows, single exe, no install.*

> **Projet de fan, non officiel.** Il n'est ni créé ni approuvé par Anthropic. « Claude » est une marque d'Anthropic.

## Ce qu'elle fait

- **Elle vit sa vie** : respiration, clins d'œil, petits saluts, sauts et balades le long du bas de l'écran.
- **Survol** : une pilule apparaît sous elle, avec un bouton *nouvelle conversation avec Claude* et un bouton *menu*.
- **Clic** : elle salue (et saute une fois sur trois). **Glisser** : elle court dans le sens où on la tire et garde sa nouvelle place.
- **Clic droit** : animations, taille (petite à très grande), balade, premier plan, lancement au démarrage de Windows, quitter.
- **Elle suit Claude Code** : on peut lui envoyer un état, elle change d'animation et le dit dans une bulle.

| État envoyé | Ce qu'elle fait | Bulle par défaut |
| --- | --- | --- |
| `running` | tape sur son ordinateur, en boucle | Je m'en occupe… |
| `waiting` | lève la main, en boucle | J'ai besoin de toi ! |
| `review` | vérifie puis salue | C'est prêt ! |
| `failed` | s'affaisse, les yeux en croix | Aïe, ça a coincé… |
| `idle` | retour au repos | |
| `waving`, `jumping`, `walking` | un salut, un saut, une balade | |

```
MascotteClaude.exe --etat running
MascotteClaude.exe --etat waiting "Je peux lancer les tests ?"
```

Le texte après l'état remplace la bulle par défaut.

## Installer

Télécharger un exe dans les [Releases](../../releases) et le lancer. Windows 10 ou 11, rien d'autre à installer.

| Exe | Mascotte |
| --- | --- |
| `MascotteClaude.exe` | Claude : suit ce que fait Claude Code |
| `MascotteChat.exe` | un chaton : se promène, joue, miaule quand on clique, fait des siestes |

Chaque mascotte a son menu au clic droit (taille, animations, démarrage avec Windows, quitter).

Le stickman, qui vivait ici, a maintenant son propre dépôt : [mascotte-stickman](https://github.com/Minecraft-2048/mascotte-stickman).

## La brancher sur Claude Code

Avec ces *hooks* dans `~/.claude/settings.json`, la mascotte suit toute seule ce que fait Claude Code (adapter le chemin de l'exe) :

| Moment | État envoyé |
| --- | --- |
| tu envoies un message, ou un outil vient de finir | `running` |
| Claude te pose une question ou demande une autorisation | `waiting` |
| Claude a fini de répondre | `review` |
| le tour s'arrête sur une erreur | `failed` |

```json
{
  "hooks": {
    "UserPromptSubmit": [
      { "hooks": [{ "type": "command", "command": "C:/chemin/vers/MascotteClaude.exe", "args": ["--etat", "running"], "async": true, "timeout": 10 }] }
    ],
    "PostToolUse": [
      { "hooks": [{ "type": "command", "command": "C:/chemin/vers/MascotteClaude.exe", "args": ["--etat", "running"], "async": true, "timeout": 10 }] }
    ],
    "PreToolUse": [
      { "matcher": "AskUserQuestion", "hooks": [{ "type": "command", "command": "C:/chemin/vers/MascotteClaude.exe", "args": ["--etat", "waiting"], "async": true, "timeout": 10 }] }
    ],
    "PermissionRequest": [
      { "hooks": [{ "type": "command", "command": "C:/chemin/vers/MascotteClaude.exe", "args": ["--etat", "waiting"], "async": true, "timeout": 10 }] }
    ],
    "Notification": [
      { "matcher": "permission_prompt|elicitation_dialog", "hooks": [{ "type": "command", "command": "C:/chemin/vers/MascotteClaude.exe", "args": ["--etat", "waiting"], "async": true, "timeout": 10 }] }
    ],
    "Stop": [
      { "hooks": [{ "type": "command", "command": "C:/chemin/vers/MascotteClaude.exe", "args": ["--etat", "review"], "async": true, "timeout": 10 }] }
    ],
    "StopFailure": [
      { "hooks": [{ "type": "command", "command": "C:/chemin/vers/MascotteClaude.exe", "args": ["--etat", "failed"], "async": true, "timeout": 10 }] }
    ]
  }
}
```

La forme `command` + `args` lance l'exe directement, sans passer par un shell : un chemin avec des espaces ne pose aucun problème. `async` évite de faire attendre Claude. Un état `running` répété ne relance ni la bulle ni l'animation.

## Changer son apparence

Un fichier `mascotte.png` posé à côté de l'exe remplace les images intégrées. C'est un atlas de 8 colonnes × 9 lignes, en cases de 192 × 208 pixels, fond transparent : la même disposition que les pets de Codex. Une dixième ligne, facultative, donne la pose en l'air (tournée à droite, puis à gauche).

| Ligne | Animation | Images |
| ---: | --- | ---: |
| 0 | repos (respiration ×4, clin d'œil, repos) | 6 |
| 1 | marche vers la droite | 8 |
| 2 | marche vers la gauche | 8 |
| 3 | salut | 4 |
| 4 | saut | 5 |
| 5 | raté | 8 |
| 6 | attente | 6 |
| 7 | travail | 6 |
| 8 | vérification | 6 |

## Compiler

```
powershell -ExecutionPolicy Bypass -File construire.ps1
```

Le script utilise le compilateur C# livré avec Windows (.NET Framework 4). Python avec Pillow et numpy ne sert qu'à reconstruire l'atlas.

- `src/Mascotte.cs` : l'application (WPF, fenêtre transparente toujours visible). Le même code sert à toutes les mascottes.
- `personnages/<nom>/` : une mascotte par dossier. `perso.txt` est sa fiche (nom, phrase d'accueil, ce que font le clic et le bouton, `plateformes=1` pour sauter sur le haut des fenêtres, `sieste=1` pour dormir, `appli=` pour ouvrir une appli installée plutôt qu'un lien), `planche.png` sa planche de 12 poses sur fond vert. Un personnage peut avoir plusieurs formes (`atlas2.png`, `atlas3.png`) et des objets (`objets.png`) pour l'animation du bloc « ? ». Une seconde planche (`planche2.png` : bisous, cœurs, danse, chant) ajoute quatre lignes à l'atlas et des cœurs et notes qui s'envolent (`particules.png`) ; avec `musique=1`, la mascotte écoute le niveau de la sortie audio de Windows et danse en rythme quand de la musique joue. Avec `jeu=1`, la mascotte accepte l'état `jeu` (`--etat jeu "grand course droite 12"` : forme `petit|grand|feu`, action `repos|marche|course|saut|mort|victoire|perdu|pause|fin`, sens `droite|gauche`, pièces) : envoyé par un jeu, il lui fait recopier le joueur tant que la partie dure (taille, marche, sauts, pièces, défaite, victoire) ; sur `fin`, ou après 12 s sans nouvelles, elle reprend sa vie en gardant sa taille. Sur `mort`, elle joue l'animation de défaite du jeu (petite, bras levés, un saut, puis elle tombe hors de l'écran) et revient quand le joueur repart. Une page web ne pouvant pas écrire `etat.txt`, la mascotte écoute aussi sur le PC seulement (`http://127.0.0.1:47835`) : `GET /ping` et `POST /jeu` avec la même ligne en texte ; seules les pages de ce PC sont acceptées. Le script de compilation produit un `Mascotte<Nom>.exe` par dossier (dans le dossier donné par `sortie=` s'il y en a un) ; `construire.ps1 <nom>` ne recompile que celui-là.
- `outils/construire_atlas.py` : découpe la planche de poses, retire le fond vert, fabrique les images de respiration, de saut et de marche, puis assemble l'atlas et l'icône. Une dixième ligne de l'atlas donne la pose en l'air (droite, gauche), utilisée pour les sauts sur les fenêtres.
- `sources/planche-1.png` : la planche de 12 poses, générée par IA (ChatGPT) à partir d'une description du personnage.
- `assets/` : l'atlas et l'icône produits par le script.

## Licence

MIT, voir [LICENSE](LICENSE).
