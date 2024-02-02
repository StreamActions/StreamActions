/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace StreamActions.Pages
{
    [AllowAnonymous]
    public class PrivacyModel : PageModel
    {
        #region Public Constructors

        public PrivacyModel(ILogger<PrivacyModel> logger) => this._logger = logger;

        #endregion Public Constructors

        #region Public Methods

        public void OnGet()
        {
        }

        #endregion Public Methods

        #region Private Fields

        private readonly ILogger<PrivacyModel> _logger;

        #endregion Private Fields
    }
}
