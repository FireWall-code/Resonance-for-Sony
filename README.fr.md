<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/images/title-dark.png">
    <source media="(prefers-color-scheme: light)" srcset="docs/images/title-light.png">
    <img width="55%" alt="Resonance" src="docs/images/title-light.png">
  </picture>
</p>

<p align="center">
  <a href="../../releases/latest"><img alt="Dernière version" src="https://img.shields.io/github/v/release/FireWall-code/Resonance-for-Sound-Connect?color=7c3aed"></a>
  <img alt="Windows 10 et 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4">
  <img alt="Anglais et français" src="https://img.shields.io/badge/langues-EN%20%7C%20FR-06b6d4">
  <a href="LICENSE"><img alt="Licence" src="https://img.shields.io/badge/licence-PolyForm_Noncommercial-f59e0b"></a>
</p>

<p align="center">
  <a href="README.md">English</a> | <strong>Français</strong>
</p>

---

**Pilotez vos écouteurs et casques Sony depuis Windows, sans téléphone.** Changez la réduction de bruit, réglez
l'égaliseur, surveillez la batterie et personnalisez les commandes tactiles depuis une appli Fluent native, un
raccourci clavier ou la ligne de commande.

Vous cherchez **Sony Sound Connect (anciennement Headphones Connect) sur PC** ? Sony ne le propose que sur
téléphone. Resonance apporte ses réglages à Windows, sans émulateur. [Voir le site](https://firewall-code.github.io/Resonance-for-Sound-Connect/fr/).

<p align="center">
  <a href="../../releases/latest"><img alt="Télécharger pour Windows" src="https://img.shields.io/badge/T%C3%A9l%C3%A9charger_pour_Windows-7c3aed?style=for-the-badge"></a>
</p>

<img alt="Resonance en thème clair et sombre, avec la carte de connexion" src="docs/images/hero.png">

## Fonctionnalités ✨

- 🎧 **Contrôle du bruit** : réduction de bruit, son ambiant avec niveau et voix, désactivé
- 🎚️ **Égaliseur** : préréglages et courbes personnalisées, avec les bandes de votre modèle
- 🔋 **Batterie en un coup d'œil** : chaque écouteur, le boîtier ou le casque, et une carte animée à la connexion
- ⌨️ **Raccourcis clavier globaux** pour les modes de bruit, dans toutes les applis
- 👆 **Commandes tactiles** : préréglages, gestes, accès rapide et gestes de la tête
- 📱 **Appareils multipoint** : connecter, déconnecter et basculer entre vos appareils, ou épingler la lecture sur l'appareil actuel
- ✨ **Audio** : DSEE Extreme, priorité de connexion, musique de fond, amplification de la voix
- 📊 **Niveau sonore en direct**, mesuré par le casque
- 🖥️ **Pensé pour Windows** : réglages rapides depuis l'icône près de l'horloge, qui indique le mode en cours, thèmes clair et sombre, démarrage avec Windows, ligne de commande

## En images 🖼️

<table>
  <tr>
    <td align="center"><b>Égaliseur</b></td>
    <td align="center"><b>Commandes tactiles</b></td>
    <td align="center"><b>Appareils</b></td>
  </tr>
  <tr>
    <td><picture><source media="(prefers-color-scheme: dark)" srcset="docs/images/equalizer.png"><img alt="Égaliseur" src="docs/images/light/equalizer.png"></picture></td>
    <td><picture><source media="(prefers-color-scheme: dark)" srcset="docs/images/touch.png"><img alt="Commandes tactiles" src="docs/images/light/touch.png"></picture></td>
    <td><picture><source media="(prefers-color-scheme: dark)" srcset="docs/images/devices.png"><img alt="Appareils" src="docs/images/light/devices.png"></picture></td>
  </tr>
  <tr>
    <td align="center"><b>Qualité audio</b></td>
    <td align="center"><b>Paramètres</b></td>
    <td align="center"><b>Raccourcis clavier</b></td>
  </tr>
  <tr>
    <td><picture><source media="(prefers-color-scheme: dark)" srcset="docs/images/quality.png"><img alt="Qualité audio" src="docs/images/light/quality.png"></picture></td>
    <td><picture><source media="(prefers-color-scheme: dark)" srcset="docs/images/settings.png"><img alt="Paramètres" src="docs/images/light/settings.png"></picture></td>
    <td><picture><source media="(prefers-color-scheme: dark)" srcset="docs/images/shortcuts.png"><img alt="Raccourcis clavier" src="docs/images/light/shortcuts.png"></picture></td>
  </tr>
</table>

*Les captures sont en anglais ; l'appli s'affiche en français si Windows l'est.*

## Casques pris en charge 🎧

Resonance demande à votre casque ce qu'il gère et n'affiche que ces réglages.

| | Modèles | État |
|---|---|---|
| ✅ | WF-1000XM6 | Chaque réglage testé sur le casque |
| 🧪 | WF-1000XM5, LinkBuds Clip, WH-1000XM3 | Vérifiés sur des sessions enregistrées ; modification des réglages pas encore confirmée par un propriétaire |
| 🔶 | WH-1000XM4/XM5/XM6, WF-1000XM4, WF-C700N, autres LinkBuds… | Devraient fonctionner : mêmes protocoles. [Aidez à confirmer le vôtre](CONTRIBUTING.md#add-support-for-your-headphones) |

## Installation 📥

1. Appairez le casque avec Windows (**Paramètres → Bluetooth et appareils**) et connectez-le.
2. Téléchargez `Resonance-win-x64.zip` depuis la [dernière version](../../releases/latest) et décompressez-le où vous voulez.
3. Lancez `Resonance.exe` : il trouve le casque tout seul.

Rien d'autre à installer. L'appli n'est pas encore signée : au premier lancement, SmartScreen peut demander une
confirmation (**Informations complémentaires → Exécuter quand même**).

Fermer la fenêtre garde Resonance près de l'horloge : un clic sur l'icône ouvre les réglages rapides (mode, égaliseur, épinglage de la
lecture), un clic droit ouvre le menu. L'icône indique le mode en cours : bleue pour la réduction de bruit, verte pour
le son ambiant, blanche pour désactivé, et grise quand le casque est déconnecté.

## Bon à savoir 💡

<details>
<summary><b>Utiliser aussi Sound Connect sur le téléphone</b></summary>

<br>

Le casque n'accepte qu'une appli de contrôle à la fois. Resonance le lâche après quelques minutes sans utilisation
(**Paramètres → Partage avec le téléphone**) et le reprend dès que vous ouvrez la fenêtre, pressez un raccourci ou
lancez une commande. **Laisser la main au téléphone** le libère tout de suite, jusqu'à ce que vous choisissiez
**Reprendre la main**.

</details>

<details>
<summary><b>Raccourcis clavier</b></summary>

<br>

Dans **Raccourcis clavier**, cliquez sur une case et pressez une combinaison (Ctrl, Alt, Maj ou Win + une touche ;
F1-F24 et les touches multimédia fonctionnent seules). Actions disponibles : réduction de bruit, son ambiant,
désactivé et bascule.

</details>

<details>
<summary><b>Ligne de commande</b></summary>

<br>

```text
resonance-cli etat                 batterie, mode, codec, morceau en cours
resonance-cli nc                   réduction de bruit
resonance-cli ambiant 12           son ambiant, niveau 12
resonance-cli desactive | bascule
resonance-cli eq puissant          préréglage d'égaliseur (eq : liste)
resonance-cli volume 12 | lecture | pause | suivant | precedent
resonance-cli appareils | connecter telephone
resonance-cli liberer | reprendre  laisse le casque au téléphone / le reprend
resonance-cli aide
```

Codes de sortie : `0` succès, `1` erreur de commande, `2` pas de casque, `3` connexion impossible. Les noms anglais
fonctionnent aussi.

</details>

<details>
<summary><b>Votre image et votre animation</b></summary>

<br>

**Paramètres → Apparence** permet de remplacer le dessin du casque par l'image de votre choix, et de jouer un
dossier d'images PNG numérotées à la connexion. Ces fichiers restent sur votre PC.

</details>

<details>
<summary><b>Pour les développeurs</b></summary>

<br>

Il faut le SDK .NET 10 sur Windows 10 (1809) ou plus récent.

```powershell
dotnet run --project src/Resonance.App        # lance l'appli
./publish.ps1                                 # exécutables autonomes + raccourci du menu Démarrer
```

La structure du code, la [spécification du protocole](docs/PROTOCOL.md) et le guide de contribution sont en anglais.

</details>

## Questions fréquentes ❓

**Existe-t-il une appli Sony Sound Connect pour PC ?**
Sony publie Sound Connect (anciennement Headphones Connect) uniquement pour Android et iPhone. Resonance est une appli
Windows indépendante qui apporte ses principaux réglages sur votre PC.

**Faut-il BlueStacks ou un autre émulateur Android ?**
Non. Resonance est une appli Windows native qui parle au casque en Bluetooth. Il ne faut ni émulateur, ni téléphone,
ni compte Sony.

**Est-ce que ça marche avec les WH-1000XM5, WH-1000XM4 ou WF-1000XM5 ?**
Les WF-1000XM6 sont testés à fond, les WF-1000XM5 ont été contrôlés sur des sessions enregistrées, et les WH-1000XM4
et WH-1000XM5 utilisent les mêmes protocoles sans être encore confirmés. Voir [Casques pris en charge](#casques-pris-en-charge-).

**Est-ce gratuit ?**
Oui, pour un usage personnel et tout autre usage non commercial. Le code source est public ; voir [Licence](#licence-).

**Peut-il mettre à jour le firmware ?**
Non. Les fichiers de firmware viennent des serveurs de Sony : continuez à utiliser l'appli du téléphone pour les mises à jour.

## Contribuer 🤝

Traductions, prise en charge d'autres modèles et signalements de bugs sont les bienvenus : voir
[CONTRIBUTING.md](CONTRIBUTING.md).

Les formats des autres modèles ont été vérifiés sur les sessions enregistrées et les notes de
[mos9527/SonyHeadphonesClient](https://github.com/mos9527/SonyHeadphonesClient) (MIT). Merci !

## Avertissement ⚠️

Resonance est un projet indépendant, sans lien avec Sony. Sony, WF-1000XM6, LDAC, DSEE Extreme, Sound Connect et Headphones Connect sont
des marques de Sony Group Corporation. Le protocole a été documenté à des fins d'interopérabilité ; aucun code ni
élément graphique de Sony n'est inclus. Utilisation à vos risques.

## Licence 📜

Gratuit pour un usage personnel et tout autre usage non commercial, sous [licence PolyForm Noncommercial 1.0.0](LICENSE).
L'usage commercial, y compris la vente de Resonance ou d'une copie modifiée, n'est pas couvert et demande l'accord de l'auteur.

Les versions jusqu'à la 1.1.0 ont été publiées sous GPL-3.0 et restent disponibles sous cette licence.
