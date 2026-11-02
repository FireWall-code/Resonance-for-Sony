# Resonance

**Pilotez vos écouteurs Sony depuis Windows 11, sans téléphone.**

Resonance est une application Windows native (C# / .NET 10, design Fluent) qui parle aux casques Sony
en Bluetooth, comme l'application mobile Sony Sound Connect. Réduction de bruit, égaliseur, batterie,
commandes tactiles : tout se règle depuis le PC, un raccourci clavier ou la ligne de commande.

Vous cherchez **Sony Sound Connect (anciennement Headphones Connect) sur PC** ? Sony ne propose l'application
que sur téléphone. Resonance apporte ses réglages à Windows 10 et 11, sans émulateur ni téléphone.

![Fenêtre principale de Resonance](docs/images/sound.png)

*[English version](README.md)*

## Fonctionnalités

- **Contrôle du bruit** : réduction de bruit, son ambiant (niveau 1 à 20, voix, mode automatique), désactivé
- **Égaliseur** avec préréglages et courbe personnalisée : 10 bandes, ou Clear Bass + 5 bandes, selon le modèle
- **Batterie** de chaque écouteur et du boîtier (ou du casque), codec (LDAC / AAC / SBC), morceau en cours, volume
- **Niveau sonore** : le niveau mesuré en direct par le casque pendant la lecture (écoute sécurisée)
- **Audio** : DSEE Extreme, priorité de connexion, musique de fond, amplification de la voix, réduction des fuites sonores
- **Détection de conversation** avec sensibilité et délai
- **Commandes tactiles** : preset par écouteur, gestes, accès rapide, gestes de la tête
- **Appareils** : voir, connecter, déconnecter, oublier et basculer entre les appareils (multipoint)
- **Paramètres** : extinction auto, recharge douce, retour de voix, pause au retrait, guidage vocal, assistant vocal,
  test des embouts, et tout réglage propre au modèle que le casque décrit lui-même (sensibilité du toucher…)
- **Bonus Windows** : icône dans la zone de notification (un clic change le mode), raccourcis clavier globaux,
  démarrage avec Windows, ligne de commande scriptable, thème clair / sombre, anglais et français
- **Votre casque à l'écran** : un dessin de votre modèle à sa couleur (ou votre propre photo), et une carte
  animée avec les niveaux de batterie au-dessus de la zone de notification quand il se connecte

## Casques pris en charge

Resonance demande au casque quelles fonctions il gère et n'affiche que celles-là, dans le format propre à
chaque modèle (variantes de réduction de bruit, d'égaliseur, de batterie et de mode d'écoute).

| Modèle | État |
|---|---|
| WF-1000XM6 | Entièrement testé (chaque réglage vérifié sur le casque) |
| WF-1000XM5, LinkBuds Clip | Toutes les pages vérifiées sur des sessions enregistrées ; modification des réglages pas encore confirmée par un propriétaire |
| WH-1000XM3 | Première version du protocole : vérifié sur une session enregistrée (batterie, réduction de bruit, égaliseur, DSEE, connexion, extinction auto, bouton NC/AMBIENT, panneau tactile, volume) |
| Autres modèles Sound Connect et Headphones Connect (WH-1000XM4/XM5/XM6, WF-1000XM4, WF-C700N, LinkBuds…) | Devraient fonctionner : mêmes protocoles et variantes. [Aidez à confirmer le vôtre](CONTRIBUTING.md#add-support-for-your-headphones) |

## Installation

1. Appairez le casque avec Windows (Paramètres → Bluetooth et appareils) et connectez-le.
2. Téléchargez `Resonance-win-x64.zip` depuis la [dernière version](../../releases/latest) et décompressez-le.
3. Lancez `Resonance.exe` : il trouve le casque tout seul.

Rien à installer, pas de runtime .NET nécessaire. Les exécutables ne sont pas encore signés : au premier
lancement, SmartScreen peut demander une confirmation (« Informations complémentaires » → « Exécuter quand même »).

La langue suit celle de Windows ; elle se change dans Paramètres → Langue.

## Utiliser aussi Sound Connect sur le téléphone

Le casque n'accepte qu'une appli de contrôle à la fois. Resonance le lâche après quelques minutes sans utilisation
(Paramètres → Partage avec le téléphone) et le reprend dès que vous ouvrez la fenêtre, pressez un raccourci ou lancez
une commande. **Laisser la main au téléphone**, dans Paramètres ou le menu de l'icône, le libère tout de suite, jusqu'à
ce que vous choisissiez **Reprendre la main**. En ligne de commande : `resonance-cli liberer` et `resonance-cli reprendre`.

## Votre image et votre animation

Paramètres → Apparence permet de remplacer le dessin du casque par l'image de votre choix, et de jouer un dossier
d'images PNG numérotées à la connexion. Ces fichiers restent sur votre PC.

## Ligne de commande

```text
resonance-cli etat                 batterie, mode, codec, morceau en cours
resonance-cli nc                   réduction de bruit
resonance-cli ambiant 12           son ambiant, niveau 12
resonance-cli desactive | bascule
resonance-cli eq puissant          préréglage d'égaliseur (eq : liste)
resonance-cli volume 12 | lecture | pause | suivant | precedent
resonance-cli appareils | connecter telephone
resonance-cli aide
```

Les noms anglais (`status`, `ambient`, `toggle`…) fonctionnent aussi.

Le reste de la documentation (fonctionnement, compilation, contribution) est en anglais dans le
[README principal](README.md).

## Remerciements

Les variantes des autres modèles ont été vérifiées sur les sessions enregistrées et les notes de
[mos9527/SonyHeadphonesClient](https://github.com/mos9527/SonyHeadphonesClient) (MIT), le travail open source
le plus complet sur ces casques.

## Avertissement

Resonance est un projet indépendant, sans lien avec Sony. Sony, WF-1000XM6, LDAC, DSEE Extreme et
Sound Connect sont des marques de Sony Group Corporation. Le protocole a été documenté à des fins
d'interopérabilité ; aucun code ni élément graphique de Sony n'est inclus.

## Licence

[GPL-3.0](LICENSE)
