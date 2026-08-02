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
	internal class Incan : BaseCivilization<Pachacuti>
	{
		public Incan() : base(Civilization.Inca, "Incan", "Inca")
		{
			StartX = 20;
			StartY = 36;
			CityNames = new string[]
			{
				"Cuzco",
				"Machu Picchu",
				"Tiwanaku",
				"Quito",
				"Lima",
				"Arequipa",
				"Chan Chan",
				"Cajamarca",
				"Huanuco",
				"Potosi",
				"Vilcas",
				"Pachacamac",
				"Ollantaytambo",
				"Tambo Colorado",
				"Chinchero",
				"Choquequirao",
				"Sacsayhuaman",
				"Pisac",
				"Moray",
				"Maras",
				"Urubamba",
				"Andahuaylas",
				"Ayacucho",
				"Huancayo",
				"Trujillo",
				"Chiclayo",
				"Piura",
				"Tumbes",
				"Loja",
				"Riobamba",
				"Cuenca",
				"Ibarra",
				"Latacunga",
				"Cochabamba",
				"La Paz",
				"Oruro",
				"Sucre",
				"Copacabana",
				"Nazca",
				"Paracas"
			};
		}
	}
}
