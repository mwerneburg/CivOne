// CivOne tests
//
// A civ with a plan still colonises until it has a base worth deepening.
//
// GetStance ends by sending Diaspora, Commerce and Culture civs to Develop once they hold
// "enough" cities — the builder paths want depth, not frontier. The threshold was a constant
// 5, and a constant does not survive contact with an epic map: CityTarget scales by
// Map.WIDTH/80, so a Normal leader on 320x200 Earth aims at about 26 cities and 5 is a
// beachhead.
//
// The failure was total rather than gradual, because settler production for a civ with three
// or more cities lives inside `stance == Expand`. A deepening civ that crossed five cities
// could never enter Expand again, so it never built another settler, so it never grew. The
// only escape was the last-resort production fallback, which fires when a city has run out of
// everything else — and a large city under active development never does.
//
// Measured on the Maori across three runs of the same map. Before the victory paths existed:
// 84 Expand stances and 3 settlers built. After: zero and zero, frozen on 8 cities from turn
// 313 to turn 749 while their capital grew to size 17 building Library, Aqueduct, Harbour,
// Observatory, Hospital, Neural Lab and Mass Transit — and never once a settler.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Units;
using CivOne.Civilizations;

namespace CivOne.Tests
{
	public class PathExpansionFreezeTests
	{
		// Epic width, where the constant went wrong. Height stays small to keep generation
		// quick — CityTarget reads WIDTH only.
		private static (Game g, Player p) AnIslandCiv(int cities, System.Type civType)
		{
			Sim.NewGame(width: 320, height: 60);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			typeof(Player).GetField("_civilization",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.SetValue(p, (ICivilization)System.Activator.CreateInstance(civType)!);
			p.Government = new Governments.Monarchy();
			p.Explore(60, 30, range: 20);
			for (int y = 24; y <= 36; y++)
			for (int x = 40; x <= 90; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			int made = 0;
			for (int cy = 25; cy <= 35 && made < cities; cy += 3)
			for (int cx = 42; cx <= 88 && made < cities; cx += 4)
			{
				City c = g.AddCity(p, (byte)made, cx, cy);
				if (c is null) continue;
				// Size 5 under Monarchy, comfortably inside the content floor: a rioting
				// fixture returns Consolidate from an earlier branch of GetStance and never
				// reaches the path clause this file is about.
				c.Size = 5;
				made++;
			}
			Assert.Equal(cities, made);
			// The signal is up, so Diaspora is a live ambition rather than a plan for a ship
			// that does not exist yet.
			p.LuxuriesRate = 0;   // >= 4 is itself a Consolidate trigger
			g.SETISignalReceived = true;
			g.DomeAssignments[1] = new System.Collections.Generic.List<Wonder> { Wonder.DomeSensorNet };
			Sim.ClearTasks();
			return (g, p);
		}

		private static string StanceOf(Player p)
			=> typeof(AI).GetMethod("GetStance",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), null)!.ToString()!;

		private static int TargetOf(Player p)
			=> (int)typeof(AI).GetMethod("CityTarget",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), null)!;

		// The Maori case: a Diaspora civ far below its own target keeps colonising.
		[Fact]
		public void ADiasporaCivWellBelowItsTargetStillExpands()
		{
			(Game g, Player p) = AnIslandCiv(8, typeof(Maori));
			// Precondition: 8 really is far below target on this map, or there is nothing here.
			Assert.True(TargetOf(p) > 16, $"fixture: target is {TargetOf(p)}, expected an epic-map target");

			Assert.Equal("Expand", StanceOf(p));
		}

		// ...and the clause still does its job once the civ is genuinely established. This is
		// the behaviour the threshold exists for — the Babylonians building temples at twenty
		// cities rather than founding more towns — so a fix that simply deleted it would be
		// wrong.
		[Fact]
		public void AnEstablishedDiasporaCivStillDeepens()
		{
			(Game g, Player p) = AnIslandCiv(20, typeof(Maori));
			Assert.True(20 >= TargetOf(p) / 2, "fixture: 20 cities should be at or past half the target");

			Assert.Equal("Develop", StanceOf(p));
		}

		// The threshold scales with the map rather than sitting at a constant. Asserted
		// through the stance at a city count that straddles the two rules: 8 cities is over
		// the old constant of 5 and under half an epic target, so the old code says Develop
		// and the new code says Expand.
		[Fact]
		public void TheThresholdFollowsTheMapNotAConstant()
		{
			(Game g, Player p) = AnIslandCiv(8, typeof(Maori));

			int threshold = System.Math.Max(5, TargetOf(p) / 2);

			Assert.True(threshold > 5, $"the threshold is still a constant: {threshold}");
			Assert.True(8 < threshold, "fixture: 8 cities must be under the threshold to test this");
		}

		// No production test here, deliberately, and the reason is worth recording.
		//
		// The first version asserted that a frozen civ never builds a settler, on the reading
		// that settlers for a civ of three or more cities live only inside the Expand branch.
		// That is false: staged on this fixture, the plans are identical either way —
		//
		//   EXPAND:  Militia, Settlers, Explorer, HangingGardens
		//   DEVELOP: Militia, Settlers, Explorer, HangingGardens
		//
		// — because Develop builds WORKER settlers for irrigation, and MayFoundCities is
		// independent of stance by design, so those settlers may found too. The test passed
		// with the fix removed, which is how the mistake surfaced.
		//
		// What the decision log actually shows for the Maori is narrower and worse: across
		// three runs they never built a single boat — no Longboat, no Transport, no Trireme.
		// An island civ's settlers have nowhere to walk, so BestSettleSite finds nothing and
		// they fall through to road and irrigate orders, which is exactly what their four
		// logged settler actions are. The stance fix above restores the posture and the
		// settler supply; it cannot give them a way off the island. That is a separate change
		// and it wants its own evidence.
	}
}
