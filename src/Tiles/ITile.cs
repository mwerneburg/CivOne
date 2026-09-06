// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Drawing;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tiles
{
	public interface ITile : ICivilopedia
	{
		int X { get; }
		int Y { get; }
		Terrain Type { get; }
		bool Special { get; }
		byte ContinentId { get; set; }
		// Reachability id for SEA units, the water counterpart of ContinentId. See
		// Map.CalculateContinentSize for why cities are part of the water fill.
		byte OceanId { get; set; }
		byte LandValue { get; set; }
		byte LandScore { get; }
		byte Movement { get; }
		byte Defense { get; }
		sbyte Food { get; }
		sbyte Shield { get; }
		sbyte Trade { get; }	
		sbyte IrrigationFoodBonus { get; }
		byte IrrigationCost { get; }
		sbyte MiningShieldBonus { get; }
		byte MiningCost { get; }
		byte Borders { get; }
		bool Road { get; set; }
		bool RailRoad { get; set; }
		bool TransportTube { get; set; }
		// Who laid this sea tube. BaseTile.TubeUnowned when nobody has claimed it — which
		// covers every land tube and every tube in a save written before claims existed.
		byte TubeOwner { get; set; }
		// Any surface link, whatever tier — see BaseTile.HasTransportLink for why the three
		// properties above cannot answer that question.
		bool HasTransportLink { get; }

		// Terracing (Hills) and moisture farming (Desert): two ways to get food out of ground
		// that irrigation cannot reach, because it has no fresh water beside it.
		bool Terrace { get; set; }
		bool MoistureFarm { get; set; }
		bool Irrigation { get; set; }
		bool Fortress { get; set; }
		bool Mine { get; set; }
		bool Hut { get; set; }
		bool Pollution { get; set; }
		byte Visited { get; }
		void Visit(byte owner);
		bool IsOcean { get; }
		City City { get; }
		IUnit[] Units { get; }
		ITile this[int relativeX, int relativeY] { get; }
	}
}