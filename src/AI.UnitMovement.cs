// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne
{
    internal partial class AI : BaseInstance
    {
        // ── deterministic movement resolution ───────────────────────────────────
        // When a unit cannot execute its current action, this evaluates what it 
        // should do next based on strategy and role, rather than randomization.
        
        private enum MovementFailureResolution
        {
            RetryNextTurn,      // Abort this move, try again next turn
            ClearMissionWait,   // Mission failed; wait for reassignment next turn
            DisembarkAndFortify, // Last resort; secure current position
            DisbandStranded     // Unrecoverable stranding (rare)
        }

        private MovementFailureResolution ResolveMovementFailure(IUnit unit, ITile attemptedTarget)
        {
            // Settlers: movement failure near cities usually means goal achieved or blocked
            // Clear Goto and let reassign find new target on next turn.
            if (unit is Settlers)
                return MovementFailureResolution.ClearMissionWait;

            // Military units: if we're staging an assault and failed to advance,
            // stay put for reinforcements or wait for path to clear
            if (unit.Role == UnitRole.LandAttack && _attackTarget != null)
            {
                // Only abandon if it's clearly a dead-end (surrounded by water/cities)
                if (!unit.Tile.GetBorderTiles().Any(t => t != null && !t.IsOcean && t.City != null))
                    return MovementFailureResolution.DisembarkAndFortify;
                return MovementFailureResolution.RetryNextTurn;
            }

            // Civilians (Diplomat, Caravan): can't force through, retreat
            if (unit.Role == UnitRole.Civilian)
                return MovementFailureResolution.ClearMissionWait;

            // Transport ships: regroup at embarkation point
            if (unit.Role == UnitRole.Transport)
                return MovementFailureResolution.ClearMissionWait;

            // Default: retry conservative approach (don't disband)
            return MovementFailureResolution.RetryNextTurn;
        }

        private void HandleMovementFailure(IUnit unit, ITile attemptedTarget)
        {
            var resolution = ResolveMovementFailure(unit, attemptedTarget);

            switch (resolution)
            {
                case MovementFailureResolution.RetryNextTurn:
                    // Keep current Goto; this unit's turn ends here
                    unit.SkipTurn();
                    break;

                case MovementFailureResolution.ClearMissionWait:
                    // Clear Goto and abort this turn; AI will reassign next turn
                    unit.Goto = Point.Empty;
                    unit.SkipTurn();
                    break;

                case MovementFailureResolution.DisembarkAndFortify:
                    // Can't proceed; secure current tile
                    if (unit.Class == UnitClass.Land && unit.Tile.IsOcean)
                    {
                        // Land unit stranded on ocean: try adjacent land tile
                        ITile land = unit.Tile.GetBorderTiles()
                            .FirstOrDefault(t => t != null && !t.IsOcean);
                        if (land != null)
                        {
                            if (!unit.MoveTo(land.X - unit.X, land.Y - unit.Y))
                                unit.SkipTurn();
                        }
                        else
                            unit.Fortify = true; // No escape; hold position
                    }
                    else
                    {
                        unit.Goto = Point.Empty;
                        unit.Fortify = true;
                    }
                    break;

                case MovementFailureResolution.DisbandStranded:
                    // Only disband if truly unrecoverable
                    Log($"[AI] {unit.GetType().Name} P{unit.Owner} stranded at ({unit.X},{unit.Y}); disbanding.");
                    Game.DisbandUnit(unit);
                    break;
            }
        }
    }
}
