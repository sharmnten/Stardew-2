# Single-player parity ledger

Reference: supplied Stardew Valley **1.6.15.24356**. All fourteen groups start
**unverified**. Earlier passing browser milestones provide evidence for specific
contracts, not an assertion that an entire gameplay group works. Release requires
all groups below to pass. Fixtures/scenarios are defined in
[scenarios.json](../../tests/fixtures/scenarios.json); their states are planned
prerequisites, not injected successful outcomes.

| Group | Status | Concrete scenarios | Required observations |
| --- | --- | --- | --- |
| Start and configuration | unverified | `new-game-*` for all eight layouts | Native configuration/creation, original starting map/buildings/items/animals, save/load and movement; compare desktop |
| Farming | unverified | `farming-season` | Native tools, soil/watering, crop growth/season survival, weather, trees and greenhouse |
| Economy and inventory | unverified | `inventory-economy` | Quality/stacking/chests, shop prices, shipping settlement, currencies and tool upgrades |
| Crafting and production | unverified | `recipes-machines`, `text-sign-clipboard` | Recipe consumption/output, cooking, machine timers, automation, tailoring, decorations and persisted sign text |
| Animals and buildings | unverified | `animals-buildings` | Construction/upgrades/renovations, livestock/pets/mounts, production and friendship |
| Fishing and gathering | unverified | `fishing-gathering` | Native fishing minigame, bait/tackle, pots/ponds, forage and resource regeneration |
| Exploration and combat | unverified | `combat-dungeons` | Maps/warps, generated mines/Skull Cavern/volcano, monsters/weapons/buffs/drops |
| Characters and family | unverified | `characters-family` | Schedules/dialogue/gifts/friendship/events, marriage and children |
| Progression | unverified | `skills-mastery-achievements` | Skills/professions/mastery, original local achievements/collections/unlocks |
| Story and quests | unverified | `community-center`, `joja-orders-museum` | Both routes, bundles/rewards, mail/quests/orders/secrets/museum |
| Calendar and events | unverified | `festivals-events-movies` | Festivals/passive festivals/birthdays/random events/cutscenes/movies |
| Late game and minigames | unverified | `island-qi-perfection`, `original-minigames` | Island/walnuts/Qi/perfection, native Prairie King/Junimo Kart input/progression/rewards |
| Persistence | unverified | `advanced-desktop-roundtrip` | Advanced desktop import/migration, full day/sleep/reload/export; retain previous bytes on failed write |
| Presentation | unverified | `presentation-desktop` | Original UI/fonts/effects/audio/localization; input/fullscreen; desktop comparison and timings |

## Current evidence and remaining comparisons

Tasks 1–6 have verified deterministic source/content recovery, original browser
startup/creation/day rollover, lossless wave decoding, atomic save persistence,
legacy bundle migration, native focus/fullscreen/clipboard, and map screenshots.
See [compatibility](compatibility.md), [save evidence](save-verification.json)
and [lifecycle evidence](lifecycle-verification.json). Those checks cover a
Standard-farm journey and constructed migration fixture; they do not replace
advanced save or desktop comparisons.

The desktop reference harness uses the supplied unchanged game, game-data and
MonoGame assemblies with original content, an offline SDK, the host .NET runtime,
Xvfb/Mesa graphics and an OpenAL null output device. It runs only during build
verification; browser gameplay never uses it as a server. Windows path aliases
in temporary storage may be needed for Linux. Exact original Windows runtime,
physical controller, audible hardware DSP, other browser engines and release
performance remain explicit comparison limits until tested.

## Eight original farm references

The supplied unchanged desktop game creates and reloads all eight original farm
layouts (nine checks including title, 9/9 passing). The browser imports each
original save and uses original LoadGameMenu.SaveFileSlot.Activate. All eight
compare equal for farm ID/map, original building types/positions, animal
types/names, farmer name/customization/money, date, and the complete sorted
location-name list. Ordinary A input moves the farmer after each load.
The browser run passes all nine checks in 151 seconds.

The original desktop reload expands its 80 initial locations to 87; the browser
matches the desktop reload state. This behavior was independently measured,
rather than using the browser result as an expectation. Meadowlands retains its
original coop and two named starter chickens. See
[reference fixture evidence](reference-fixtures.json). All-layout creation in
the browser is now verified below; advanced gameplay groups remain open.

## Original crop method comparison

The original desktop serializer creates a Spring 28/year 2 fixture containing
watered, dry and greenhouse parsnips. Both runtimes load those original bytes
and call their own original crop methods. Watered growth advances; dry growth
does not. Four watered updates produce one harvested parsnip. An explicit
Summer 1 update kills the outdoor parsnip and preserves the greenhouse crop.
The desktop check passes 1/1 in 33 seconds; the browser comparison passes 1/1
in 55 seconds, with identical observed phase/day/harvest/death fields.

This verifies those original methods and serialization prerequisites. The extended comparison also passes original Hoe/WateringCan behavior and
fruit/wild-tree growth (browser 1/1, 54 seconds). Natural Spring 28→Summer 1 season/weather rollover also matches the
desktop through the populated advanced fixture below. These checks cover
representative behavior; the complete final parity gate is still pending.

## Browser farm creation and production methods

All eight farms pass normal browser title/customization controls, text entry,
farm selectors, skip intro, original OK, initial overnight/save and movement.
Their map/farm ID, starting structures and animal types match the original
desktop layouts (9/9 checks, 708 seconds). Browser creation uses the original
default random seed; seeded reference imports separately compare animal names.

Inventory/economy and production comparisons pass 3/3 in 101 seconds: original
chest stacking separates quality and splits999+11, parsnip quality prices are
35/43/52/70, the original shop sells parsnip seeds for20 gold, currency charges
match, and the shipping parser totals157 gold. Original recipes consume50 wood
for a Chest and one egg for Fried Egg. The original mayonnaise machine consumes
one egg and becomes ready after its180-minute timer. These are method/menu
comparisons; upgrade returns, overnight settlement, automation, tailoring and
decoration remain open until their own scenarios pass.

## Populated advanced save and desktop export compatibility

An original desktop fixture contains quality-separated chest/inventory items,
parsnips inside/outside the greenhouse, a processing indoor mayonnaise machine,
Linus friendship, foraging experience/professions, recipes, currencies and
shipping items. The browser loads the original bytes, sleeps using ordinary
movement and the original bed question, accepts the original shipping screen
and waits for the original save/wake flow. Summer 1, weather, 657 gold, dead
outdoor/surviving greenhouse crops, ready mayonnaise and all observed original
fields match the desktop. The exported ZIP cold-loads through the original
browser Load menu, with the same fields and working movement (1/1, 180 seconds).

The supplied unchanged desktop game also loads the browser-exported ZIP and
compares equal for every observed advanced field (1/1, 37 seconds). An earlier
test observed the loader's intermediate default world; waiting for original
gameMode 3 and IsProcessing=false fixes that test readiness race. Exported XML
retained the crop throughout; no serializer or gameplay patch was needed.

The final all-group gate is pending. Tool upgrades, automation/tailoring and
decoration, animals/buildings, fishing, combat, family/events, progression,
story routes, festivals, island/minigames and full presentation comparisons
still require their planned checks. Genuine older advanced desktop saves,
physical hardware and other browser engines remain comparison limits.
