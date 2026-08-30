// CivOne tests
//
// The post-contact buildings had no cultural weight, which left the culture victory a
// pre-industrial contest: a civilization that reached Xenobiology and memetic protocols had
// nothing new to say for itself.
//
// Only two of the five touched happiness at all, and both gave the weakest effect in the game
// — a Temple's -1, without a Temple's doubling under Mysticism or the Oracle. So:
//
//   Exchange Center  -1 unhappy  ->  +3 culture   (a trade, not an addition)
//   Xenolab          +50% science + 2 culture
//   Neural Lab       -1 unhappy  + 2 culture
//   Surplus Depot    unchanged — logistics
//   Sea Platform     unchanged — already one of the strongest tile effects in the game

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class PostContactCultureTests
	{
		private static City ACity()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 44; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player p = g.HumanPlayer;
			p.Explore(38, 25, range: 20);
			City c = g.AddCity(p, 0, 38, 25)!;
			c.Size = 8;
			Sim.ClearTasks();
			return c;
		}

		private static int Culture(City c) => c.CultureRate;

		[Theory]
		[InlineData(typeof(ExchangeCenter), 3)]
		[InlineData(typeof(Xenolab), 2)]
		[InlineData(typeof(NeuralLab), 2)]
		public void EachPostContactBuildingAddsItsCulture(System.Type type, int expected)
		{
			City c = ACity();
			int before = Culture(c);

			c.AddBuilding((IBuilding)System.Activator.CreateInstance(type)!);

			Assert.Equal(before + expected, Culture(c));
		}

		// The two that were deliberately left alone. A test that only asserted the additions
		// would pass just as well if culture had been sprinkled over all five.
		[Theory]
		[InlineData(typeof(SurplusDepot))]
		[InlineData(typeof(SeaPlatform))]
		public void TheInfrastructureBuildingsAddNoCulture(System.Type type)
		{
			City c = ACity();
			int before = Culture(c);

			c.AddBuilding((IBuilding)System.Activator.CreateInstance(type)!);

			Assert.Equal(before, Culture(c));
		}

		// The Exchange Center TRADED its happiness away. Without this the change reads as a
		// free upgrade rather than the swap it was chosen to be.
		[Fact]
		public void TheExchangeCentreNoLongerCalmsAnyone()
		{
			City c = ACity();
			c.Size = 12;
			c.AddBuilding(new ExchangeCenter());
			int withExchange = c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale);

			City bare = ACity();
			bare.Size = 12;
			int without = bare.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale);

			Assert.Equal(without, withExchange);
		}

		// ...while the Neural Lab kept its own. It gained culture rather than trading for it,
		// and a sweep that treated the two alike would be wrong about one of them.
		[Fact]
		public void TheNeuralLabStillCalmsACitizen()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "City.cs"));

			Assert.Contains("if (HasBuilding<NeuralLab>()) unhappyCount -= 1;", src);
			Assert.DoesNotContain("if (HasBuilding<ExchangeCenter>()) unhappyCount -= 1;", src);
		}

		// The Xenolab is still a science building. Culture was added to it, not swapped in.
		[Fact]
		public void TheXenolabStillMultipliesScience()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "City.cs"));

			Assert.Contains("HasBuilding<Xenolab>()) science +=", src);
		}

		// A city with all three is worth 7 a turn — the figure the balance was chosen against,
		// so a later tweak to any one of them has to be a decision rather than a drift.
		[Fact]
		public void AFullyEquippedCityGainsSeven()
		{
			City c = ACity();
			int before = Culture(c);

			c.AddBuilding(new ExchangeCenter());
			c.AddBuilding(new Xenolab());
			c.AddBuilding(new NeuralLab());

			Assert.Equal(before + 7, Culture(c));
		}

		// The happiness report must not go on advertising an effect that moved. It listed the
		// Exchange Center among the buildings keeping a city calm.
		[Fact]
		public void TheAttitudeSurveyNoLongerListsTheExchangeCentre()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "Screens", "Reports", "AttitudeSurvey.cs"));
			int at = src.IndexOf("_moodSlots");
			string table = src.Substring(at, src.IndexOf("};", at) - at);

			Assert.DoesNotContain("ExchangeCenter", table);
			Assert.Contains("NeuralLab", table);
		}

		// The AI refuses happiness buildings to a city that is already content. The Exchange
		// Center had to come off that list with its happiness effect, or the AI would decline
		// a culture building for a reason it no longer has — and decline it precisely in the
		// calm cities best placed to build one.
		[Fact]
		public void TheAiNoLongerTreatsTheExchangeCentreAsAMoodBuilding()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "AI.Strategy.cs"));
			int at = src.IndexOf("city.UnhappyCitizens == 0 && Player.LuxuriesRate == 0");
			Assert.True(at > 0, "the AI's content-city gate has moved");
			// LastIndexOf(value, startIndex) — searching BACK from the gate to the `if` that
			// owns it. The three-argument overload counts characters rather than bounding the
			// search, which is how the first draft of this looked backwards from zero.
			int from = src.LastIndexOf("if (building is", at);
			Assert.True(from > 0 && from < at, "could not find the clause the gate belongs to");
			string clause = src.Substring(from, at - from);

			Assert.DoesNotContain("ExchangeCenter", clause);
			Assert.Contains("NeuralLab", clause);
		}

		// A Culture-path civilization has to actually WANT these. Its production case listed
		// only Temple and Cathedral, and its research weighting only pre-industrial advances,
		// so making the buildings cultural did nothing for the civ playing for culture — it
		// picked them up on ordinary merit like everybody else.
		[Fact]
		public void TheCulturePathPrefersThePostContactBuildings()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "AI.Strategy.cs"));
			int at = src.IndexOf("case VictoryPath.Culture:\n\t\t\t\t\t// Culture accrues");
			Assert.True(at > 0, "the Culture production case has moved or been rewritten");
			string block = src.Substring(at, src.IndexOf("break;", at) - at);

			Assert.Contains("Consider(new ExchangeCenter())", block);
			Assert.Contains("Consider(new Xenolab())", block);
			Assert.Contains("Consider(new NeuralLab())", block);
		}

		// ...and researches toward them. Without this the buildings are preferred but arrive
		// no sooner, which on a post-contact tech tree is most of the delay.
		[Fact]
		public void TheCulturePathResearchesTowardThem()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "AI.Strategy.cs"));
			int at = src.IndexOf("// Cultural Ascendancy requires it");
			Assert.True(at > 0, "the Culture research weighting has moved");
			string block = src.Substring(at, src.IndexOf("break;", at) - at);

			Assert.Contains("a is MemeticProtocols", block);
			Assert.Contains("a is Xenobiology", block);
			Assert.Contains("a is NeuralInterface", block);
		}

		// Philosophy stays the top weight: the victory REQUIRES it (Game.cs), so no building
		// advance may outrank the one without which none of it counts.
		[Fact]
		public void PhilosophyStillOutranksTheBuildingAdvances()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "AI.Strategy.cs"));
			// Anchored on the Culture case's own comment: `a is Philosophy` is weighted on
			// three different paths and IndexOf found the first of them.
			int at = src.LastIndexOf("if (a is Philosophy)",
				src.IndexOf("// Cultural Ascendancy requires it"));
			string block = src.Substring(at, src.IndexOf("break;", at) - at);

			int Weight(string advance)
			{
				int i = block.IndexOf("a is " + advance);
				Assert.True(i > 0, $"{advance} is not weighted on the culture path");
				var m = System.Text.RegularExpressions.Regex.Match(block.Substring(i), @"weight \+= (\d+)");
				return int.Parse(m.Groups[1].Value);
			}

			Assert.True(Weight("Philosophy") > Weight("MemeticProtocols"));
			Assert.True(Weight("MemeticProtocols") > Weight("Xenobiology"));
		}

		// Nor may the Civilopedia. Its page still promised the unrest reduction the building no
		// longer provides — the kind of drift that turns a rules change into a lie.
		[Fact]
		public void ThePediaDescribesWhatTheBuildingsNowDo()
		{
			string Page(string name) => System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "Buildings", name + ".cs"));

			Assert.Contains("CULTURE", Page("ExchangeCenter"));
			Assert.DoesNotContain("reduces unrest", Page("ExchangeCenter"));
			Assert.Contains("CULTURE", Page("Xenolab"));
			Assert.Contains("CULTURE", Page("NeuralLab"));
			Assert.Contains("reducing unrest", Page("NeuralLab"));   // it kept that one
		}
	}
}
