# Sony "MDR v2" headphones protocol (WF-1000XM6)

Reconstructed for interoperability from **Sound Connect 13.3.0** (`com.sony.songpal.mdr`,
version code 130305109) and verified on a **WF-1000XM6, firmware 2.0.5**, from Windows 11.
No Sony code is reproduced here: this document only describes bytes on the wire.

Evidence levels used below:
**[App]** read in the Android app · **[Device]** answer observed from the headphones ·
**[Tested]** command sent from Windows, confirmed by a notification, then restored.

## 1. Transport

| | |
|---|---|
| Link | Bluetooth Classic, RFCOMM |
| MDR v2 service | `956C7B26-D49A-4BA8-B03F-B17D393CB6E2` [Device] |
| MDR v1 service (older models) | `96CC203E-5068-46AD-B32D-E316F5E069BA` [App] |

Windows exposes the service directly: no driver and no phone are needed. The headphones accept
a control connection from the PC while the phone keeps its own (multipoint). Only **one** control
connection per host is possible at a time.

## 2. Framing

```
3E | type | seq | length (u32, big-endian) | payload | checksum | 3C
```

- `checksum` = 8-bit sum of `type`, `seq`, the length bytes and the payload.
- Escaping: inside the body, `3C`, `3D` and `3E` are sent as `3D, (b & 0xEF)`.
- `type` [App]: `01` ACK, `0C` command table 1, `0D` common, `0E` command table 2.
- Every data frame is acknowledged by an `ACK` frame with `seq' = 1 − seq`. The sender alternates
  `seq` between 0 and 1 and keeps a single frame in flight [Tested].

## 3. Commands

Byte 0 is the command, byte 1 the "inquired type" (which variant of a feature).
Every group follows the same pattern [App]:

| +0 | +1 | +2 | +3 | +4 | +5 | +6 | +7 | +8 | +9 | +A | +B | +C | +D |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| GET_CAP | RET_CAP | GET_STATUS | RET_STATUS | SET_STATUS | NTFY_STATUS | GET_PARAM | RET_PARAM | SET_PARAM | NTFY_PARAM | GET_EXT | RET_EXT | SET_EXT | NTFY_EXT |

Table 1 groups: `00` connect, `10` common, `20` power, `30` firmware update, `40` LE Audio, `50` EQ,
`60` noise cancelling / ambient sound, `70` sense, `80` optimizer, `90` alerts, `A0` playback,
`B0` SAR / auto play, `C0` logs, `D0` general settings, `E0` audio, `F0` system.
Table 2 groups: `20` power, `30` peripherals, `40` voice guidance, `50` safe listening, `60` LE Audio,
`70` party, `F0` system.

A RET answer and its NTFY notification share the same layout. A SET almost always mirrors the RET
layout with the command byte + 1.

At connection, `06 00` (table 1 and table 2) returns the list of functions the model supports:
this is how an app adapts its UI to each model.

**Beware of the on/off convention**: `OnOffSettingValue` uses `00` = ON and `01` = OFF, while
`NcAsmOnOffValue` (inside noise control frames) uses `00` = OFF and `01` = ON [App].

## 4. WF-1000XM6 features

| Feature | Read | Write | Layout | Evidence |
|---|---|---|---|---|
| Model / firmware | `04 01`, `04 02` | — | `05 <t> <len> <utf8>` | Device |
| MAC address | `02 00` | — | `03 00 <ver> <len> "AA:BB:…"` | Device |
| Codec | `12 02` | — | `13 02 <codec>` (`10` LDAC, `02` AAC, `01` SBC, `30` LC3) | Device |
| Battery L/R | `22 01` | — | `23 01 <L%> <charging> <R%> <charging>` | Device |
| Case battery | `22 02` | — | `23 02 <%> <charging>` | Device |
| Noise cancelling / ambient | `66 19` | `68 19` | `<chg=01> <on 01/off 00> <mode 00 NC/01 ambient> <voice 0/1> <level 0-20> <auto 0/1> <sensitivity std/high/low>` | Tested |
| Equalizer | `56 04` | `58 04` | `<preset> <n=0A> <n steps 0-12>` (6 = 0 dB) | Tested |
| EQ band frequencies | `5A 04` | — | `5B 04 0A (01 <u16 Hz>)×10` → 31…16000 Hz | Device |
| DSEE Extreme | `E6 01` | `E8 01` | `<00 off / 01 auto>` | Tested |
| Connection priority | `E6 02` | `E8 02` | `<00 sound quality / 01 stable connection>` | Tested (renegotiates the codec) |
| Background music (BGM) | `E6 09` | `E8 09` | `<on/off> <room 0-2>` | Tested |
| Speak-to-chat | `F6 0C` | `F8 0C` | `<on/off> <on/off>` | Tested |
| … sensitivity / timeout | `FA 0C` | `FC 0C` | `<auto/high/low> <short/std/long/never>` | Tested |
| Touch presets | `F6 03` | `F8 03` | `02 <left preset> <right preset>` | Tested |
| Gestures per preset | `FA 03` | `FC 03` | `<n> (<preset> <k> (<gesture> <function>)×k)×n` | Tested |
| Touch capabilities | `F0 03` | — | see `ParseTouchCapability` in the code | Device |
| Head gestures | `F6 0F` | `F8 0F` | `<on/off>` | Tested |
| Auto power off | `26 05` | `28 05` | `<value> 00` (`10` when removed, `11` never) | Tested |
| Multipoint | `D6 D2` | `D8 D2` | `00 <on/off>` | App + Device |
| Sidetone | `D6 D1` | `D8 D1` | `00 <on/off>` | Tested |
| One-earbud noise control | `D6 D4` | `D8 D4` | `00 <on/off>` | Tested |
| Battery care (80 %) | T2 `26 01` | T2 `28 01` | `<on/off>` | Tested |
| Voice guidance | T2 `46 01` | T2 `48 01` | `<on/off>` | Tested |
| Voice guidance volume | T2 `46 20` | T2 `48 20` | `<signed volume -2…+2> 01` | Tested |
| Volume | `A6 20` | `A8 20` | `<0-30>` | Tested |
| Playback | `A6 01` | `A4 01` | `00 <pause 01 / next 02 / previous 03 / play 07>` | App |
| Now playing | `A6 01` | — | `(<state> <len> <utf8>)×3 <status>` title / album / artist | Device |
| Quick access 1 / 2 | `F6 0D` | `F8 0D` | `02 <QA1> <QA2>` (`00` none, `01` Spotify Tap) | Tested |
| Voice assistant | `F6 04` (choices: `F0 04`) | `F8 04` | `<30 phone / 31 Google / 32 Alexa / 34 Sony / FF none>` | Tested (30) |
| Remembered devices | T2 `36 02` | — | `<n> (<MAC 17 B> <state> <CoD×3> <len> <name>)×n` | Device |
| Connect / disconnect / forget | — | T2 `3C 02` | `<01 / 00 / 02> <ASCII MAC 17 B>` → `3D 02 <action> <result> <MAC>` | Device |
| Switch audio source | — | T2 `3C 01` | `<ASCII MAC 17 B>` → `3D 01 <result> <MAC>` | App |
| Pairing mode | — | T2 `34 02` | `01 00` | App |
| Earbud fit test | — | `F4 06 <01 enter/00 leave> 00`, then `F8 06 <00 start/01 cancel> 00 FF FF` | progress `F9 06 <state> <error> …`, result `FD 06 <L 00=good> <R> …` | Tested |
| Power off | — | `24 03 01` | | App |

## 4b. Other models: support functions and variants

At connection, `CONNECT_GET_SUPPORT_FUNCTION` (`06 00` on table 1, and on table 2) returns
`07 00 <n> (<function> <priority>)×n`. Each function type names the variant a model implements, and
the variant decides the inquired type and the payload layout. Resonance only queries declared functions;
headphones that do not answer get the WF-1000XM6 command set.

| Function (table 1) | Inquired type | `RET_PARAM` payload after the inquired type |
|---|---|---|
| `6D` NC/ASM + noise adaptation (WF-1000XM6) | NC/ASM `19` | `<chg> <on> <mode> <voice> <level> <adaptive> <sensitivity>` |
| `6B` / `6C` NC/ASM dual (WF-1000XM5) | `17` / `18` | `<chg> <on> <mode> <voice> <level>` |
| `68` / `6A` NC/ASM auto / dual-single | `15` / `16` | `<chg> <on> <mode> <nc value> <voice> <level>` |
| `64` NC on/off + ambient level | `13` | `<chg> <on> <nc on> <voice> <level>` |
| `65` NC mode switch + ambient level | `14` | `<chg> <on> <nc value> <voice> <level>` |
| `67` ambient sound only (LinkBuds) | `22` | `<chg> <on> <voice> <level>` |
| `20` / `28` single battery (over-ear) | power `00` / `08` | `<level> <charging>` |
| `50` / `52` / `57` preset EQ | EQ `00` / `02` / `04` | `<preset> <n> <steps>×n`; 6 bands = Clear Bass + 5 bands, steps 0-20 |
| `E1` / `E3` connection mode | audio `00` / `02` | `<priority>`; the XM6 answers `02` while only declaring `E7` |
| `E4` / `EB` background music | audio `03` / `09` | `<on/off> <room size>` |
| `E8` voice boost, `E9` sound leakage reduction | audio `06`, `07` | `<on/off>` |
| `F1` pause when removed | system `01` | `<on/off>` |
| `D1`-`D4` general settings | general setting `D1`-`D4` | see below |

**General settings describe themselves.** `GET_CAPABILITY` returns
`D1 <slot> <type 00 switch / 01 list> <format> <len> <name> <len> <summary>`, followed for a list by
`<n> (<format> <len> <name> <len> <summary>)×n`. Names are identifiers such as `SIDETONE_SETTING`,
`MULTIPOINT_SETTING`, `TWS_ONE_SIDE_USE_NCASM_SETTING` or `TAP_SENSITIVITY_SETTING`; list choices are
`<name>_ELEM_LOW`, `<name>_ELEM_HIGH`… `RET_PARAM` is `D7 <slot> <type> <value>` (a switch uses
`00` on / `01` off, a list the choice index), and `SET_PARAM` is `D8 <slot> <type> <value>`.

**Sound level (table 2).** Functions `50`-`53` (safe listening for over-ear / earbuds) map to inquired
type `00`-`03`. `5A <type>` returns `5B <type> <level dB, FF none> <reason 00 not playing / 01 call /
02 not worn>`. The headphones only answer on request.

## 5. Observed behaviour

- **Noise cancelling mode**: the headphones ignore the ambient level, voice focus and auto ambient
  fields (they keep the previous values). Those fields only apply in ambient mode.
- **Turning noise control off and on**: while off, the headphones report mode `00`; when turning it
  back on, send the wanted mode rather than the notified one.
- **Cinema mode** (`E8 04`): accepted without effect or notification; the XM6 does not declare
  function `E5`. Its listening mode is limited to Standard / background music (`E8 09`).
- **Sony voice assistant (`34`)**: refused until it has been set up from the phone.
- **Device results** (`PeripheralResult`): low nibble `0` success, `1` error, `2` in progress, `3` busy;
  high nibble `0` disconnect, `1` connect, `2` unpair.
- **Equalizer**: editing bands requires preset `A0` (Custom) or a user preset. Selecting a preset only
  needs `58 04 <preset> 00`.
- **Connection priority**: the headphones renegotiate A2DP (`15 02 00`, then `15 02 10`) and may drop
  the RFCOMM link. Clients must reconnect and read the state again.
- The headphones push `A9 01` (track), `A9 20` (volume), `15 03` (DSEE active) and `C9 …`
  (action log) on their own.

## 6. Not implemented

| Feature | Reason |
|---|---|
| Safe listening dose (T2 `50`) | The live level is shown (see 4b). The `55 xx` logs are decoded (per earbud, u32 timestamp, level, u32 total), but the conversion into a "% of the WHO weekly dose" is computed by the phone app and was not recovered. |
| LE Audio (`E8 05 <priority> <LE>`) | Possible, but switching drops the Classic link used for control, and Windows needs LE Audio hardware. |
| Adaptive sound control (`70`) | The phone detects activity / location and pushes settings. |
| 360 audio / head tracking / SAR (`B0`, `B6`, `B7`) | Sensor data for the phone's spatial renderer and apps. |
| Voice guidance language | Needs a voice package downloaded from Sony's servers and transferred (`30`). |
| Firmware update (`30`) | Files come from Sony's servers. |
