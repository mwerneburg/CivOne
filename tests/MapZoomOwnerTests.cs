// CivOne tests
//
// The map read its zoom from Game.CurrentPlayer. On every AI turn — which is when world
// events play — the view snapped to the AI's default zoom, anchored at the top-left tile,
// so the player was "taken somewhere else", and snapped back when their turn returned.
// The zoom is the human's, whoever's turn it is.

using System.Linq;
using System.Reflection;
using CivOne.Screens.GamePlayPanels;

namespace CivOne.Tests
{
	public class MapZoomOwnerTests
	{
		private static void MakeAnAIsTurn()
		{
			Player ai = Game.Instance.Players.First(p => p != Game.Instance.HumanPlayer && !p.IsDestroyed());
			typeof(Game).GetField("_currentPlayer", BindingFlags.NonPublic | BindingFlags.Instance)!
				.SetValue(Game.Instance, (int)Game.Instance.PlayerNumber(ai));
			Assert.NotEqual(Game.Instance.HumanPlayer, Game.Instance.CurrentPlayer);
		}

		[Fact]
		public void AnAITurnKeepsTheHumansZoom()
		{
			Sim.NewGame(width: 80, height: 50);
			Game.Instance.HumanPlayer.MapZoomBasisPoints = 500;
			MakeAnAIsTurn();

			GameMap map = new GameMap();
			map.Resize(240, 192);

			Assert.Equal(8, map.TilePixelSize);
		}
	}
}
