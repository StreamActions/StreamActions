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
Offline unit tests for TwitchOAuthParser.
"""

from pathlib import Path
import sys
import unittest
from unittest.mock import MagicMock, patch

_TWITCH_PARSERS_DIR = str(Path(__file__).resolve().parent.parent / "parsers" / "twitch")
if _TWITCH_PARSERS_DIR not in sys.path:
    sys.path.insert(0, _TWITCH_PARSERS_DIR)

from TwitchOAuthParser import TwitchOAuthParser


SAMPLE_OAUTH_MARKDOWN = """\
# Getting OAuth Access Tokens

## Implicit Grant Flow

| Parameter | Required? | Type | Description |
| --- | --- | --- | --- |
| client\\_id | Yes | String | Client ID. |

## Client Credentials Grant Flow

To get an access token, send an HTTP POST request.

| Parameter | Required? | Type | Description |
| --- | --- | --- | --- |
| client\\_id | Yes | String | Your app's [registered](https://dev.twitch.tv/docs/authentication/register-app.md) client ID. |
| client\\_secret | Yes | String | Your app's registered client secret. |
| grant\\_type | Yes | String | Must be set to `client_credentials`. |

## PKCE-enhanced Authorization Code Grant Flow

### Create a code verifier and code challenge

Create a code verifier and challenge first.

### Get the user to authorize your app (PKCE)

| Parameter | Required? | Type | Description |
| --- | --- | --- | --- |
| client\\_id | Yes | String | Your app's registered client ID. |
| code\\_challenge | Yes | String | Base64url SHA-256 hash of your generated code verifier. |
| code\\_challenge\\_method | Yes | String | Must be set to `S256`. |
| force\\_verify | No | Boolean | Set to **true** to force re-authorization. |
| redirect\\_uri | Yes | URI | Your app's registered redirect URI. |
| response\\_type | Yes | String | Must be set to `code`. |
| scope | Yes | String | Space-delimited list of scopes. |
| state | No | String | CSRF state string. |

### Use the authorization code to get a token (PKCE)

| Parameter | Required? | Type | Description |
| --- | --- | --- | --- |
| client\\_id | Yes | String | Your app's registered Client ID. |
| client\\_secret | Confidential apps only | String | Your app's registered Client Secret. **Do not include this** if your app's Client Type is Public. |
| code | Yes | String | Authorization code from `/authorize`. |
| code\\_verifier | Yes | String | The code verifier you created in the first step. |
| grant\\_type | Yes | String | Must be set to `authorization_code`. |
| redirect\\_uri | Yes | URI | Your app's registered redirect URI. |

## Authorization Code Grant Flow

### Get the user to authorize your app

| Parameter | Required? | Type | Description |
| --- | --- | --- | --- |
| client\\_id | Yes | String | Your app's registered client ID. |
| response\\_type | Yes | String | Must be set to `code`. |

### Use the authorization code to get a token

| Parameter | Required? | Type | Description |
| --- | --- | --- | --- |
| client\\_id | Yes | String | Your app's registered client ID. |
| client\\_secret | Yes | String | Your app's registered client secret. |
| code | Yes | String | Authorization code. |
| grant\\_type | Yes | String | Must be set to `authorization_code`. |
| redirect\\_uri | Yes | URI | Your app's registered redirect URI. |

## Device Code Grant Flow

### Starting the DCF Flow for your user

| Parameter | Required? | Type | Description |
| --- | --- | --- | --- |
| client\\_id | Yes | String | Client ID. |
"""


class TwitchOAuthParserTests(unittest.TestCase):
    """
    Tests for TwitchOAuthParser Markdown parsing and URL resolution.
    """

    def setUp(self) -> None:
        """
        Create a fresh TwitchOAuthParser instance for each test.
        """
        self.parser = TwitchOAuthParser()

    def test_parse_captures_client_credentials_pkce_and_auth_code_sections(self) -> None:
        """
        Verify Client Credentials Grant Flow, both PKCE sections, and standard Authorization Code sections are parsed.
        """
        result = self.parser.parse(SAMPLE_OAUTH_MARKDOWN)

        expected_order = [
            "Client Credentials Grant Flow",
            "Get the user to authorize your app (PKCE)",
            "Use the authorization code to get a token (PKCE)",
            "Get the user to authorize your app",
            "Use the authorization code to get a token",
        ]
        self.assertEqual(
            [entry["endpoint"] for entry in result["toc"]["Authorize OAuth"]],
            expected_order,
        )
        self.assertEqual(list(result["endpoints"].keys()), expected_order)

        expected_shapes = [
            ("Client Credentials Grant Flow", "#client-credentials-grant-flow", "requestBody", ["client_id", "client_secret", "grant_type"]),
            (
                "Get the user to authorize your app (PKCE)",
                "#get-the-user-to-authorize-your-app-pkce",
                "requestQuery",
                ["client_id", "code_challenge", "code_challenge_method", "force_verify", "redirect_uri", "response_type", "scope", "state"],
            ),
            (
                "Use the authorization code to get a token (PKCE)",
                "#use-the-authorization-code-to-get-a-token-pkce",
                "requestBody",
                ["client_id", "client_secret", "code", "code_verifier", "grant_type", "redirect_uri"],
            ),
            ("Get the user to authorize your app", "#get-the-user-to-authorize-your-app", "requestQuery", ["client_id", "response_type"]),
            ("Use the authorization code to get a token", "#use-the-authorization-code-to-get-a-token", "requestBody", ["client_id", "client_secret", "code", "grant_type", "redirect_uri"]),
        ]

        for name, slug, field_key, expected_params in expected_shapes:
            with self.subTest(endpoint=name):
                endpoint = result["endpoints"][name]
                self.assertEqual(endpoint["slug"], slug)
                self.assertIn(field_key, endpoint)
                self.assertEqual([row["parameter"] for row in endpoint[field_key]], expected_params)

        pkce_token_params = {
            row["parameter"]: row
            for row in result["endpoints"]["Use the authorization code to get a token (PKCE)"]["requestBody"]
        }
        self.assertEqual(pkce_token_params["client_secret"]["required"], "Confidential apps only")
        self.assertEqual(
            pkce_token_params["code_verifier"]["description"],
            "The code verifier you created in the first step.",
        )

    def test_parse_handles_heading_casing_variants(self) -> None:
        """
        Verify target headings are matched regardless of title-casing changes.
        """
        headings = [
            ("Client Credentials Grant Flow", "#client-credentials-grant-flow"),
            ("Client credentials grant flow", "#client-credentials-grant-flow"),
        ]
        for heading, expected_slug in headings:
            with self.subTest(heading=heading):
                md = (
                    f"## {heading}\n\n"
                    "| Parameter | Required? | Type | Description |\n"
                    "| --- | --- | --- | --- |\n"
                    "| grant\\_type | Yes | String | Must be set to `client_credentials`. |\n"
                )
                result = self.parser.parse(md)
                self.assertEqual(result["toc"]["Authorize OAuth"], [{"endpoint": heading}])
                self.assertEqual(result["endpoints"][heading]["slug"], expected_slug)
                self.assertEqual(
                    result["endpoints"][heading]["requestBody"],
                    [
                        {
                            "parameter": "grant_type",
                            "required": "Yes",
                            "type": "String",
                            "description": "Must be set to client_credentials.",
                        }
                    ],
                )

    def test_parse_ignores_code_blocks_and_incomplete_table_rows(self) -> None:
        """
        Verify headings inside code blocks are ignored and rows with fewer than four columns are skipped.
        """
        md = (
            "```markdown\n"
            "## Client Credentials Grant Flow\n"
            "```\n\n"
            "### Get the user to authorize your app (PKCE)\n\n"
            "| Parameter | Type | Description |\n"
            "| --- | --- | --- |\n"
            "| client\\_id | String | Missing required column. |\n"
        )
        result = self.parser.parse(md)
        self.assertEqual(
            result["toc"]["Authorize OAuth"],
            [{"endpoint": "Get the user to authorize your app (PKCE)"}],
        )
        self.assertEqual(
            result["endpoints"]["Get the user to authorize your app (PKCE)"],
            {
                "slug": "#get-the-user-to-authorize-your-app-pkce",
                "requestQuery": [],
            },
        )

    def test_parse_from_url_resolves_markdown_url_offline(self) -> None:
        """
        Verify parseFromUrl fetches the .md URL with the expected User-Agent and parses the response.
        """
        mock_response = MagicMock()
        mock_response.status_code = 200
        mock_response.text = SAMPLE_OAUTH_MARKDOWN

        with patch("BaseParser.requests.get", return_value=mock_response) as mock_get:
            result = self.parser.parseFromUrl(
                "https://dev.twitch.tv/docs/authentication/getting-tokens-oauth"
            )

        mock_get.assert_called_once_with(
            "https://dev.twitch.tv/docs/authentication/getting-tokens-oauth.md",
            headers={"User-Agent": "streamactions.diff.parser/1"},
        )
        self.assertIn("Client Credentials Grant Flow", result["endpoints"])
        self.assertIn("Get the user to authorize your app (PKCE)", result["endpoints"])
        self.assertIn("Use the authorization code to get a token (PKCE)", result["endpoints"])
