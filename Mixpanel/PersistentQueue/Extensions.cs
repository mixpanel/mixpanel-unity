using System.Collections.Generic;

namespace mixpanel.queue
{
	public static class Extensions
	{
		/// <summary>
		/// Return value for key if present, otherwise return default.
		/// No new keys or values will be added to the dictionary.
		/// </summary>
		public static T GetValueOrDefault<T, TK>(this IDictionary<TK, T> self, TK key) => self.TryGetValue(key, out T value) == false ? default : value;
	}
}