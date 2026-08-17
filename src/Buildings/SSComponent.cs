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
using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class SSComponent : BaseBuilding, ISpaceShip
	{
		private static readonly string[] _page1 =
		{
			"A SPACE COMPONENT provides",
			"PROPULSION and FUEL for a",
			"SPACESHIP.",
			"",
			"More components mean a shorter",
			"voyage to ALPHA CENTAURI — but",
			"MODULES are mass. A laden hull",
			"is slower than a light one.",
		};

		// Page 2 is the crossing table, and it is COMPUTED from Game.SpaceshipFlightYears
		// rather than typed out. The formula has already changed once — a maxed hull used to
		// cross in 6 years, an accidental 0.73c, and now takes 22 — and a hand-written table
		// would still be quoting the old figure with no test able to notice. This one cannot
		// drift from the ship the game actually flies.
		//
		// Sized to the page: the blurb is followed by the stats block (Civilopedia.DrawStats),
		// and lines are 9px from y=34 on a 200px screen, so about 13 lines is the budget here.
		// The old page-2 text claimed "arriving first wins the SPACE RACE", which stopped being
		// true when arrival became a milestone rather than an ending — dropped rather than
		// reworded, since the table is the more useful thing to put in its place.
		private static string[] Page2()
		{
			// The reader's own civilization, when there is one — the Civilopedia is also
			// browsable outside a game, where "no fuel" is the honest default.
			bool hasFuel = Game.Started && Game.Instance.HumanPlayer is not null
				&& Game.Instance.Progress(Game.PlayerNumber(Game.Instance.HumanPlayer)).HasExoticFuel;

			var lines = new List<string>
			{
				"Requires PLASTICS.",
				"",
				"Components pair as propulsion",
				"and fuel; an unmatched one",
				"adds nothing to your speed.",
				"",
				hasFuel ? "CROSSING TIME (4.4 LIGHT YEARS)"
				        : "CROSSING TIME - NO EXOTIC FUEL",
				" ENGINES MODULES  YEARS  SPEED",
			};
			foreach ((int comp, int module) in new[] { (16, 3), (16, 12), (8, 6), (4, 3), (2, 3) })
			{
				// The table shows what THIS civilization would achieve, which is the only
				// honest answer once speed depends on the fuel: quoting 0.2c to a civ that
				// cannot reach it would be the spaceship report's phantom colonists again.
				float years = Game.SpaceshipFlightYears(
					Game.SpaceshipStructuresNeeded(comp, module), comp, module, hasFuel);
				int milliC = (int)Math.Round(4.4f / years * 1000f);
				lines.Add($"   {comp / 2,-8}{module / 3,-7}{years,4:F0}   .{milliC:D3}c");
			}
			return lines.ToArray();
		}

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : Page2();

		public SSComponent() : base(16)
		{
			Name = "SS Component";
			RequiredTech = new Plastics();
			Type = Building.SSComponent;
		}
	}
}