// CivOne tests
//
// Tasks.Orders.Error() raised a popup for the PLAYER whenever an order failed, whoever's
// unit it was. The AI routes FoundCity, BuildRoad, BuildIrrigation and BuildMines through
// Orders, so every failed AI settler order interrupted the human: 178,252 PopupMessage
// samples (322s) in one 750-turn game, second only to the settler AI itself.
//
// Every other error site in the codebase already guards on ownership — ZOC, NOIRR, AMPHIB,
// TRIREME, Longboat, the air units. This one was missed because it sits one level of
// indirection from the message, behind a private helper.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Units;

namespace CivOne.Tests
{
	public class OrderErrorOwnershipTests
	{
		private static (Game, Player) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			return (Game.Instance, Game.Instance.HumanPlayer);
		}

		// The failure the AI actually hits: a settler ordered onto a city of size 10+ cannot
		// join it (Orders.CreateCity -> Error("ADDCITY")). With hundreds of AI settlers
		// walking into full cities all game, this is the one that fired 178,252 times.
		private static int PopupsAfterFailedJoin(Player owner)
		{
			Game g = Game.Instance;
			// Must be land: CreateCity bails out early on ocean without Aquatic Colonization,
			// which would skip the ADDCITY branch entirely and prove nothing.
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(40, 25, range: 4);
			City big = g.AddCity(owner, 0, 40, 25)!;
			big.Size = 12;                        // too large to join

			IUnit settler = g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(owner))!;
			settler.MovesLeft = settler.Move;
			Sim.ClearTasks();

			GameTask.Enqueue(Orders.FoundCity(settler as Settlers));
			for (int i = 0; i < 12 && GameTask.Any(); i++) GameTask.Update();

			return GameTask.Count<Message>()
			     + (Common.HasScreenType<CivOne.Screens.PopupMessage>() ? 1 : 0);
		}

		// An AI unit's failed order must not interrupt the player.
		[Fact]
		public void AnAIsFailedOrderRaisesNoPopup()
		{
			(Game g, Player human) = AWorld();
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != human);

			Assert.Equal(0, PopupsAfterFailedJoin(ai));
		}

		// ...but the player still hears about their own.
		[Fact]
		public void ThePlayersOwnFailedOrderStillReports()
		{
			(Game g, Player human) = AWorld();

			Assert.True(PopupsAfterFailedJoin(human) > 0,
				"the owner should still be told why their own order failed");
		}
	}
}
