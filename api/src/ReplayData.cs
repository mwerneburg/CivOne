// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;

namespace CivOne
{
	/// <summary>
	/// Base class for game events recorded in the replay log. Each subclass captures one
	/// notable event with the game turn it occurred on. The full log drives the end-game
	/// replay sequence shown after a win or loss.
	/// </summary>
	public abstract class ReplayData
	{
		public class CityBuilt : ReplayData
		{
			public readonly byte OwnerId;
			public readonly int CityId, CityNameId, X, Y;

			public CityBuilt(int turn, byte ownerId, int cityId, int cityNameId, int x, int y) : base(turn)
			{
				OwnerId = ownerId;
				CityId = cityId;
				CityNameId = cityNameId;
				X = x;
				Y = y;
			}
		}

		public class CityCaptured : ReplayData
		{
			public readonly int CityId, CityNameId, X, Y;
			public readonly byte NewOwnerId;

			public CityCaptured(int turn, int cityId, int cityNameId, int x, int y, byte newOwnerId) : base(turn)
			{
				CityId = cityId;
				CityNameId = cityNameId;
				X = x;
				Y = y;
				NewOwnerId = newOwnerId;
			}
		}

		public class CityDestroyed : ReplayData
		{
			public readonly int CityId, CityNameId, X, Y;
			public readonly byte OwnerId; // owner at the time of destruction

			public CityDestroyed(int turn, int cityId, int cityNameId, int x, int y, byte ownerId = 0) : base(turn)
			{
				CityId = cityId;
				CityNameId = cityNameId;
				X = x;
				Y = y;
				OwnerId = ownerId;
			}
		}

		public class CivilizationDestroyed : ReplayData
		{
			public readonly int DestroyedId, DestroyedById;

			public CivilizationDestroyed(int turn, int destroyedId, int destroyedById) : base(turn)
			{
				DestroyedId = destroyedId;
				DestroyedById = destroyedById;
			}
		}

		public class WonderBuilt : ReplayData
		{
			public readonly byte OwnerId;
			public readonly string WonderName;
			public readonly int X, Y;

			public WonderBuilt(int turn, byte ownerId, string wonderName, int x, int y) : base(turn)
			{
				OwnerId = ownerId;
				WonderName = wonderName;
				X = x;
				Y = y;
			}
		}

		public class TechDiscovered : ReplayData
		{
			public readonly byte OwnerId;
			public readonly string TechName;

			public TechDiscovered(int turn, byte ownerId, string techName) : base(turn)
			{
				OwnerId = ownerId;
				TechName = techName;
			}
		}

		public class UnitBuilt : ReplayData
		{
			public readonly byte OwnerId;
			public readonly string UnitName;

			public UnitBuilt(int turn, byte ownerId, string unitName) : base(turn)
			{
				OwnerId = ownerId;
				UnitName = unitName;
			}
		}

		public class BuildingBuilt : ReplayData
		{
			public readonly byte OwnerId;
			public readonly string BuildingName;

			public BuildingBuilt(int turn, byte ownerId, string buildingName) : base(turn)
			{
				OwnerId = ownerId;
				BuildingName = buildingName;
			}
		}

		public int Turn { get; private set; }

		protected ReplayData(int turn)
		{
			Turn = turn;
		}
	}
}
