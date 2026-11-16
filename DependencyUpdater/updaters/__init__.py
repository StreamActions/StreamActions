# coding=utf-8
#
# This file is part of StreamActions.
# Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
#
# StreamActions is free software: you can redistribute it and/or modify
# it under the terms of the GNU Affero General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# StreamActions is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU Affero General Public License for more details.
#
# You should have received a copy of the GNU Affero General Public License
# along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
#

"""
Per-ecosystem dependency updater modules for StreamActions.
"""

from updaters.BaseUpdater import (
    ALLOWED_ACTION_OWNERS,
    BaseUpdater,
    DependencyUpdate,
    EcosystemResult,
    SkippedUpdate,
    format_markdown_report,
    results_to_dict,
)
from updaters.DockerUpdater import DockerUpdater
from updaters.DotNetUpdater import DotNetUpdater
from updaters.GitHubActionsUpdater import GitHubActionsUpdater
from updaters.PythonUpdater import PythonUpdater

__all__ = [
    "ALLOWED_ACTION_OWNERS",
    "BaseUpdater",
    "DependencyUpdate",
    "DockerUpdater",
    "DotNetUpdater",
    "EcosystemResult",
    "GitHubActionsUpdater",
    "PythonUpdater",
    "SkippedUpdate",
    "format_markdown_report",
    "results_to_dict",
]

