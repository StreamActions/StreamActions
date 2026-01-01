/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using FluentScheduler;
using System;

namespace StreamActions
{
    /// <summary>
    /// Schedules methods to run on an interval, or as a one-off after a specified delay.
    /// </summary>
    public static class Scheduler
    {
        #region Public Events

        /// <summary>
        /// Event raised when a job ends.
        /// </summary>
        public static event EventHandler<JobEndInfo> OnJobEnd;

        /// <summary>
        /// Event raised when an exception occurs in a job.
        /// </summary>
        public static event EventHandler<JobExceptionInfo> OnJobException;

        /// <summary>
        /// Event raised when a job starts.
        /// </summary>
        public static event EventHandler<JobStartInfo> OnJobStart;

        #endregion Public Events

        #region Public Methods

        /// <summary>
        /// Adds a job to the scheduler.
        /// </summary>
        /// <param name="action">The method to call when the time has elapsed.</param>
        /// <param name="schedule">The schedule defining when the job runs.</param>
        public static void AddJob(Action action, Action<Schedule> schedule) => JobManager.AddJob(action, schedule);

        /// <summary>
        /// Cancels and removes a scheduled job.
        /// </summary>
        /// <param name="name"></param>
        public static void RemoveJob(string name) => JobManager.RemoveJob(name);

        #endregion Public Methods

        #region Internal Methods

        /// <summary>
        /// Initializes the <see cref="JobManager"/>.
        /// </summary>
        internal static void Initialize()
        {
            JobManager.UseUtcTime();
            JobManager.Initialize(new Registry());
            JobManager.JobEnd += JobManager_JobEnd;
            JobManager.JobException += JobManager_JobException;
            JobManager.JobStart += JobManager_JobStart;
        }

        /// <summary>
        /// Shuts down the <see cref="JobManager"/> and cancels all pending jobs.
        /// </summary>
        internal static void Shutdown() => JobManager.Stop();

        #endregion Internal Methods

        #region Private Methods

        /// <summary>
        /// Passthrough event handler for <see cref="OnJobEnd"/>.
        /// </summary>
        /// <param name="obj">The <see cref="JobEndInfo"/>.</param>
        private static void JobManager_JobEnd(JobEndInfo obj) => OnJobEnd?.Invoke(null, obj);

        /// <summary>
        /// Passthrough event handler for <see cref="OnJobException"/>.
        /// </summary>
        /// <param name="obj">The <see cref="JobExceptionInfo"/>.</param>
        private static void JobManager_JobException(JobExceptionInfo obj) => OnJobException?.Invoke(null, obj);

        /// <summary>
        /// Passthrough event handler for <see cref="OnJobStart"/>.
        /// </summary>
        /// <param name="obj">The <see cref="JobStartInfo"/>.</param>
        private static void JobManager_JobStart(JobStartInfo obj) => OnJobStart?.Invoke(null, obj);

        #endregion Private Methods
    }
}
