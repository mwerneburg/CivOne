// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne
{
	/// <summary>
	/// The resolved value of a modification attribute. Each property on a modification base class
	/// (e.g. <see cref="Units.UnitModification.Name"/>) returns one of these so callers can
	/// distinguish "attribute present and valid" from "attribute absent or invalid" without null checks.
	/// </summary>
	public class AttributeValue<T>
	{
		/// <summary>True if the attribute was present on the class and its value passed validation.</summary>
		public bool HasValue { get; }
		/// <summary>The attribute value. Only meaningful when <see cref="HasValue"/> is true.</summary>
		public T Value { get; }

		internal static AttributeValue<T> Set(BaseAttribute attribute) => new AttributeValue<T>(attribute);

		private AttributeValue(BaseAttribute attribute)
		{
			if (!(HasValue = (attribute is not null && attribute.Valid))) return;
			Value = attribute.GetValue<T>();
		}
	}
}