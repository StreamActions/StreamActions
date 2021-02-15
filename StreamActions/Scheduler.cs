/*
 * Copyright © 2019-2021 StreamActions Team
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