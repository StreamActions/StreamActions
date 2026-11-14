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

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props StreamActions.sln LICENSE ./
COPY StreamActions.Common/StreamActions.Common.csproj StreamActions.Common/
COPY StreamActions.Core/StreamActions.Core.csproj StreamActions.Core/
COPY StreamActions.Database/StreamActions.Database.csproj StreamActions.Database/
COPY StreamActions.Web/StreamActions.Web.csproj StreamActions.Web/
COPY StreamActions.Daemon/StreamActions.Daemon.csproj StreamActions.Daemon/

RUN dotnet restore StreamActions.Daemon/StreamActions.Daemon.csproj

COPY StreamActions.Common/ StreamActions.Common/
COPY StreamActions.Core/ StreamActions.Core/
COPY StreamActions.Database/ StreamActions.Database/
COPY StreamActions.Web/ StreamActions.Web/
COPY StreamActions.Daemon/ StreamActions.Daemon/

RUN dotnet publish StreamActions.Daemon/StreamActions.Daemon.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /etc/streamactions /var/lib/streamactions/plugins /var/lib/streamactions/backups /var/log/streamactions \
    && chown -R $APP_UID:$APP_UID /etc/streamactions /var/lib/streamactions /var/log/streamactions

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

VOLUME ["/etc/streamactions", "/var/lib/streamactions", "/var/log/streamactions"]

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "StreamActions.Daemon.dll", "--config-dir=/etc/streamactions", "--data-dir=/var/lib/streamactions", "--log-dir=/var/log/streamactions", "--plugins-dir=/var/lib/streamactions/plugins", "--backup-dir=/var/lib/streamactions/backups"]

