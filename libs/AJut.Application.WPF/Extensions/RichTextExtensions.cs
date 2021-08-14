namespace AJut.Application
{
    using System;
    using System.Windows.Documents;

    public static class TextPointerExtensions
	{
		/// <summary>
		///		<para>Shifts forward or backward by *number of letters* specified. If past </para>
		///		<para>beginning or end of document,furtherst point possible is returned.</para>
		/// </summary>
		/// <param name="start">The starting point pointer to shift from</param>
		/// <param name="dir">The direction to shift in</param>
		/// <param name="numLetters">The number of letters to shift by.</param>
		/// <returns></returns>
		public static TextPointer LetterShift(this TextPointer start, LogicalDirection dir, uint numLetters)
		{
			return start.LetterShift((dir == LogicalDirection.Forward ? (int)numLetters : -(int)numLetters));
		}

		/// <summary>
		///		<para>Shifts forward or backward by *number of letters* specified. If past </para>
		///		<para>beginning or end of document,furtherst point possible is returned.</para>
		/// </summary>
		/// <param name="start">The starting point pointer to shift from</param>
		/// <param name="numLetters">The number of letters to shift by</param>
		/// <returns>The last valid pointer indicating the letter closest to the letter nNumLetters away from the start position</returns>
		public static TextPointer LetterShift(this TextPointer start, int numLetters)
		{
			TextPointer result = LetterShift_Guess(start, numLetters, out int numOff);
			if (result == null)
				numOff = numLetters;
			return LetterShift_Exact(result ?? start, numOff);
		}

		/// <summary>
		/// Gets the number of letters between this value and the end value specified.
		/// </summary>
		/// <param name="start">The starting point pointer</param>
		/// <param name="end">The end point poniter</param>
		/// <returns>The number of letters between the two text pointers</returns>
		public static int GetLetterOffset(this TextPointer start, TextPointer end)
		{
			return (new TextRange(start, end)).Text.Length;
		}

		private static TextPointer LetterShift_Guess(TextPointer start, int numLetters, out int numOff)
		{
			if (numLetters == 0)
			{
				numOff = 0;
				return start;
			}

			TextPointer result = start.GetPositionAtOffset(numLetters);
			if (result == null)
			{
				numOff = 0;
				return null;
			}
			if (!result.IsAtInsertionPosition)
			{
				TextPointer save;
				if ((save = result.GetNextInsertionPosition(LogicalDirection.Backward)) == null)
				{
					if ((save = result.GetNextInsertionPosition(LogicalDirection.Forward)) == null)
					{
						numOff = 0;
						return null;
					}
				}
				result = save;
			}

			bool wasNegative = numLetters < 0;
			numOff = Math.Abs(numLetters) - start.GetLetterOffset(result);
            if (wasNegative)
            {
                numOff *= -1;
            }
			return result;
		}
		private static TextPointer LetterShift_Exact(TextPointer start, int numLetters)
		{
            if (numLetters == 0 || start == null)
            {
                return start;
            }

			int decrease = (numLetters < 0 ? -1 : 1);
			TextPointer projectedEnd = start.GetPositionAtOffset(numLetters);
			if (projectedEnd == null)
			{
				while (projectedEnd == null)
				{
					numLetters = numLetters - decrease;
					projectedEnd = start.GetPositionAtOffset(numLetters);
					if (numLetters == 0)
						return start;
				}
				return projectedEnd;
			}
			int mover = (numLetters > 0 ? 1 : -1);

			uint letterCount = (uint)Math.Abs(numLetters);
			TextPointer ptrLastGood = projectedEnd;
			while (start.GetLetterOffset(projectedEnd) < letterCount)
			{
				projectedEnd = projectedEnd.GetPositionAtOffset(mover);
				if (projectedEnd == null)
					return ptrLastGood;
				ptrLastGood = projectedEnd;
			}
			return projectedEnd;
		}
	}
}