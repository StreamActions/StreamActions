﻿/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace StreamActions.Common
{
    public static class StreamActionsTypeExtensions
    {
        #region Public Methods

        /// <summary>
        /// Returns the specified element, or <paramref name="defVal"/> if <paramref name="index"/> is out of range.
        /// </summary>
        /// <typeparam name="T">The type stored in <paramref name="source"/></typeparam>
        /// <param name="source">A List of <typeparamref name="T"/> values.</param>
        /// <param name="index">The index to attempt to retrieve.</param>
        /// <param name="defVal">The default value to return, if <paramref name="index"/> is out of range.</param>
        /// <returns>The value at <paramref name="index"/>; <paramref name="defVal"/> if index is out of range.</returns>
        public static T? GetElementAtOrDefault<T>(this List<T> source, int index, T? defVal = default)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return source.Count > index ? source[index] : defVal;
        }

        /// <summary>
        /// Invokes an <see cref="EventHandler{TEventArgs}"/> on a background Task thread.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of <paramref name="e"/>.</typeparam>
        /// <param name="eventHandler">The <see cref="EventHandler{TEventArgs}"/> to invoke.</param>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the event data.</param>
        /// <returns></returns>
        public static Task InvokeAsync<TEventArgs>(this EventHandler<TEventArgs> eventHandler, object? sender, TEventArgs e) => Task.Run(() => eventHandler?.Invoke(sender, e));

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
        /// Determines if the string at the specified index of the <c>List&lt;string&rt;</c> is <c>null</c>, an empty string (""), or only consists of white-space characters, or if the index is out of range.
        /// </summary>
        /// <param name="source">The <c>List&lt;string&rt;</c> to check.</param>
        /// <param name="index">The index of the desired entry in the <c>List&lt;string&rt;</c>.</param>
        /// <returns><c>true</c> if the string at the specified index is <c>null</c>, empty, contains only white-space characters, or the index is out of range.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        public static bool IsNullWhiteSpaceOrOutOfRange(this List<string?> source, int index)
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

        /// <summary>
        /// Waits for a <see cref="CancellationToken"/> in a background Task thread.
        /// </summary>
        /// <param name="token">The <see cref="CancellationToken"/> to wait for.</param>
        /// <returns>A Task that can be awaited.</returns>
        public static Task WaitAsync(this CancellationToken token) => Task.Run(() => _ = token.WaitHandle.WaitOne());

        #endregion Public Methods
    }
}