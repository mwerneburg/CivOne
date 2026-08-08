// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;
using CivOne.Leaders;

namespace CivOne.Civilizations
{
	// Charlemagne's Franks stand in for the Germans and the French, who shared a player slot and a homeland; west-European civilisation descends from the same court.
	internal class Frank : BaseCivilization<Charlemagne>
	{
		public Frank() : base(Civilization.Franks, "Frank", "Franks")
		{
			// Legacy 80x50 fallback only. Earth maps place this civ from
			// Game.EarthCentroids, which is real latitude and longitude.
			StartX = 41;
			StartY = 11;
			CityNames = new string[]
			{
				"Aachen",
				"Paris",
				"Tours",
				"Metz",
				"Orleans",
				"Cologne",
				"Reims",
				"Worms",
				"Ingelheim",
				"Soissons",
				"Verdun",
				"Rouen",
				"Toulouse",
				"Lyon",
				"Mainz",
				"Trier",
				"Bourges",
				"Poitiers",
				"Strasbourg",
				"Frankfurt",
				"Nijmegen",
				"Regensburg",
				"Compiegne",
				"Quierzy",
				"Herstal",
				"Thionville",
				"Attigny",
				"Ponthion",
				"Nimwegen",
				"Chelles",
				"Laon",
				"Sens",
				"Auxerre",
				"Besancon",
				"Chalons",
				"Amiens",
				"Arras",
				"Cambrai",
				"Utrecht",
				"Salzburg",
			};
		}
	}
}
