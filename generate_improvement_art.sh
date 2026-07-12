#!/usr/bin/env bash
# Resize design/buildings PNGs to 1280x720 (fit + black padding) and
# install them into the game's improvement_art directory with the filenames
# that ImprovementArtScreen.FindArtPath() expects.

set -euo pipefail

SRC="$(dirname "$0")/design/buildings"
DEST=~/Library/Application\ Support/CivOne/data/improvement_art
REPO="$(dirname "$0")/runtime/sdl/Resources/defaults/data/improvement_art"

W=1280
H=720

resize_and_install() {
    local src="$1"
    local destname="$2"
    if [ ! -f "$src" ]; then
        echo "  SKIP (missing source): $destname"
        return 0
    fi
#    echo "magick $src -resize "${W}x${H}" -background black -gravity center -extent \"${W}x${H}\" $DEST/$destname"
    magick "$src" \
        -resize "${W}x${H}" \
        -background black \
        -gravity center \
        -extent "${W}x${H}" \
        "$DEST/$destname"
    cp "$DEST/$destname" "$REPO/$destname"
    echo "  $destname"
}

echo "Installing improvement art to: $DEST"

# Buildings — filename from: improvementName.ToLower().Replace(' ', '_') + ".png"
resize_and_install "$SRC/aqueduct.png"           "aqueduct.png"
resize_and_install "$SRC/bank.png"               "bank.png"
resize_and_install "$SRC/barracks.png"           "barracks.png"
resize_and_install "$SRC/Cathedral.png"          "cathedral.png"
resize_and_install "$SRC/citywalls.png"          "city_walls.png"
resize_and_install "$SRC/CivicMonument.png"      "civic_monument.png"
resize_and_install "$SRC/colosseum.png"          "colosseum.png"
resize_and_install "$SRC/Courthouse.png"         "courthouse.png"
resize_and_install "$SRC/factory.png"            "factory.png"
resize_and_install "$SRC/granary.png"            "granary.png"
resize_and_install "$SRC/hydroplant.png"         "hydro_plant.png"
resize_and_install "$SRC/InfrastructureBond.png" "infrastructure_bond.png"
resize_and_install "$SRC/Library.png"            "library.png"
resize_and_install "$SRC/marketplace.png"        "marketplace.png"
resize_and_install "$SRC/masstransit.png"        "mass_transit.png"
resize_and_install "$SRC/nuclearplant.png"       "nuclear_plant.png"
resize_and_install "$SRC/observatory.png"        "observatory.png"
resize_and_install "$SRC/Palace.png"             "palace.png"
resize_and_install "$SRC/powerplant.png"         "power_plant.png"
resize_and_install "$SRC/SAMbattery.png"         "sam_battery.png"
resize_and_install "$SRC/sewersystem.png"        "sewer_system.png"
resize_and_install "$SRC/ShipyardImproved.png"   "shipyard.png"
resize_and_install "$SRC/temple.png"             "temple.png"
resize_and_install "$SRC/university.png"         "university.png"
resize_and_install "$SRC/recyclingcenter.png"    "recycling_cntr..png"
resize_and_install "$SRC/ExchangeCenter.png"     "exchange_center.png"
resize_and_install "$SRC/Mfg.Plant.png"          "mfg._plant.png"
resize_and_install "$SRC/NeuralLab.png"          "neural_lab.png"
resize_and_install "$SRC/SeaPlatform.png"        "sea_platform.png"
resize_and_install "$SRC/SurplusDepot.png"       "surplus_depot.png"
resize_and_install "$SRC/Xenolab.png"            "xenolab.png"

# Wonders
resize_and_install "$SRC/FusionCore.png"              "fusion_core.png"
resize_and_install "$SRC/HumanGenome.png"             "human_genome.png"
resize_and_install "$SRC/PoliceStation.png"           "police_station.png"
resize_and_install "$SRC/AuditAuthority.png"          "audit_authority.png"
resize_and_install "$SRC/lighthouse.png"              "lighthouse.png"
resize_and_install "$SRC/ApolloProgram.png"           "apollo_program.png"
resize_and_install "$SRC/CopernicusObservatory.png"   "copernicus'_observatory.png"
resize_and_install "$SRC/CureForCancer.png"           "cure_for_cancer.png"
resize_and_install "$SRC/DarwinsVoyage.png"           "darwin's_voyage.png"
resize_and_install "$SRC/GreatLibrary.png"            "great_library.png"
resize_and_install "$SRC/GreatWall.png"               "great_wall.png"
resize_and_install "$SRC/HangingGardens.png"          "hanging_gardens.png"
resize_and_install "$SRC/HooverDam.png"               "hoover_dam.png"
resize_and_install "$SRC/IsaacNewtonsCollege.png"     "isaac_newton's_college.png"
resize_and_install "$SRC/JSBachCathedral.png"         "j.s.bach's_cathedral.png"
resize_and_install "$SRC/MagellansVoyage.png"         "magellan's_expedition.png"
resize_and_install "$SRC/ManhattanProject.png"        "manhattan_project.png"
resize_and_install "$SRC/Michelangelo'sChapel.png"    "michelangelo's_chapel.png"
#resize_and_install "$SRC/Oracle.png"                 "oracle.png"
resize_and_install "$SRC/Pyramids.png"                "pyramids.png"
resize_and_install "$SRC/ShakespearesTheater.png"     "shakespeare's_theatre.png"
resize_and_install "$SRC/SouthPoleExpedition.png"     "south_pole_expedition.png"
resize_and_install "$SRC/UnitedNations.png"           "united_nations.png"
resize_and_install "$SRC/WomensSuffrage.png"          "women's_suffrage.png"
resize_and_install "$SRC/InterstellarProbe.png"       "interstellar_probe.png"
resize_and_install "$SRC/Colossus.png"          "colossus.png"
resize_and_install "$SRC/SETIProgram.png"       "seti_program.png"
resize_and_install "$SRC/MarcoPoloVoyage.png"   "marco_polo's_voyage.png"
resize_and_install "$SRC/ZhengHeVoyage.png"     "zheng_he's_voyage.png"

resize_and_install "$SRC/TajMahal.png"          "taj_mahal.png"
resize_and_install "$SRC/HagiaSophia.png"       "hagia_sofia.png"

# Dome wonders
resize_and_install "$SRC/DomeCommandHub.png"    "dome_command_hub.png"
resize_and_install "$SRC/DomeEmitterArray.png"  "dome_emitter_array.png"
resize_and_install "$SRC/DomeKineticRing.png"   "dome_kinetic_ring.png"
resize_and_install "$SRC/DomePowerCore.png"     "dome_power_core.png"
resize_and_install "$SRC/DomeSensorNet.png"     "dome_sensor_net.png"

# SDI Defense (building)
resize_and_install "$SRC/SDIDefense.png"              "sdi_defense.png"

# ── Event art ────────────────────────────────────────────────────────────────
DEST_EVENT=~/Library/Application\ Support/CivOne/data/event_art
REPO_EVENT="$(dirname "$0")/runtime/sdl/Resources/defaults/data/event_art"

resize_and_install_event() {
    local src="$1"
    local destname="$2"
    if [ ! -f "$src" ]; then
        echo "  SKIP (missing source): $destname"
        return 0
    fi
    magick "$src" \
        -resize "${W}x${H}" \
        -background black \
        -gravity center \
        -extent "${W}x${H}" \
        "$DEST_EVENT/$destname"
    cp "$DEST_EVENT/$destname" "$REPO_EVENT/$destname"
    echo "  $destname"
}

echo "Installing event art to: $DEST_EVENT"

resize_and_install_event "$SRC/InciteRebellion.png"       "incite_rebellion.png"
resize_and_install_event "$SRC/civilunrest0.png"          "civilunrest0.png"
resize_and_install_event "$SRC/civilunrest1.png"          "civilunrest1.png"
resize_and_install_event "$SRC/civilunrest2.png"          "civilunrest2.png"
resize_and_install_event "$SRC/governmentcollapses.png"   "governmentcollapses.png"
resize_and_install_event "$SRC/cityconquered.png"         "cityconquered.png"
resize_and_install_event "$SRC/cityliberated.png"         "cityliberated.png"
resize_and_install_event "$SRC/Famine.png"                "famine.png"
resize_and_install_event "$SRC/GlobalWarming.png"         "globalwarming.png"
resize_and_install_event "$SRC/NuclearBombDetonation.png" "nuclearbombdetonation.png"
resize_and_install_event "$SRC/NuclearMeltdown.png"       "nuclearmeltdown.png"
resize_and_install_event "$SRC/Pollution.png"             "pollution.png"
resize_and_install_event "$SRC/Hurricane.png"             "hurricane.png"
resize_and_install_event "$SRC/SpaceshipArrived.png"      "spaceshiparrived.png"
resize_and_install_event "$SRC/SpaceshipIntercepted.png"  "spaceshipintercepted.png"
resize_and_install_event "$SRC/SpaceshipLaunched.png"     "spaceshiplaunched.png"
resize_and_install_event "$SRC/WeLoveTheKingDay.png"      "welovethekingday.png"
# Owners invasion arc — keys are CamelCase (EventArtScreen.FindPath matches the exact name)
resize_and_install_event "$SRC/TheOthersArrive.png"       "TheOthersArrive.png"
resize_and_install_event "$SRC/Repossession.png"          "Repossession.png"
# South Pole Expedition curse
resize_and_install_event "$SRC/TheThing.png"              "TheThing.png"

echo "Done."
