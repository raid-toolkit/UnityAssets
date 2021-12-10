using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityAssets
{
	public static class StringExtensions
	{
		public static string FixedLength(this string input, int length)
		{
			if (input.Length == length)
				return input;

			return input.Length < length ? input.PadRight(length, ' ') : input.Substring(0, length) + (char)0x2026;
		}
	}
}
