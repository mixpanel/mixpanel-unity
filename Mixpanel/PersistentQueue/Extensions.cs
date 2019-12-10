using System.Collections.Generic;

namespace mixpanel.queue
{
	public static class Extensions
	{
		/// <summary>
		/// Return value for key if present, otherwise return default.
		/// No new keys or values will be added to the dictionary.
		/// </summary>
		public static T GetValueOrDefault<T, TK>(this IDictionary<TK, T> self, TK key)
		{
			T value;
			return self.TryGetValue(key, out value) == false ? default(T) : value;
		}
	}
}