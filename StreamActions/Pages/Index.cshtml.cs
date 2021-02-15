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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StreamActions.Pages
{
    public class IndexModel : PageModel
    {
        #region Public Constructors

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        #endregion Public Constructors

        #region Public Methods

        public void OnGet()
        {
        }

        #endregion Public Methods

        #region Private Fields

        private readonly ILogger<IndexModel> _logger;

        #endregion Private Fields
    }
}