using System;
using System.Collections.Generic;

namespace StreamActions
{
    public static class StreamActionsTypeExtensions
    {
        #region Public Methods

        /// <summary>
        /// Determines if the string at the specified index of the <c>List&lt;string></c> is <c>null</c> or an empty string (""), or if the index is out of range.
        /// </summary>
        /// <param name="source">The <c>List&lt;string></c> to check.</param>
        /// <param name="index">The index of the desired entry in the <c>List&lt;string></c>.</param>
        /// <returns><c>true</c> if the string at the specified index is <c>null</c>, empty, or the index is out of range.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        public static bool IsNullEmptyOrOutOfRange(this List<string> source, int index)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            try
            {
                return string.IsNullOrEmpty(source[index]);
            }
            catch (IndexOutOfRangeException)
            {
                return true;
            }
        }

        /// <summary>
        /// Determines if the string at the specified index of the <c>List&lt;string></c> is <c>null</c>, an empty string (""), or only consists of white-space characters, or if the index is out of range.
        /// </summary>
        /// <param name="source">The <c>List&lt;string></c> to check.</param>
        /// <param name="index">The index of the desired entry in the <c>List&lt;string></c>.</param>
        /// <returns><c>true</c> if the string at the specified index is <c>null</c>, empty, contains only white-space characters, or the index is out of range.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        public static bool IsNullWhiteSpaceOrOutOfRange(this List<string> source, int index)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            try
            {
                return string.IsNullOrWhiteSpace(source[index]);
            }
            catch (IndexOutOfRangeException)
            {
                return true;
            }
        }

        #endregion Public Methods
    }
}