// CivOne tests
//
// The AI's tax/luxury/science split. The failure this guards against is a total
// research shutdown: the raise branch of ConsiderSliders only ever moved the
// luxury slider UP, and its trigger ("any city in disorder") is permanently true
// for a large empire, so luxuries ratcheted to 8 against a tax floor of 2 and
// science got nothing for the rest of the game. Measured at turn 440 of a real
// game: Romans 79 cities on 30 advances, Mongols 51 cities on 15.

using System;
using System.Linq;
using CivOne;

namespace CivOne.Tests
{
	public class SliderPolicyTests
	{
		// A civ already driven to the old 2/8/0 split must climb back out of it.
		//
		// Scope: the Sim harness cannot manufacture civil disorder, so this exercises
		// the calm path — either the ceiling clamp or the wind-down will rescue it, and
		// the test only fails when BOTH are broken. The clamp's own case (high unrest,
		// where the raise branch would otherwise pin the slider) is verified against a
		// real save rather than here.
		[Fact]
		public void Sliders_AlwaysLeaveTradeForScience()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;

			// The state the old policy pinned every large empire to.
			player.TaxesRate     = 2;
			player.LuxuriesRate  = 8;
			Assert.Equal(0, 10 - player.TaxesRate - player.LuxuriesRate);

			for (int turn = 0; turn < 20; turn++) player.AI!.ConsiderSliders();

			int science = 10 - player.TaxesRate - player.LuxuriesRate;
			Assert.True(science >= 2,
				$"expected at least 2 trade on research, got {science} "
				+ $"(tax {player.TaxesRate}, luxuries {player.LuxuriesRate})");
		}

		// Science is the remainder of the other two sliders, so pushing either past
		// (10 - the other) drives it NEGATIVE rather than buying anything. The AI did
		// exactly that — tax 3 against luxuries 8, for a science rate of -1 — so the
		// invariant is enforced in the setters and this pins it.
		[Fact]
		public void Sliders_CannotDriveScienceNegative()
		{
			Sim.NewGame(width: 80, height: 50);
			Player player = Game.Instance.HumanPlayer;

			player.TaxesRate    = 8;
			player.LuxuriesRate = 8;   // would be 16 points of a 10-point budget

			Assert.True(player.TaxesRate + player.LuxuriesRate <= 10,
				$"tax {player.TaxesRate} + luxuries {player.LuxuriesRate} exceeds the budget");
			Assert.True(player.ScienceRate >= 0, $"science rate {player.ScienceRate} went negative");

			// ...and the same from the other direction.
			player.LuxuriesRate = 10;
			player.TaxesRate    = 7;
			Assert.True(player.TaxesRate + player.LuxuriesRate <= 10);
			Assert.True(player.ScienceRate >= 0);
		}

		// The real path, on a real save: 71 Malian cities with one in disorder, parked at
		// tax 5 / luxuries 5 / science 0. Two things must hold for every civ in it —
		// research restarts, and the sliders SETTLE. With a single crisis threshold the
		// two halves of ConsiderSliders traded one point back and forth forever (lowering
		// luxuries tipped cities into disorder, unrest crossed 40%, the crisis branch put
		// the point back), which is a stable two-turn cycle at science 0.
		[Fact]
		public void Sliders_SettleAndKeepResearchAlive_OnARealSave()
		{
			Sim.EnsureRuntime();
			Sim.ResetState();
			Settings.Instance.Autopilot = true;
			Assert.True(Game.LoadCos(System.IO.Path.Combine(
				AppContext.BaseDirectory, "fixtures", "CIVIL3.cos")), "fixture should load");

			foreach (Player p in Game.Instance.Players.Where(p => !p.IsDestroyed() && p.Cities.Length > 0))
			{
				int trade = p.Cities.Sum(c => c.TradeTotal);
				var states = new System.Collections.Generic.List<string>();
				for (int turn = 0; turn < 12; turn++)
				{
					AI.Instance(p).ConsiderSliders();
					states.Add($"{p.TaxesRate}/{p.LuxuriesRate}/{p.ScienceRate}");
					Assert.True(p.ScienceRate >= 0,
						$"{p.TribeNamePlural} science rate {p.ScienceRate} went negative");
					Assert.True(p.TaxesRate + p.LuxuriesRate <= 10,
						$"{p.TribeNamePlural} sliders sum past 10");
				}

				Assert.Single(states.Skip(states.Count - 4).Distinct());
				if (trade > 0)
					Assert.True(p.ScienceRate > 0,
						$"{p.TribeNamePlural} has {trade} trade but ended on science rate 0");
			}
		}
	}
}
