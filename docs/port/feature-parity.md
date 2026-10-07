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
| Economy and inventory | unverified | `inventory-economy`, `tool-upgrades` | Quality/stacking/chests, shop prices, shipping settlement, currencies and tool upgrades |
| Crafting and production | unverified | `recipes-machines`, `tailoring-automation-decoration`, `text-sign-clipboard` | Recipe consumption/output, cooking, machine timers, automation, tailoring, decorations and persisted sign text |
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
comparisons. Shipping settlement is verified below; automation/tailoring and
furniture methods are verified in later sections. Tool upgrade returns and
normal tailoring/furniture UI and persistence remain open.

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

## Hopper, tailoring and furniture methods

A separate original serialized fixture compares the native hopper loading path,
including its mutex event, cloth+parsnip tailoring and valid indoor furniture
placement. Desktop1/1 (24 seconds) and browser1/1 (85 seconds) pass with identical
observations: one egg consumed, mayonnaise output with180-minute timer, shirt
1159 and one chair0 placed. Normal tailoring/furniture UI and post-action save
persistence remain open. The original sign hides its paste button, so the clipboard scenario uses Ctrl+V.

The desktop sign editor commits text and the original serializer/reload retains
it (1/1, 53 seconds). The browser reaches the real sign with ordinary movement,
clears its prior text with Backspace, pastes fresh clipboard text, commits through
the original editor, sleeps normally and cold-loads the saved text unchanged
(1/1, 219 seconds). Two initial failures were test-driver issues: leaving the
loaded bed opens an original Sleep question, and MonoGame names Backspace as
Keys.Back. Answering No and correcting the observed key name resolved them;
no sign or save gameplay logic changed.

## Construction and livestock reference

The unchanged desktop game passes original barn placement/resource consumption,
construction3→0days, Big Barn upgrade (12000gold, capacity8), chicken petting/egg
production, cow petting/milk production, dog friendship12 and owned stable horse
creation (1/1, 24 seconds). The browser matches every observed field (1/1, 56 seconds).
The fixture grants starting capital without changing lifetime earnings, avoiding
an unrelated earned-income event. Both runtimes use the original farm warp and
construction safety checks. Construction/renovation UI, other livestock, actual
mounting and full-day animal/building persistence remain open.

The affected browser method gate passes6/6 in228 seconds after the asynchronous
original farm-warp dispatch: animals56s, decoration52s, farming53s and inventory/
recipes65s. Production excludes all seven mutable fixture drivers, the scenario
bridge/module and reference saves (fresh audit1/1, 1.4 seconds). These checks
remain subsets of the fourteen groups; the full parity gate is pending.

## Fishing method reference

Desktop1/1 (51 seconds) and browser1/1 (54 seconds) match for original bait/tackle
attachments, crab-pot input/catch, fish-pond spawning and32 fixed16ms fishing-bar
updates under a controlled seed. The trap consumes one bait and produces
item718; the Sunfish pond grows1→2 fish. The original first-catch branch raises
difficulty30→50 while preserving the level5 bar height136. An initial test
expectation omitted that native branch; correcting the expectation needed no
gameplay change. Production excludes all eight fixture drivers (1/1, 2.3s).
Normal casting/catching/harvest, gathering and broader fishing workflows remain
required; the fishing group is not yet verified overall.

## Dungeon generation and combat methods

Desktop1/1 (30 seconds) and browser1/1 (71 seconds) match original Mine5,
Skull Cavern121 and Volcano1 dimensions, tile sheets, object hashes and monster
distributions. Rusty Sword damage data, slime damage/death/kill counter/drop
table, and speed/defense buff application/expiry also match. The mine owns a
separate random generator; controlling that input in both test drivers resolves
an initial comparison difference without changing original generation logic.
Production excludes all nine mutable fixture drivers. Normal dungeon warps,
traversal, weapon swings, spawned drops and broader variants remain required.

## NPC and family methods

Desktop1/1 (32 seconds) and browser1/1 (54 seconds) match a loved gold-quality
coconut gift to Linus:100 friendship points, gift counters and original reaction
dialogue. Original schedule times, destinations, facing and route lengths also
match. A serialized Abigail marriage, upgraded home and child are explicit
prerequisites; original child updates reach ages1/2/3 at days13/27/55. Courtship,
wedding, birth/adoption, normal conversations/heart events and full-day family
persistence remain required.

## Skill, mastery and local achievement methods

Desktop1/1 (72 seconds, CPU contended) and browser1/1 (71 seconds) match original
experience gains for all five skills, reaching level10 with50 queued notices.
Original farming/foraging experience produces10000 mastery experience and one
mastery level. The original mining reward button consumes that level and unlocks
Heavy Furnace and Statue Of The Dwarf King recipes. Earning the final gold toward
15000 lifetime income unlocks local achievement0; a Sunfish catch registers its
collection entry. The extended desktop check passes1/1 (34 seconds) using original
LevelUpMenu mouse input for both level5/10 menus of every skill. Browser1/1
(62 seconds) clicks the same ten original menus and obtains professions
0/2/6/8/12/14/18/20/24/26 while retaining the earlier method comparisons.
The readonly input-readiness observation uses original CanReceiveInput().
Other profession branches, other mastery rewards/unlocks and post-action
persistence remain open. Production excludes all sixteen mutable fixture drivers,
scenario module and reference saves (fresh audit1/1,1.4s).

## Bundle and Joja contribution methods

The unchanged desktop passes both checks (2/2,44 seconds); the browser matches
both original saved prerequisites and menu outcomes (1/1,63 seconds). Original
inventory clicks and ingredient slots consume the four Spring Crops items,
complete bundle0 and make20 spring seeds available. The original Joja vault
checkbox spends40000 gold and queues vault/Joja mail. No completion state is
injected. Reward collection, normal world/menu input, full routes, overnight
project activation, quests/orders/museum/secrets remain required. Production
excludes all twelve mutable fixture drivers (fresh audit1/1,1.2s).

## Festival setup and calendar methods

Desktop1/1 (57 seconds) and browser1/1 (68 seconds) match the original year2
Egg Festival Town warp, event setup, actor names, tile sheets and return to
player control. Ordinary browser keyboard input moves the player afterward.
All eight festival scripts load with matching metadata/hashes. Original birthday
gifting gives Linus640 friendship points; a Night Market date and four seasonal
movie choices also match. Contests/rewards, full passive-festival world flows,
movie screening, random events/cutscenes and persisted outcomes remain required.
Production excludes all thirteen mutable fixture drivers (fresh audit1/1,2.7s).

## Original arcade replay

Desktop1/1 (56 seconds) and browser1/1 (52 seconds) match controlled keyboard
input and16ms updates interpreted by the original games. Prairie King starts,
moves192 pixels, fires two bullets and records wave0/lives3 progress. Both
Junimo Kart modes start, generate36/40 tracks and match player coordinates,
physics, score110 and three lives. Endless mode deliberately creates no
checkpoints; progression mode creates one. The replay overrides hardware input
only during the synchronous test and restores it afterward. A subsequent normal browser check also passes (1/1,68 seconds): the original
Continue menu resumes progress, DOM D moves, Arrow Up fires, and Escape exits.
Junimo Kart DOM controls, full arcade progression/rewards and post-action
persistence remain required. Production excludes all fourteen mutable fixture drivers (1/1,1.2s).

## Island, Qi and perfection methods

Desktop1/1 (24 seconds) and browser1/1 (53 seconds) match the original limited
IslandFishing reward API/team update: seven requests credit five walnuts.
The original Hut perch mutex and animation/construction timers spend one walnut,
complete the upgrade and queue its mail; four walnuts remain. Original Qi order
constructors produce matching objectives/rewards for eleven implemented entries.
The supplied catalog also contains unused QiChallenge11, tagged NOT_IMPLEMENTED;
its original rejection and unconstructed Custom objective are preserved. Original
perfection rises0→0.0019230769. These are method checks, including null-location
reward crediting; physical island collection/warps, player UI, Qi completion/
rewards, full perfection/ending and post-action persistence remain open.
Production excludes all fifteen mutable fixture drivers (fresh audit1/1,1.1s).

## Tool upgrade purchase and collection

The unchanged desktop passes1/1 (39 seconds): the original ClintUpgrade shop
charges2000 gold and five copper bars, removes the basic axe and starts the
CopperAxe level1 upgrade. Two original farmer day updates decrement2→1→0; the
original blacksmith interaction returns the axe and clears the pending upgrade.
Browser1/1 (52 seconds) matches every field; the accompanying normal arcade
regression also passes1/1 (54 seconds). The extended original desktop test also
passes1/1 (78 seconds) across three actual nights and normal reloads. The full
browser check passes1/1 (304 seconds): original shop DOM purchase, first-night
pending upgrade/cold Load, second-night completion, counter interaction/animation/
popup/service Leave, normal walk from the farmhouse entrance to the bed, and
third-night save/cold Load retain the original concrete CopperAxe level1.
A focused original ready-save check covers counter/popup/home/bed input (1/1,
63 seconds). The original new-game/save-failure/import/migration regression also
passes1/1 (173 seconds). Tests await original night menus, queued morning messages
and durable IndexedDB XML before accepting completion. Async interop/storage
predicates use explicit awaited polling because the installed Playwright polling
implementation treats returned Promises as immediately truthy. These checks
preserve original gameplay and collision behavior. Production excludes all sixteen
mutable fixture drivers (fresh audit1/1,1.4s). Fresh tooling17/17, platform38/38
and game3/3 checks pass.
