// CivOne tests
//
// The city screen's "RATES" panel showed only the empire-wide tax/science/luxury sliders,
// so cycling a citizen through Entertainer -> Taxman -> Scientist changed nothing visible
// and the player could not tell what the specialist was doing. The panel now also prints
// each row's per-city output; this pins the model behaviour that display relies on.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class SpecialistOutputTests
	{
		// A specialist is worth 2 of its own kind, and nothing to the other two rows.
		[Fact]
		public void CyclingASpecialist_MovesExactlyOneOutput()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			p.Explore(20, 20, range: 5);
			City city = g.AddCity(p, 0, 20, 20)!;
			city.Size = 4;

			// Free a worker so there is a specialist to cycle.
			ITile? spare = city.ResourceTiles.FirstOrDefault(t => t.X != city.X || t.Y != city.Y);
			Assert.NotNull(spare);
			city.SetResourceTile(spare!);
			Assert.Contains(city.Citizens, c => (int)c >= 6);

			// Specialists start as Entertainers.
			short lux0 = city.Luxuries, tax0 = city.Taxes, sci0 = city.Science;

			// Four stops since the Artist was added: Entertainer -> Artist -> Taxman ->
			// Scientist -> Entertainer. The Artist is the one that moves NONE of these three,
			// because what it produces is culture — which is exactly why it was worth adding.
			int cult0 = city.CultureRate;

			city.ChangeSpecialist(0);   // -> Artist
			Assert.Equal(lux0 - 2, city.Luxuries);
			Assert.Equal(tax0,     city.Taxes);
			Assert.Equal(sci0,     city.Science);
			Assert.Equal(cult0 + City.ArtistCulture, city.CultureRate);

			city.ChangeSpecialist(0);   // -> Taxman
			Assert.Equal(lux0 - 2, city.Luxuries);
			Assert.Equal(tax0 + 2, city.Taxes);
			Assert.Equal(sci0,     city.Science);
			Assert.Equal(cult0,    city.CultureRate);

			city.ChangeSpecialist(0);   // -> Scientist
			Assert.Equal(lux0 - 2, city.Luxuries);
			Assert.Equal(tax0,     city.Taxes);
			Assert.Equal(sci0 + 2, city.Science);

			city.ChangeSpecialist(0);   // -> Entertainer again
			Assert.Equal(lux0, city.Luxuries);
			Assert.Equal(tax0, city.Taxes);
			Assert.Equal(sci0, city.Science);
		}
	}
}
