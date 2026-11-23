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

using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using StreamActions.Web.Components;
using StreamActions.Web.Components.Pages;
using StreamActions.Web.Security;
using System.Globalization;
using System.Security.Claims;
using Xunit;

namespace StreamActions.Web.Tests;

public sealed class PermissionsUxAndDataTableTests
{
    #region DataTableGrid

    [Fact]
    [Trait("Member", "DataTableGrid")]
    public void DataTableGrid_NumberedPaginationAndInfiniteScroll_SlicesPagesResetsOnSearchAndSupportsVirtualize()
    {
        List<string> items = [.. Enumerable.Range(1, 60).Select(static i => $"Entry-{i:D2}")];

        using BunitContext context = new();
        IRenderedComponent<DataTableGrid<string>> grid = context.Render<DataTableGrid<string>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.PageSize, 25)
            .Add(p => p.FilterPredicate, static (item, q) => item.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Add(p => p.HeaderContent, builder => builder.AddMarkupContent(0, "<tr><th>Name</th></tr>"))
            .Add(p => p.RowTemplate, item => builder => builder.AddMarkupContent(0, $"<tr data-row='{item}'><td>{item}</td></tr>")));

        // Default PageSize = 25 -> Page 1 shows Entry-01..Entry-25.
        _ = grid.Find("[data-datatable-status='true']").TextContent.Should().Contain("Showing 1 to 25 of 60 entries");
        _ = grid.FindAll("tbody tr").Should().HaveCount(25);
        _ = grid.FindAll("[data-row='Entry-01']").Should().ContainSingle();
        _ = grid.FindAll("[data-row='Entry-26']").Should().BeEmpty();

        // Navigate to Page 2 -> Entry-26..Entry-50.
        grid.Find("[data-datatable-page-next='true']").Click();
        _ = grid.Find("[data-datatable-status='true']").TextContent.Should().Contain("Showing 26 to 50 of 60 entries");
        _ = grid.FindAll("[data-row='Entry-26']").Should().ContainSingle();
        _ = grid.FindAll("[data-row='Entry-01']").Should().BeEmpty();

        // Changing page size to 10 resets to Page 1 -> Entry-01..Entry-10.
        grid.Find("[data-datatable-page-size='true']").Change("10");
        _ = grid.Find("[data-datatable-status='true']").TextContent.Should().Contain("Showing 1 to 10 of 60 entries");
        _ = grid.FindAll("tbody tr").Should().HaveCount(10);

        // Navigate to Page 3, then enter a search query -> resets to Page 1 and shows filtered count.
        grid.Find("[data-datatable-page-number='3']").Click();
        _ = grid.Find("[data-datatable-status='true']").TextContent.Should().Contain("Showing 21 to 30 of 60 entries");

        grid.Find("[data-datatable-search='true']").Input("Entry-5");
        _ = grid.Find("[data-datatable-status='true']").TextContent
            .Should().Contain("Showing 1 to 10 of 10 entries")
            .And.Contain("(filtered from 60 total entries)");

        // Clear search and select PageSize = All (-1) -> switches to Infinite-Scroll (<Virtualize>) mode and hides pagination nav.
        grid.Find("[data-datatable-search='true']").Input(string.Empty);
        grid.Find("[data-datatable-page-size='true']").Change(PagedRequest.ShowAllPageSize.ToString(CultureInfo.InvariantCulture));

        _ = grid.Find("[data-datatable-infinite-scroll='true']").Should().NotBeNull();
        _ = grid.FindAll("[data-datatable-pagination='true']").Should().BeEmpty();
        _ = grid.Find("[data-datatable-status='true']").TextContent.Should().Contain("Showing all 60 matching entries (infinite scroll)");
    }

    [Fact]
    [Trait("Member", "DataTableGrid")]
    public void DataTableGrid_WithServerItemsProvider_InvokesProviderWithPagedRequest()
    {
        List<PagedRequest> receivedRequests = [];
        List<int> sourceNumbers = [.. Enumerable.Range(1, 35)];

        using BunitContext context = new();
        IRenderedComponent<DataTableGrid<int>> grid = context.Render<DataTableGrid<int>>(parameters => parameters
            .Add(p => p.PageSize, 10)
            .Add(p => p.ServerItemsProvider, (req, _) =>
            {
                receivedRequests.Add(req);
                PagedResult<int> result = PagedResult<int>.FromEnumerable(
                    sourceNumbers,
                    req,
                    static (item, q) => item.ToString(CultureInfo.InvariantCulture).Contains(q, StringComparison.OrdinalIgnoreCase));
                return ValueTask.FromResult(result);
            })
            .Add(p => p.RowTemplate, item => builder => builder.AddMarkupContent(0, $"<tr data-num='{item}'><td>{item}</td></tr>")));

        _ = receivedRequests.Should().ContainSingle();
        _ = receivedRequests[0].PageNumber.Should().Be(1);
        _ = receivedRequests[0].PageSize.Should().Be(10);
        _ = grid.FindAll("tbody tr").Should().HaveCount(10);

        grid.Find("[data-datatable-page-last='true']").Click();
        _ = receivedRequests.Last().PageNumber.Should().Be(4);
        _ = grid.Find("[data-datatable-status='true']").TextContent.Should().Contain("Showing 31 to 35 of 35 entries");
    }

    #endregion

    #region PermissionsThreeModeAndBulkSave

    [Fact]
    [Trait("Member", "Permissions")]
    public void Permissions_ThreeModeFlow_SupportsSingleGroupViewAndEditPreservesStagedEditsAcrossPagesAndPersistsBatchOnSave()
    {
        Guid wsId = Guid.NewGuid();
        Guid modsGroupId = Guid.NewGuid();
        Guid vipsGroupId = Guid.NewGuid();
        Workspace ws = new() { Id = wsId, OwnerUserId = Guid.NewGuid(), Name = "ThreeMode Channel", PrimaryPlatformId = "twitch" };

        TrackingPermissionStore permStore = new(ws);
        permStore.WorkspaceGroups.Add(new PermissionGroup { Id = modsGroupId, WorkspaceId = wsId, Name = "Moderators", MappedPlatformDesignation = "moderator" });
        permStore.WorkspaceGroups.Add(new PermissionGroup { Id = vipsGroupId, WorkspaceId = wsId, Name = "Custom VIPs" });

        using TestPermissionHarness harness = new(permStore, ws, isRoot: false);
        IRenderedComponent<Permissions> component = harness.Context.Render<Permissions>();

        // 1. Starts in ListMode: shows both groups, View/Edit buttons, and Delete only on custom group.
        _ = component.Find("[data-permissions-mode='List']").Should().NotBeNull();
        _ = component.FindAll($"[data-group-view='{modsGroupId}']").Should().ContainSingle();
        _ = component.FindAll($"[data-group-edit='{modsGroupId}']").Should().ContainSingle();
        _ = component.FindAll($"[data-group-delete='{modsGroupId}']").Should().BeEmpty();
        _ = component.FindAll($"[data-group-delete='{vipsGroupId}']").Should().ContainSingle();

        // 2. Click View on Custom VIPs -> transitions to ViewMode for only Custom VIPs.
        component.Find($"[data-group-view='{vipsGroupId}']").Click();
        _ = component.Find("[data-permissions-mode='View']").Should().NotBeNull();
        _ = component.Markup.Should().Contain("Viewing Group: Custom VIPs");
        _ = component.FindAll("select[data-permission-select]").Should().BeEmpty();

        // 3. Click Edit Permissions -> transitions to EditMode for only Custom VIPs.
        component.Find("[data-enter-edit-mode='true']").Click();
        _ = component.Find("[data-permissions-mode='Edit']").Should().NotBeNull();
        _ = component.Markup.Should().Contain("Editing Group: Custom VIPs");
        _ = component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count").Should().Be("0");

        // Switch page size to 10 so permissions span multiple pages.
        component.Find("[data-datatable-page-size='true']").Change("10");

        // Change a permission on Page 1 -> updates in-memory buffer without calling SetGrantAsync or SetGrantsBatchAsync.
        AngleSharp.Dom.IElement firstPageSelect = component.Find("select[data-permission-select]");
        string firstPagePermKey = firstPageSelect.GetAttribute("data-permission-select")!;
        firstPageSelect.Change(nameof(PermissionState.Allow));

        _ = permStore.SetGrantCallCount.Should().Be(0);
        _ = permStore.SetGrantsBatchCalls.Should().BeEmpty();
        _ = component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count").Should().Be("1");

        // Navigate to Page 2, stage another change, then return to Page 1 -> Page 1 staged edit is preserved!
        component.Find("[data-datatable-page-next='true']").Click();
        AngleSharp.Dom.IElement secondPageSelect = component.Find("select[data-permission-select]");
        string secondPagePermKey = secondPageSelect.GetAttribute("data-permission-select")!;
        secondPageSelect.Change(nameof(PermissionState.Deny));

        _ = component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count").Should().Be("2");

        component.Find("[data-datatable-page-prev='true']").Click();
        _ = component.Find($"select[data-permission-select='{firstPagePermKey}'] option[value='{PermissionState.Allow}']").HasAttribute("selected")
            .Should().BeTrue();
        _ = component.Find($"tr[data-permission-row='{firstPagePermKey}']").GetAttribute("data-permission-modified")
            .Should().Be("true");

        // Switch to All (infinite scroll) -> staged edits remain intact!
        component.Find("[data-datatable-page-size='true']").Change(PagedRequest.ShowAllPageSize.ToString(CultureInfo.InvariantCulture));
        _ = component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count").Should().Be("2");

        // Test Revert button -> restores staged edits back to saved states (0 unsaved changes).
        component.Find("[data-revert-permissions='true']").Click();
        _ = component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count").Should().Be("0");

        // Test Category-level Bulk Allow on Moderation -> stages only Moderation permissions for Custom VIPs (does not touch Moderators).
        component.Find($"[data-bulk-category-allow='{PermissionCategories.Moderation}']").Click();
        int moderationCount = harness.Registry.GetByCategory(PermissionCategories.Moderation).Count;
        _ = component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count")
            .Should().Be(moderationCount.ToString(CultureInfo.InvariantCulture));
        _ = permStore.SetGrantsBatchCalls.Should().BeEmpty();

        // Click Save Changes -> persists in one single SetGrantsBatchAsync call for Custom VIPs only!
        component.Find("[data-save-permissions='true']").Click();
        _ = permStore.SetGrantsBatchCalls.Should().ContainSingle();
        _ = permStore.SetGrantsBatchCalls[0].GroupId.Should().Be(vipsGroupId);
        _ = permStore.SetGrantsBatchCalls[0].Grants.Should().HaveCount(moderationCount);
        _ = permStore.SetGrantsBatchCalls[0].Grants.Values.Should().OnlyContain(static s => s == PermissionState.Allow);
        _ = permStore.Grants.Where(g => g.GroupId == modsGroupId).Should().BeEmpty();
        _ = component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count").Should().Be("0");

        // Return to ListMode and create a new custom group -> initializes 0 grant rows (all Inherit).
        component.Find("[data-back-to-groups='true']").Click();
        _ = component.Find("[data-permissions-mode='List']").Should().NotBeNull();
        component.Find("#new-group-name").Change("Stream Helpers");
        component.Find("[data-create-group='true']").Click();
        PermissionGroup createdGroup = permStore.WorkspaceGroups.Single(g => g.Name == "Stream Helpers");
        _ = permStore.Grants.Where(g => g.GroupId == createdGroup.Id).Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "Permissions")]
    public void Permissions_SelfEscalationWarningAndNavigationGuard_BlocksSaveUntilConfirmedAndPromptsOnDirtyNavigation()
    {
        Guid wsId = Guid.NewGuid();
        Guid vipsGroupId = Guid.NewGuid();
        Workspace ws = new() { Id = wsId, OwnerUserId = Guid.NewGuid(), Name = "Security Channel", PrimaryPlatformId = "twitch" };

        TrackingPermissionStore permStore = new(ws);
        permStore.WorkspaceGroups.Add(new PermissionGroup { Id = vipsGroupId, WorkspaceId = wsId, Name = "VIPs" });

        using TestPermissionHarness harness = new(permStore, ws, isRoot: false);
        IRenderedComponent<Permissions> component = harness.Context.Render<Permissions>();

        component.Find($"[data-group-edit='{vipsGroupId}']").Click();

        // Filter to ui.permissions.manage and set it to Allow -> triggers self-escalation warning and disables Save Changes.
        component.Find("#permissions-search").Input(BuiltInPermissionKeys.UiPermissionsManage);
        component.Find($"select[data-permission-select='{BuiltInPermissionKeys.UiPermissionsManage}']").Change(nameof(PermissionState.Allow));

        _ = component.FindAll("[data-self-escalation-warning='true']").Should().ContainSingle();
        _ = component.Find("[data-self-escalation-warning='true']").TextContent
            .Should().Contain(BuiltInPermissionKeys.UiPermissionsManage)
            .And.Contain("VIPs");
        _ = component.Find("[data-save-permissions='true']").HasAttribute("disabled").Should().BeTrue();

        // Clicking Cancel on self-escalation warning reverts ui.permissions.manage back to Inherit and hides the warning.
        component.Find("[data-cancel-self-escalation='true']").Click();
        _ = component.FindAll("[data-self-escalation-warning='true']").Should().BeEmpty();
        _ = component.Find($"select[data-permission-select='{BuiltInPermissionKeys.UiPermissionsManage}'] option[value='{PermissionState.Inherit}']").HasAttribute("selected")
            .Should().BeTrue();

        // Page-level bulk Allow also triggers the self-escalation warning when ui.permissions.manage was not Allow.
        component.Find("#permissions-search").Input(string.Empty);
        component.Find("[data-bulk-page-allow='true']").Click();
        _ = component.FindAll("[data-self-escalation-warning='true']").Should().ContainSingle();
        _ = component.Find("[data-save-permissions='true']").HasAttribute("disabled").Should().BeTrue();

        // Clicking Back to Groups while dirty opens the unsaved-changes guard dialog instead of discarding silently.
        component.Find("[data-back-to-groups='true']").Click();
        _ = component.FindAll("[data-unsaved-navigation-prompt='true']").Should().ContainSingle();
        _ = component.Find("[data-permissions-mode='Edit']").Should().NotBeNull();

        // Stay in editor, confirm self-escalation -> Save Changes becomes enabled and persists batch.
        component.Find("[data-cancel-discard-changes='true']").Click();
        _ = component.FindAll("[data-unsaved-navigation-prompt='true']").Should().BeEmpty();

        component.Find("[data-confirm-self-escalation='true']").Click();
        _ = component.Find("[data-save-permissions='true']").HasAttribute("disabled").Should().BeFalse();

        component.Find("[data-save-permissions='true']").Click();
        _ = permStore.SetGrantsBatchCalls.Should().ContainSingle();
        _ = permStore.SetGrantsBatchCalls[0].Grants[BuiltInPermissionKeys.UiPermissionsManage].Should().Be(PermissionState.Allow);
    }

    #endregion

    #region GlobalPermissionsAndAuditExplainView

    [Fact]
    [Trait("Member", "GlobalPermissions")]
    public void GlobalPermissions_ThreeModeAndWorkspaceAudit_ProtectsDefaultWorkspacePlanSupportsBatchSaveAndExplainsTier1Outcome()
    {
        Guid wsId = Guid.NewGuid();
        Guid defaultPlanId = Guid.NewGuid();
        Guid proPlanId = Guid.NewGuid();
        Workspace ws = new() { Id = wsId, OwnerUserId = Guid.NewGuid(), Name = "Partner Stream", PrimaryPlatformId = "twitch" };

        InMemoryWorkspaceStore workspaceStore = new();
        workspaceStore.Workspaces[wsId] = ws;

        TrackingPermissionStore permStore = new(ws);
        permStore.GlobalGroups[defaultPlanId] = new PermissionGroup
        {
            Id = defaultPlanId,
            WorkspaceId = null,
            Name = PermissionCategories.DefaultWorkspacePlanGroupName,
        };
        permStore.GlobalGroups[proPlanId] = new PermissionGroup
        {
            Id = proPlanId,
            WorkspaceId = null,
            Name = "Enterprise Plan",
        };
        permStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment { Id = Guid.NewGuid(), WorkspaceId = wsId, GlobalGroupId = defaultPlanId });
        permStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment { Id = Guid.NewGuid(), WorkspaceId = wsId, GlobalGroupId = proPlanId });
        permStore.Grants.Add(new PermissionGrant
        {
            Id = Guid.NewGuid(),
            GroupId = defaultPlanId,
            PermissionKey = BuiltInPermissionKeys.FeatureCustomCommands,
            State = PermissionState.Allow,
        });

        using TestPermissionHarness harness = new(permStore, ws, isRoot: true, workspaceStore: workspaceStore);
        IRenderedComponent<GlobalPermissions> component = harness.Context.Render<GlobalPermissions>();

        // Default Workspace Plan is marked as built-in (View & Edit, no Delete), while Enterprise Plan has Delete.
        _ = component.FindAll($"[data-group-view='{defaultPlanId}']").Should().ContainSingle();
        _ = component.FindAll($"[data-group-edit='{defaultPlanId}']").Should().ContainSingle();
        _ = component.FindAll($"[data-group-delete='{defaultPlanId}']").Should().BeEmpty();
        _ = component.FindAll($"[data-group-delete='{proPlanId}']").Should().ContainSingle();

        // Enter EditMode for Enterprise Plan, bulk-deny Features category, and Save Changes via SetGrantsBatchAsync.
        component.Find($"[data-group-edit='{proPlanId}']").Click();
        _ = component.Find("[data-permissions-mode='Edit']").Should().NotBeNull();
        component.Find($"[data-bulk-category-deny='{PermissionCategories.Features}']").Click();
        component.Find("[data-save-permissions='true']").Click();

        _ = permStore.SetGrantsBatchCalls.Should().ContainSingle();
        _ = permStore.SetGrantsBatchCalls[0].GroupId.Should().Be(proPlanId);
        component.WaitForState(() => component.Find("[data-unsaved-changes-count]").GetAttribute("data-unsaved-changes-count") == "0");

        // Return to ListMode and launch Workspace Permission Audit for Partner Stream.
        component.Find("[data-back-to-groups='true']").Click();
        component.WaitForState(() => component.FindAll("[data-permissions-mode='List']").Count == 1);
        _ = component.Find("[data-permissions-mode='List']").Should().NotBeNull();

        component.Find("[data-launch-workspace-audit='true']").Click();
        _ = component.Find("[data-permissions-mode='View']").Should().NotBeNull();
        _ = component.Markup.Should().Contain("Permission Audit: Partner Stream (twitch)");

        // Filter audit to feature.custom_commands: Default Workspace Plan allows it, Enterprise Plan denies it -> Denied by Global Permissions (Tier 1), and both groups + Implicit Deny default state appear in contributions.
        component.Find("#global-permissions-search").Input(BuiltInPermissionKeys.FeatureCustomCommands);
        _ = component.Find($"[data-audit-determined-by='{BuiltInPermissionKeys.FeatureCustomCommands}']").TextContent
            .Should().Contain("Global Permissions (Tier 1)");
        string breakdownHtml = component.Find($"details[data-audit-breakdown='{BuiltInPermissionKeys.FeatureCustomCommands}']").TextContent;
        _ = breakdownHtml.Should().Contain(PermissionCategories.DefaultWorkspacePlanGroupName)
            .And.Contain("Enterprise Plan")
            .And.Contain("Implicit Deny (Overridable Deny)");
    }

    [Fact]
    [Trait("Member", "Permissions")]
    public void Permissions_UserAuditView_OmitsInheritGroupsShowsImplicitDenyDefaultStateAndRedactsGlobalGroupNamesForNonRootViewer()
    {
        Guid wsId = Guid.NewGuid();
        Guid globalPlanId = Guid.NewGuid();
        Guid modsGroupId = Guid.NewGuid();
        Guid vipsGroupId = Guid.NewGuid();
        Workspace ws = new() { Id = wsId, OwnerUserId = Guid.NewGuid(), Name = "Streamer Audit Channel", PrimaryPlatformId = "twitch" };

        TrackingPermissionStore permStore = new(ws);
        permStore.GlobalGroups[globalPlanId] = new PermissionGroup { Id = globalPlanId, WorkspaceId = null, Name = "Secret Partner Tier" };
        permStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment { Id = Guid.NewGuid(), WorkspaceId = wsId, GlobalGroupId = globalPlanId });
        permStore.Grants.Add(new PermissionGrant
        {
            Id = Guid.NewGuid(),
            GroupId = globalPlanId,
            PermissionKey = BuiltInPermissionKeys.FeatureSoundboard,
            State = PermissionState.Deny,
        });

        permStore.WorkspaceGroups.Add(new PermissionGroup { Id = modsGroupId, WorkspaceId = wsId, Name = "Moderators", MappedPlatformDesignation = "moderator" });
        permStore.WorkspaceGroups.Add(new PermissionGroup { Id = vipsGroupId, WorkspaceId = wsId, Name = "Idle Inherit Group" });
        permStore.GroupMembers.Add(new PermissionGroupMember { Id = Guid.NewGuid(), GroupId = modsGroupId, PlatformId = "twitch", PlatformUserId = "777", LastKnownUsername = "mod_seven" });
        permStore.GroupMembers.Add(new PermissionGroupMember { Id = Guid.NewGuid(), GroupId = vipsGroupId, PlatformId = "twitch", PlatformUserId = "777", LastKnownUsername = "mod_seven" });
        permStore.Grants.Add(new PermissionGrant
        {
            Id = Guid.NewGuid(),
            GroupId = modsGroupId,
            PermissionKey = BuiltInPermissionKeys.ModerationTimeout,
            State = PermissionState.Allow,
        });

        // Render as a non-root workspace owner -> Tier 1 global group names must be redacted to "Global Permissions".
        using TestPermissionHarness harness = new(permStore, ws, isRoot: false);
        IRenderedComponent<Permissions> component = harness.Context.Render<Permissions>();

        component.Find("[data-audit-platform-id='true']").Change("twitch");
        component.Find("[data-audit-user-id='true']").Change("777");
        component.Find("[data-audit-username='true']").Change("mod_seven");
        component.Find("[data-launch-user-audit='true']").Click();

        _ = component.Find("[data-permissions-mode='View']").Should().NotBeNull();
        _ = component.Markup.Should().Contain("Permission Audit: mod_seven (twitch:777)");

        // 1. Check moderation.timeout: allowed by Moderators (Workspace Tier 2), omits "Idle Inherit Group", and includes "Implicit Deny (Overridable Deny)".
        component.Find("#permissions-search").Input(BuiltInPermissionKeys.ModerationTimeout);
        string timeoutBreakdown = component.Find($"details[data-audit-breakdown='{BuiltInPermissionKeys.ModerationTimeout}']").TextContent;
        _ = timeoutBreakdown.Should().Contain("Moderators")
            .And.Contain("Implicit Deny (Overridable Deny)")
            .And.NotContain("Idle Inherit Group");

        // 2. Check feature.soundboard: denied by Tier 1 global group, redacted to "Global Permissions" (never leaks "Secret Partner Tier").
        component.Find("#permissions-search").Input(BuiltInPermissionKeys.FeatureSoundboard);
        string soundboardBreakdown = component.Find($"details[data-audit-breakdown='{BuiltInPermissionKeys.FeatureSoundboard}']").TextContent;
        _ = soundboardBreakdown.Should().Contain("Global Permissions")
            .And.Contain("Implicit Deny (Overridable Deny)")
            .And.NotContain("Secret Partner Tier");
        _ = component.Markup.Should().NotContain("Secret Partner Tier");
    }

    #endregion

    private sealed class TestPermissionHarness : IDisposable
    {
        private readonly ServiceProvider _cacheProvider;

        public TestPermissionHarness(
            TrackingPermissionStore permStore,
            Workspace workspace,
            bool isRoot,
            InMemoryWorkspaceStore? workspaceStore = null)
        {
            ServiceCollection cacheServices = new();
            _ = cacheServices.AddHybridCache();
            this._cacheProvider = cacheServices.BuildServiceProvider();
            HybridCache cache = this._cacheProvider.GetRequiredService<HybridCache>();

            this.EventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
            this.Registry = new PermissionRegistry();
            this.PermissionService = new PermissionService(permStore, this.Registry, cache, this.EventBus);
            this.Context = new BunitContext();

            PanelUser user = new()
            {
                Id = workspace.OwnerUserId,
                DisplayName = isRoot ? "root_admin" : "streamer_owner",
                IsRoot = isRoot,
                IsGlobalAdmin = isRoot,
            };

            string policy = isRoot ? AuthClaims.RootOrGlobalAdminPolicy : AuthClaims.PanelAuthenticatedPolicy;
            _ = this.Context.AddAuthorization().SetAuthorized(user.DisplayName).SetPolicies(policy, AuthClaims.PanelAuthenticatedPolicy);

            ClaimsPrincipal principal = AuthClaims.BuildClaimsPrincipal(
                user,
                mfaElevatedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(15));
            _ = this.Context.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthStateProvider(principal));
            _ = this.Context.Services.AddSingleton<IWorkspaceSessionContext>(
                new SingleWorkspaceSessionContext(new AccessibleWorkspace(workspace, true, [])));
            _ = this.Context.Services.AddSingleton<IPermissionStore>(permStore);
            _ = this.Context.Services.AddSingleton<IPermissionService>(this.PermissionService);
            _ = this.Context.Services.AddSingleton<IPermissionRegistry>(this.Registry);
            _ = this.Context.Services.AddSingleton<IEventBus>(this.EventBus);

            InMemoryWorkspaceStore wsStore = workspaceStore ?? new InMemoryWorkspaceStore();
            wsStore.Workspaces[workspace.Id] = workspace;
            _ = this.Context.Services.AddSingleton<IWorkspaceStore>(wsStore);
            _ = this.Context.Services.AddSingleton<IMfaPolicyService>(new MfaPolicyService(
                new OptionalInstanceSettingsService(),
                wsStore,
                permStore,
                new AllowAllWorkspaceAccessService()));
            _ = this.Context.Services.AddSingleton(TimeProvider.System);
        }

        public BunitContext Context { get; }
        public PermissionRegistry Registry { get; }
        public PermissionService PermissionService { get; }
        public InMemoryEventBus EventBus { get; }

        public void Dispose()
        {
            this.Context.Dispose();
            this.EventBus.Dispose();
            this._cacheProvider.Dispose();
        }
    }

    private sealed class StubAuthStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class SingleWorkspaceSessionContext(AccessibleWorkspace workspace) : IWorkspaceSessionContext
    {
        public Guid? ActiveWorkspaceId => workspace.Workspace.Id;
        public AccessibleWorkspace? ActiveWorkspace => workspace;
        public IReadOnlyList<AccessibleWorkspace> AccessibleWorkspaces => [workspace];
        public event EventHandler? WorkspaceChanged
        {
            add { }
            remove { }
        }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<bool> SelectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
        public ValueTask RefreshAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class OptionalInstanceSettingsService : IInstanceSettingsService
    {
        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged
        {
            add { }
            remove { }
        }

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new InstanceSettings { StepUpSudoPolicy = StepUpSudoPolicy.Optional });
        public ValueTask UpdateAsync(InstanceSettings updated, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class AllowAllWorkspaceAccessService : IWorkspaceAccessService
    {
        public ValueTask<IReadOnlyList<AccessibleWorkspace>> GetAccessibleWorkspacesAsync(PanelUser user, IReadOnlyList<UserLoginIdentity> identities, IReadOnlyCollection<string>? oidcGroupClaims = null, string? authProvider = null, bool isMfaVerified = false, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<AccessibleWorkspace>>([]);
        public ValueTask<bool> CanAccessWorkspaceAsync(Guid workspaceId, PanelUser user, IReadOnlyList<UserLoginIdentity> identities, IReadOnlyCollection<string>? oidcGroupClaims = null, string? authProvider = null, bool isMfaVerified = false, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
        public ValueTask<bool> HasPermissionInWorkspaceAsync(Guid workspaceId, PanelUser user, IReadOnlyList<UserLoginIdentity> identities, string permissionKey, IReadOnlyCollection<string>? oidcGroupClaims = null, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }

    private sealed class InMemoryWorkspaceStore : IWorkspaceStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];

        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));
        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values.Where(w => w.OwnerUserId == ownerUserId)]);
        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values]);
        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);
        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);
        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>([]);
        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<WorkspacePlatform?>(null);
        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(platform);
        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);
        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class TrackingPermissionStore(params Workspace[] workspaces) : IPermissionStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = workspaces.ToDictionary(static w => w.Id);
        public Dictionary<Guid, PermissionGroup> GlobalGroups { get; } = [];
        public List<PermissionGroup> WorkspaceGroups { get; } = [];
        public List<PermissionGroupMember> GroupMembers { get; } = [];
        public List<WorkspaceGlobalGroupAssignment> WorkspaceGlobalAssignments { get; } = [];
        public List<PermissionGrant> Grants { get; } = [];
        public int SetGrantCallCount { get; private set; }
        public List<(Guid GroupId, Dictionary<string, PermissionState> Grants)> SetGrantsBatchCalls { get; } = [];

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            workspaceId.HasValue
                ? ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this.WorkspaceGroups.Where(g => g.WorkspaceId == workspaceId.Value)])
                : ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this.GlobalGroups.Values]);

        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGrant>>([.. this.Grants.Where(g => g.GroupId == groupId)]);

        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PermissionState.Inherit);

        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            HashSet<Guid> assigned = [.. this.WorkspaceGlobalAssignments.Where(a => a.WorkspaceId == workspaceId).Select(a => a.GlobalGroupId)];
            return ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this.GlobalGroups.Values.Where(g => assigned.Contains(g.Id))]);
        }

        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default)
        {
            this.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                GlobalGroupId = globalGroupId,
            });
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default)
        {
            _ = this.WorkspaceGlobalAssignments.RemoveAll(a => a.WorkspaceId == workspaceId && a.GlobalGroupId == globalGroupId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Guid>>([.. this.GroupMembers
                .Where(m => string.Equals(m.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.GroupId)]);

        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserPermissionOverride>>([]);

        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<WorkspacePlatformRoleRoster?>(null);

        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default)
        {
            if (group.WorkspaceId.HasValue)
            {
                _ = this.WorkspaceGroups.RemoveAll(g => g.Id == group.Id);
                this.WorkspaceGroups.Add(group);
            }
            else
            {
                this.GlobalGroups[group.Id] = group;
            }

            return ValueTask.FromResult(group);
        }

        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            _ = this.WorkspaceGroups.RemoveAll(g => g.Id == groupId);
            _ = this.GlobalGroups.Remove(groupId);
            return ValueTask.CompletedTask;
        }

        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default)
        {
            this.SetGrantCallCount++;
            this.ApplyGrant(groupId, permissionKey, state);
            return ValueTask.CompletedTask;
        }

        public ValueTask SetGrantsBatchAsync(Guid groupId, IReadOnlyDictionary<string, PermissionState> grants, CancellationToken cancellationToken = default)
        {
            this.SetGrantsBatchCalls.Add((groupId, new Dictionary<string, PermissionState>(grants, StringComparer.OrdinalIgnoreCase)));
            foreach (KeyValuePair<string, PermissionState> kvp in grants)
            {
                this.ApplyGrant(groupId, kvp.Key, kvp.Value);
            }

            return ValueTask.CompletedTask;
        }

        private void ApplyGrant(Guid groupId, string permissionKey, PermissionState state)
        {
            _ = this.Grants.RemoveAll(g => g.GroupId == groupId && string.Equals(g.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase));
            if (state != PermissionState.Inherit)
            {
                this.Grants.Add(new PermissionGrant
                {
                    Id = Guid.NewGuid(),
                    GroupId = groupId,
                    PermissionKey = permissionKey,
                    State = state,
                });
            }
        }

        public ValueTask<PermissionGroup> SeedDefaultGlobalGroupAsync(IReadOnlyCollection<string>? enabledDangerousFeatureKeys = null, CancellationToken cancellationToken = default)
        {
            PermissionGroup group = new() { Id = Guid.NewGuid(), WorkspaceId = null, Name = PermissionCategories.DefaultWorkspacePlanGroupName };
            this.GlobalGroups[group.Id] = group;
            return ValueTask.FromResult(group);
        }

        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroupMember>>([.. this.GroupMembers.Where(m => m.GroupId == groupId)]);

        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default)
        {
            this.GroupMembers.Add(member);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default)
        {
            _ = this.GroupMembers.RemoveAll(m => m.GroupId == groupId
                && string.Equals(m.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase));
            return ValueTask.CompletedTask;
        }

        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
