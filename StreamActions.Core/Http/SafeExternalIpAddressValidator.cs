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

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace StreamActions.Core.Http;

/// <summary>
/// Validates destination IP addresses and hostnames against SSRF-blocked private, loopback, link-local, metadata, and reserved ranges.
/// </summary>
public static class SafeExternalIpAddressValidator
{
    /// <summary>
    /// Determines whether the specified <see cref="IPAddress"/> is a globally routable external unicast address.
    /// </summary>
    /// <param name="address">The IP address to validate.</param>
    /// <returns><see langword="true"/> if the address is safe for external outbound requests; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowed(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> bytes = stackalloc byte[4];
            return address.TryWriteBytes(bytes, out int written)
                && (written == 4)
                && IsAllowedIPv4(BinaryPrimitives.ReadUInt32BigEndian(bytes));
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.ScopeId != 0)
            {
                return false;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                return IsAllowed(address.MapToIPv4());
            }

            Span<byte> bytes = stackalloc byte[16];
            if (!address.TryWriteBytes(bytes, out int written) || (written != 16))
            {
                return false;
            }

            // ::ffff:0:0/96 (IPv4-mapped IPv6) unwrapped and validated strictly as IPv4.
            if (IsAllZero(bytes[..10]) && (bytes[10] == 0xFF) && (bytes[11] == 0xFF))
            {
                return IsAllowedIPv4(BinaryPrimitives.ReadUInt32BigEndian(bytes[12..16]));
            }

            // ::/96 (IPv4-compatible IPv6, including :: and ::1).
            if (IsAllZero(bytes[..12]))
            {
                return false;
            }

            // Defense-in-depth checks for non-global IPv6 classes.
            if (address.IsIPv6LinkLocal
                || address.IsIPv6Multicast
                || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal
                || address.IsIPv6Teredo)
            {
                return false;
            }

            // fc00::/7 (Unique Local Addresses), fe80::/10 (Link-Local), ff00::/8 (Multicast).
            if (((bytes[0] & 0xFE) == 0xFC)
                || ((bytes[0] == 0xFE) && ((bytes[1] & 0xC0) == 0x80))
                || (bytes[0] == 0xFF))
            {
                return false;
            }

            // 64:ff9b::/96 and 64:ff9b:1::/48 (NAT64 well-known prefixes).
            if ((bytes[0] == 0x00) && (bytes[1] == 0x64) && (bytes[2] == 0xFF) && (bytes[3] == 0x9B))
            {
                if (IsAllZero(bytes[4..12]) || ((bytes[4] == 0x00) && (bytes[5] == 0x01)))
                {
                    return false;
                }
            }

            // Fail closed for anything outside IPv6 Global Unicast (2000::/3).
            if ((bytes[0] & 0xE0) != 0x20)
            {
                return false;
            }

            // 2001::/32 (Teredo).
            if ((bytes[0] == 0x20) && (bytes[1] == 0x01) && (bytes[2] == 0x00) && (bytes[3] == 0x00))
            {
                return false;
            }

            // 2001:2::/48 (Benchmarking).
            if ((bytes[0] == 0x20) && (bytes[1] == 0x01) && (bytes[2] == 0x00) && (bytes[3] == 0x02) && (bytes[4] == 0x00) && (bytes[5] == 0x00))
            {
                return false;
            }

            // 2001:10::/28 and 2001:20::/28 (ORCHID / ORCHIDv2).
            if ((bytes[0] == 0x20) && (bytes[1] == 0x01) && (bytes[2] == 0x00) && ((bytes[3] & 0xF0) is 0x10 or 0x20))
            {
                return false;
            }

            // 2001:db8::/32 (Documentation).
            if ((bytes[0] == 0x20) && (bytes[1] == 0x01) && (bytes[2] == 0x0D) && (bytes[3] == 0xB8))
            {
                return false;
            }

            // 2002::/16 (6to4, including any embedded IPv4 address).
            if ((bytes[0] == 0x20) && (bytes[1] == 0x02))
            {
                return false;
            }

            // 3fff::/20 (Documentation RFC 9637).
            return (bytes[0] != 0x3F) || (bytes[1] != 0xFF) || ((bytes[2] & 0xF0) != 0x00);
        }

        return false;
    }

    /// <summary>
    /// Determines whether a hostname refers to the loopback <c>localhost</c> domain.
    /// </summary>
    /// <param name="host">The hostname to check.</param>
    /// <returns><see langword="true"/> if the hostname is <c>localhost</c> or a subdomain of <c>.localhost</c>; otherwise, <see langword="false"/>.</returns>
    public static bool IsLocalhostHostName(string? host) =>
        HostPatternMatcher.TryNormalizeHost(host, out string normalized)
        && (string.Equals(normalized, "localhost", StringComparison.Ordinal)
            || normalized.EndsWith(".localhost", StringComparison.Ordinal));

    /// <summary>
    /// Evaluates a big-endian 32-bit IPv4 address against all blocked non-public CIDR ranges.
    /// </summary>
    /// <param name="value">The big-endian 32-bit IPv4 address.</param>
    /// <returns><see langword="true"/> if the IPv4 address is globally routable; otherwise, <see langword="false"/>.</returns>
    private static bool IsAllowedIPv4(uint value) =>
        !IsInCidr(value, 0x00000000u, 0xFF000000u)       // 0.0.0.0/8 (Unspecified / this network)
        && !IsInCidr(value, 0x0A000000u, 0xFF000000u)    // 10.0.0.0/8 (RFC 1918)
        && !IsInCidr(value, 0x64400000u, 0xFFC00000u)    // 100.64.0.0/10 (CGNAT)
        && !IsInCidr(value, 0x7F000000u, 0xFF000000u)    // 127.0.0.0/8 (Loopback)
        && !IsInCidr(value, 0xA9FE0000u, 0xFFFF0000u)    // 169.254.0.0/16 (Link-local / Cloud metadata)
        && !IsInCidr(value, 0xAC100000u, 0xFFF00000u)    // 172.16.0.0/12 (RFC 1918)
        && !IsInCidr(value, 0xC0000000u, 0xFFFFFF00u)    // 192.0.0.0/24 (IETF protocol assignments)
        && !IsInCidr(value, 0xC0000200u, 0xFFFFFF00u)    // 192.0.2.0/24 (TEST-NET-1 documentation)
        && !IsInCidr(value, 0xC0586300u, 0xFFFFFF00u)    // 192.88.99.0/24 (Deprecated 6to4 relay anycast)
        && !IsInCidr(value, 0xC0A80000u, 0xFFFF0000u)    // 192.168.0.0/16 (RFC 1918)
        && !IsInCidr(value, 0xC6120000u, 0xFFFE0000u)    // 198.18.0.0/15 (Benchmarking)
        && !IsInCidr(value, 0xC6336400u, 0xFFFFFF00u)    // 198.51.100.0/24 (TEST-NET-2 documentation)
        && !IsInCidr(value, 0xCB007100u, 0xFFFFFF00u)    // 203.0.113.0/24 (TEST-NET-3 documentation)
        && !IsInCidr(value, 0xE0000000u, 0xF0000000u)    // 224.0.0.0/4 (Multicast)
        && !IsInCidr(value, 0xF0000000u, 0xF0000000u);   // 240.0.0.0/4 (Reserved & 255.255.255.255 broadcast)

    /// <summary>
    /// Checks whether a 32-bit IPv4 value matches the specified masked CIDR network.
    /// </summary>
    /// <param name="value">The big-endian IPv4 value.</param>
    /// <param name="network">The masked network prefix.</param>
    /// <param name="mask">The subnet bitmask.</param>
    /// <returns><see langword="true"/> if inside the CIDR range; otherwise, <see langword="false"/>.</returns>
    private static bool IsInCidr(uint value, uint network, uint mask) => (value & mask) == network;

    /// <summary>
    /// Determines whether all bytes in a span are zero.
    /// </summary>
    /// <param name="span">The byte span to inspect.</param>
    /// <returns><see langword="true"/> if every byte is <c>0</c>; otherwise, <see langword="false"/>.</returns>
    private static bool IsAllZero(ReadOnlySpan<byte> span) => !span.ContainsAnyExcept((byte)0);
}

