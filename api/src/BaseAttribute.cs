// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;

namespace CivOne
{
	/// <summary>
	/// Base class for all plugin modification attributes. Subclasses are applied as C# attributes
	/// to <see cref="IModification"/> implementations to declare which game properties to override.
	/// The <see cref="Valid"/> flag gates all value access: out-of-range or wrong-type arguments
	/// are silently ignored rather than throwing, so the game stays stable even with malformed plugins.
	/// </summary>
	public abstract class BaseAttribute : Attribute
	{
		private readonly object _value;

		internal T GetValue<T>() => Valid ? (T)_value : default(T);

		/// <summary>
		/// True if the constructor argument was the correct type and passed the optional range check.
		/// When false, <see cref="GetValue{T}"/> returns <c>default(T)</c> and the modification
		/// has no effect.
		/// </summary>
		public bool Valid { get; }

		internal BaseAttribute(Type type, object value, Func<object, bool> checkValue = null)
		{
			_value = value;
			Valid = (value.GetType() == type) && (checkValue is null || checkValue(value));
		}
	}
}