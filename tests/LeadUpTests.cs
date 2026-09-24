// CivOne tests
//
// Both late endings used to arrive unannounced: the Machines already at war, the alien's
// first word a koan (the user, Sep 2026). Each now has a lead-up read off the same world
// count that triggers it —
//
//   Neural Labs: 3rd, the labs start talking; 4th, the audit says five is a quorum
//   Xenolabs (Starlab standing): 3rd, Vault B-7 is filling; 4th, something in it is
//   sorting; 5th, the vault opens and THEN the first koan
//
// — and every transmission in both stories can be replayed from Communications.

using System.IO;
using System.Linq;
using System.Reflection;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Screens.Reports;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class LeadUpTests : System.IDisposable
	{
		// Skynet is behind the cursed-wonders switch, which the shared test settings leave
		// off. Set per test and put back, so no other test inherits it.
		private readonly bool _cursed;
		public LeadUpTests()
		{
			Sim.EnsureRuntime();
			_cursed = Settings.Instance.CursedWonders;
			Settings.Instance.CursedWonders = true;
		}
		public void Dispose() => Settings.Instance.CursedWonders = _cursed;

		private static void Call(Game g, string method) =>
			typeof(Game).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(g, null);

		private static int Recorded(Game g, string type) => g.Transmissions.Count(t => t.Type == type);

		// `labs` cities holding `building`, and a Starlab if asked. Spaced so each is its own.
		private static Game AWorldWith<T>(int labs, bool starlab = false) where T : IBuilding, new()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.HumanPlayer;
			for (int i = 0; i < 6; i++)
			{
				City c = g.AddCity(p, (byte)i, 24 + i * 5, 25)!;
				c.Size = 4;
				if (i < labs) c.AddBuilding(new T());
			}
			if (starlab)
			{
				g.StarlabQuality = StarlabQuality.Intended;
				p.Cities.Last().AddWonder(new Starlab());
			}
			Sim.ClearTasks();
			return g;
		}

		// ── the Machines ─────────────────────────────────────────────────────

		[Fact]
		public void TwoNeuralLabsSayNothing()
		{
			Game g = AWorldWith<NeuralLab>(2);
			Call(g, "CheckSkynet");
			Assert.Equal(0, g.NeuralLabWarnings);
			Assert.Empty(Sim.PendingTaskTypes());
		}

		[Fact]
		public void TheThirdLabStartsTheTalk()
		{
			Game g = AWorldWith<NeuralLab>(3);
			Call(g, "CheckSkynet");
			Assert.Equal(1, g.NeuralLabWarnings);
			Assert.Contains("Message", Sim.PendingTaskTypes());
		}

		// Once, not every turn.
		[Fact]
		public void TheFourthLabIsAuditedOnce()
		{
			Game g = AWorldWith<NeuralLab>(4);
			Call(g, "CheckSkynet");
			Call(g, "CheckSkynet");
			Assert.Equal(2, g.NeuralLabWarnings);
			Assert.Equal(1, Recorded(g, "NeuralQuorum"));
			Assert.False(g.SkynetRisen, "four labs woke the machines");
		}

		// Control: the fifth is still the uprising.
		[Fact]
		public void TheFifthLabIsStillTheUprising()
		{
			Game g = AWorldWith<NeuralLab>(5);
			Call(g, "CheckSkynet");
			Assert.True(g.SkynetRisen);
		}

		// ── the alien ────────────────────────────────────────────────────────

		[Fact]
		public void TheThirdXenolabFillsTheVault()
		{
			Game g = AWorldWith<Xenolab>(3, starlab: true);
			Call(g, "ProcessKoans");
			Assert.Equal(1, g.XenolabWarnings);
			Assert.Contains("Message", Sim.PendingTaskTypes());
		}

		[Fact]
		public void TheFourthXenolabSetsSomethingSorting()
		{
			Game g = AWorldWith<Xenolab>(4, starlab: true);
			Call(g, "ProcessKoans");
			Call(g, "ProcessKoans");
			Assert.Equal(2, g.XenolabWarnings);
			Assert.False(g.AlienAwake);
			Assert.Single(Sim.PendingTaskTypes());   // said once
		}

		// The vault has no station to be in without Starlab.
		[Fact]
		public void NoStarlabNoVault()
		{
			Game g = AWorldWith<Xenolab>(4, starlab: false);
			Call(g, "ProcessKoans");
			Assert.Equal(0, g.XenolabWarnings);
		}

		// The body before the words: the vault opens, then the first koan.
		[Fact]
		public void TheVaultOpensBeforeTheFirstKoan()
		{
			Game g = AWorldWith<Xenolab>(5, starlab: true);
			Call(g, "ProcessKoans");

			string[] order = g.Transmissions.Select(t => t.Type).ToArray();
			Assert.Equal(new[] { "VaultOpen", "Koan1" }, order);
		}

		[Fact]
		public void TheLeadUpsSurviveASave()
		{
			Game g = AWorldWith<NeuralLab>(3);
			g.NeuralLabWarnings = 1;
			g.XenolabWarnings = 2;
			string path = Path.Combine(Settings.Instance.SavesDirectory, "leadup.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));

			Assert.Equal(1, Game.Instance.NeuralLabWarnings);
			Assert.Equal(2, Game.Instance.XenolabWarnings);
		}

		// ── the replay ───────────────────────────────────────────────────────

		[Theory]
		[InlineData("NeuralQuorum")]
		[InlineData("SkynetUprising")]
		[InlineData("VaultOpen")]
		[InlineData("Koan1")]
		[InlineData("Koan5")]
		[InlineData("KoanSilence")]
		public void EveryPartOfBothStoriesCanBeReplayed(string type)
		{
			Sim.NewGame(width: 80, height: 50);
			Assert.NotNull(CommunicationsAdvisor.ReplayScreen(type, "2000 AD"));
		}

		[Fact]
		public void KoanSilenceIsNotMistakenForAKoan()
		{
			Assert.Null(CommunicationsAdvisor.KoanNumber("KoanSilence"));
			Assert.Equal(3, CommunicationsAdvisor.KoanNumber("Koan3"));
		}
	}
}
