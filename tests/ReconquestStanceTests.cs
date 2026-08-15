// CivOne tests
//
// A civ whose cities the barbarians are sitting in goes and gets them back.
//
// The deadlock this breaks: attackers are Considered only in the Militarize stance, and attack
// targets are chosen only in Militarize — but Militarize was reached only by a civ that
// ALREADY out-gunned a neighbour. A civ that loses cities therefore could never build the army
// that would win them back.
//
// Measured at turn 393 of a live game: the Haudenosaunee down to three cities, holding six
// Militia and a Phalanx — every one attack 1 — while barbarian Legions and Knights held three
// cities whose OriginalOwner was still theirs. They had The Wheel the whole time, so
// BestAttacker would have given them a Chariot at attack 4, an even match. They never built
// one, in any game, ever.
//
// The clause is deliberately narrow, and the narrowness is the safety argument:
//
//   * BARBARIAN-held only. A rival holding our city is covered by the at-war clause while the
//     war lasts; after peace, re-arming forever over a city we have accepted losing is the
//     no-expiry pathology — and AI wars here end in permanent peace, so it would never clear.
//     Barbarians hold no diplomacy and never sue for peace.
//   * OUR cities only, by OriginalOwner. Mere proximity to a barbarian city used to trigger
//     Militarize and was removed for exactly the reason above: it never expired. This does —
//     the moment the cities are retaken or razed.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ReconquestStanceTests
	{
		// A small peaceful civ, and a city of its own that somebody else now holds.
		private static (Game g, Player p, City mine, City lost) AnOccupiedCiv(byte occupier)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 12);
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			City mine = g.AddCity(p, 0, 40, 25)!;
			mine.Size = 4;
			City lost = g.AddCity(p, 1, 44, 25)!;
			lost.Size = 4;
			// Taken: the owner changes, the ORIGINAL owner does not. That is the whole hinge.
			lost.Owner = occupier;
			Sim.ClearTasks();
			return (g, p, mine, lost);
		}

		private static string StanceOf(Player p)
			=> typeof(AI).GetMethod("GetStance",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), null)!.ToString()!;

		// The case: barbarians in a city that was ours.
		[Fact]
		public void ACivWhoseCityTheHordeHoldsMilitarises()
		{
			(Game g, Player p, City mine, City lost) = AnOccupiedCiv(occupier: 0);
			Assert.Equal(g.PlayerNumber(p), lost.OriginalOwner);

			Assert.Equal("Militarize", StanceOf(p));
		}

		// ...and having got them back, it stands down. The expiry is the entire reason this
		// clause is safe to have — the proximity rule it sits beside was removed for lacking
		// one.
		[Fact]
		public void RetakingTheCityEndsTheWarFooting()
		{
			(Game g, Player p, City mine, City lost) = AnOccupiedCiv(occupier: 0);
			Assert.Equal("Militarize", StanceOf(p));

			lost.Owner = g.PlayerNumber(p);

			Assert.NotEqual("Militarize", StanceOf(p));
		}

		// Razed, rather than retaken: also an ending.
		[Fact]
		public void ARazedCityAlsoEndsIt()
		{
			(Game g, Player p, City mine, City lost) = AnOccupiedCiv(occupier: 0);
			Assert.Equal("Militarize", StanceOf(p));

			lost.Size = 0;

			Assert.NotEqual("Militarize", StanceOf(p));
		}

		// A RIVAL holding one of our cities does not trigger it. While the war is on, the
		// at-war clause above already militarises; once peace is signed this would never
		// clear, and AI wars here end in permanent peace.
		[Fact]
		public void ARivalHoldingOurCityDoesNotTriggerIt()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			Player rival = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                                 && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 12);
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City mine = g.AddCity(p, 0, 40, 25)!;
			mine.Size = 4;
			City lost = g.AddCity(p, 1, 44, 25)!;
			lost.Size = 4;
			lost.Owner = g.PlayerNumber(rival);
			Sim.ClearTasks();
			Assert.False(p.IsAtWar(rival), "fixture: at peace, or the at-war clause answers first");

			Assert.NotEqual("Militarize", StanceOf(p));
		}

		// A barbarian city that was never ours does not trigger it either — that is the
		// proximity rule which was removed, and it must not come back this way.
		[Fact]
		public void ABarbarianCityThatWasNeverOursDoesNotTriggerIt()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 12);
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City mine = g.AddCity(p, 0, 40, 25)!;
			mine.Size = 4;
			City theirs = g.AddCity(g.Players.First(x => x is not null && g.PlayerNumber(x) == 0)!, 1, 44, 25)!;   // barbarian from birth
			theirs.Size = 4;
			Sim.ClearTasks();
			Assert.NotEqual(g.PlayerNumber(p), theirs.OriginalOwner);

			Assert.NotEqual("Militarize", StanceOf(p));
		}

		// The point of the stance, not just the stance: in it, the civ actually asks for
		// something that can fight. A plan full of Militia is what it had before.
		[Fact]
		public void TheOccupiedCivBuildsSomethingThatCanFight()
		{
			(Game g, Player p, City mine, City lost) = AnOccupiedCiv(occupier: 0);
			p.AddAdvance(new Advances.TheWheel(), false);   // BestAttacker -> Chariot, attack 4

			var plan = new System.Collections.Generic.List<IProduction>();
			System.Type stance = typeof(AI).GetNestedType("StrategyStance",
				System.Reflection.BindingFlags.NonPublic)!;
			typeof(AI).GetMethod("PlanProductionInto",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { plan, mine, System.Enum.Parse(stance, StanceOf(p)) });

			Assert.Contains(plan, x => x is IUnit u && u.Attack > 1);
		}
	}
}
