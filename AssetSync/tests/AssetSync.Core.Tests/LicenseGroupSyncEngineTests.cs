using AssetSync.Core;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using AssetSync.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AssetSync.Core.Tests;

public class LicenseGroupSyncEngineTests
{
    private static GroupLicenseMapping Mapping() =>
        new() { Id = 1, EntraGroupId = "g1", EntraGroupName = "G1", SnipeItLicenseId = 10, ReadOnly = true };

    private sealed class Harness
    {
        public Mock<IEntraDirectoryService> Entra = new();
        public Mock<ISnipeItService> Snipe = new();
        public Mock<IMappingRepository> Repo = new();
        public Mock<IConfigRepository> Config = new();
        public Mock<ILogRepository> Log = new();
        public Mock<IWebhookService> Webhook = new();

        public Harness()
        {
            // sensible defaults — individual tests override as needed
            Config.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
            Repo.Setup(r => r.GetUserMappingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<UserMapping>());
            Repo.Setup(r => r.GetPendingRemovalsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PendingRemoval>());
            Repo.Setup(r => r.UpsertPendingRemovalAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.ClearPendingRemovalAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.UpdateGroupLicenseRunStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SnipeItLookup>());
            Snipe.Setup(s => s.CheckoutSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Snipe.Setup(s => s.CheckinSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Log.Setup(l => l.AppendAsync(It.IsAny<LogEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Webhook.Setup(w => w.SendConnectivityFailureNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Write-direction defaults: group exists + writable; add/remove succeed; resolve echoes "obj-<upn>".
            Entra.Setup(e => e.GetGroupInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EntraGroupInfo { Id = "g1", Exists = true, IsMembershipWritable = true });
            Entra.Setup(e => e.AddGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Entra.Setup(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Entra.Setup(e => e.ResolveUserObjectIdAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string? upn, string? email, CancellationToken _) => string.IsNullOrEmpty(upn) ? null : "obj-" + upn);
        }

        public LicenseGroupSyncEngine Build() =>
            new(Entra.Object, Snipe.Object, Repo.Object, Config.Object, Log.Object, Webhook.Object,
                NullLogger<LicenseGroupSyncEngine>.Instance);
    }

    private static SnipeItLookup User(int id, string upn) => new() { Id = id, Name = upn, Username = upn, Email = upn };
    private static EntraUser Member(string upn) => new() { Id = Guid.NewGuid().ToString(), UserPrincipalName = upn, Mail = upn };
    private static LicenseSeat Seat(int id, int? assigned) => new() { Id = id, AssignedToUserId = assigned };

    // 1. Complete-read gate: a failed (thrown) enumeration must NOT remove anything.
    [Fact]
    public async Task FailedMemberRead_DoesNotRemove_AndIsError()
    {
        var h = new Harness();
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("page 2 failed"));

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Error, result.Status);
        h.Snipe.Verify(s => s.CheckinSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 2. Never act on the absence of data: an empty group is a hard stop, never a mass-removal.
    [Fact]
    public async Task EmptyGroup_DoesNotRemove_AndIsError()
    {
        var h = new Harness();
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EntraUser>());
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Seat(1, 200) }); // a currently-assigned seat that a naive run might revoke

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Error, result.Status);
        h.Snipe.Verify(s => s.CheckinSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 3a. Grace period — first absence: pending recorded, NO removal.
    [Fact]
    public async Task GracePeriod_FirstMiss_RecordsPending_NoRemoval()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x"), User(200, "bob@x") });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Member("alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Seat(1, 100), Seat(2, 200) }); // bob(200) is absent from the group
        h.Repo.Setup(r => r.GetPendingRemovalsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PendingRemoval>());

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.PendingNew);
        Assert.Equal(0, result.CheckedIn);
        h.Snipe.Verify(s => s.CheckinSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Repo.Verify(r => r.UpsertPendingRemovalAsync(1, "200", It.IsAny<CancellationToken>()), Times.Once);
    }

    // 3b. Grace period — second consecutive absence reaches the threshold: removal happens.
    [Fact]
    public async Task GracePeriod_SecondMiss_RemovesSeat()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x"), User(200, "bob@x") });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Member("alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Seat(1, 100), Seat(2, 200) });
        // bob already missed once
        h.Repo.Setup(r => r.GetPendingRemovalsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PendingRemoval { MappingId = 1, SubjectKey = "200", ConsecutiveMisses = 1 } });

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.CheckedIn);
        h.Snipe.Verify(s => s.CheckinSeatAsync(10, 2, It.IsAny<CancellationToken>()), Times.Once);
        h.Repo.Verify(r => r.ClearPendingRemovalAsync(1, "200", It.IsAny<CancellationToken>()), Times.Once);
    }

    // 3c. A reappeared user clears their pending state and is not removed.
    [Fact]
    public async Task ReappearedUser_ClearsPending_NoRemoval()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x"), User(200, "bob@x") });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Member("alice@x"), Member("bob@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Seat(1, 100), Seat(2, 200) });
        h.Repo.Setup(r => r.GetPendingRemovalsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PendingRemoval { MappingId = 1, SubjectKey = "200", ConsecutiveMisses = 1 } });

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        h.Snipe.Verify(s => s.CheckinSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Repo.Verify(r => r.ClearPendingRemovalAsync(1, "200", It.IsAny<CancellationToken>()), Times.Once);
    }

    // 4. Circuit breaker: more than the per-mapping limit (20) would be removed -> halt, remove nothing.
    [Fact]
    public async Task CircuitBreaker_Over20Removals_Halts_AndRemovesNothing()
    {
        var h = new Harness();
        h.Config.Setup(c => c.GetAsync(ConfigKeys.LicenseRemovalGraceSyncs, It.IsAny<CancellationToken>())).ReturnsAsync("1"); // remove on first miss
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Member("alice@x") });
        // 1 kept seat for alice + 21 seats assigned to users absent from the group
        var seats = new List<LicenseSeat> { Seat(1, 100) };
        for (int i = 0; i < 21; i++) seats.Add(Seat(100 + i, 1000 + i));
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(seats);

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Halted, result.Status);
        h.Snipe.Verify(s => s.CheckinSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Repo.Verify(r => r.UpsertPendingRemovalAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never); // pending state left intact
    }

    // Assign phase: a group member with no seat is checked out a free seat.
    [Fact]
    public async Task AssignsFreeSeat_ToMemberWithoutOne()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Member("alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Seat(1, null) }); // one free seat

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.Assigned);
        h.Snipe.Verify(s => s.CheckoutSeatAsync(10, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    // No free seat: surfaced as a skip, never an error/removal.
    [Fact]
    public async Task NoFreeSeat_SkipsAndLogs_NoError()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x"), User(200, "bob@x") });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Member("alice@x"), Member("bob@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Seat(1, 100) }); // alice holds the only seat; bob has none and none free

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.NoFreeSeat);
        Assert.Equal(0, result.Assigned);
    }

    // Unmatched member: skipped, and never triggers a counterpart removal.
    [Fact]
    public async Task UnmatchedMember_SkipsAndLogs()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Member("ghost@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<LicenseSeat>());

        var result = await h.Build().RunMappingAsync(Mapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.NoMatch);
    }

    // ===== Write direction (read_only = OFF, Snipe authoritative -> writes Entra membership) =====

    private static GroupLicenseMapping WriteMapping() =>
        new() { Id = 1, EntraGroupId = "g1", EntraGroupName = "G1", SnipeItLicenseId = 10, ReadOnly = false };
    private static EntraUser EntraMember(string objectId) => new() { Id = objectId };

    // Add: a licensed user not currently in the group is added.
    [Fact]
    public async Task Write_AddsLicensedUserNotInGroup()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Seat(1, 100) });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EntraUser>());

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.Added);
        h.Entra.Verify(e => e.AddGroupMemberAsync("g1", "obj-alice@x", It.IsAny<CancellationToken>()), Times.Once);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Remove first miss: an unlicensed member goes pending, is NOT removed.
    [Fact]
    public async Task Write_RemovalFirstMiss_RecordsPending_NoRemoval()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Seat(1, 100) });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { EntraMember("obj-alice@x"), EntraMember("obj-bob") }); // bob no longer licensed
        h.Repo.Setup(r => r.GetPendingRemovalsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PendingRemoval>());

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.PendingNew);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Repo.Verify(r => r.UpsertPendingRemovalAsync(1, "obj-bob", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Remove second consecutive miss: directory member is removed.
    [Fact]
    public async Task Write_RemovalSecondMiss_RemovesMember()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Seat(1, 100) });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { EntraMember("obj-alice@x"), EntraMember("obj-bob") });
        h.Repo.Setup(r => r.GetPendingRemovalsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PendingRemoval { MappingId = 1, SubjectKey = "obj-bob", ConsecutiveMisses = 1 } });

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.Removed);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync("g1", "obj-bob", It.IsAny<CancellationToken>()), Times.Once);
        h.Repo.Verify(r => r.ClearPendingRemovalAsync(1, "obj-bob", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Resolve no-match: a licensed Snipe user with no Entra identity is skipped, and triggers no removal.
    [Fact]
    public async Task Write_ResolveNoMatch_SkipsUser_NoRemoval()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x"), User(200, "ghost@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Seat(1, 100), Seat(2, 200) });
        h.Entra.Setup(e => e.ResolveUserObjectIdAsync("ghost@x", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { EntraMember("obj-alice@x") });

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.NoMatch);
        h.Entra.Verify(e => e.AddGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Complete-read gate: a failed/partial Snipe seat read must NOT drive Entra removals.
    [Fact]
    public async Task Write_PartialSeatRead_DoesNotRemove_AndIsError()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("seat page failed"));

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Error, result.Status);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Entra.Verify(e => e.AddGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Missing/deleted group is an error — never a write target.
    [Fact]
    public async Task Write_MissingGroup_IsError()
    {
        var h = new Harness();
        h.Entra.Setup(e => e.GetGroupInfoAsync("g1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntraGroupInfo { Id = "g1", Exists = false });

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Error, result.Status);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Dynamic (non-writable) group is refused.
    [Fact]
    public async Task Write_DynamicGroup_IsError()
    {
        var h = new Harness();
        h.Entra.Setup(e => e.GetGroupInfoAsync("g1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntraGroupInfo { Id = "g1", Exists = true, IsMembershipWritable = false });

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Error, result.Status);
        h.Entra.Verify(e => e.AddGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Never act on the absence of data: empty desired set + non-empty group => refuse mass removal.
    [Fact]
    public async Task Write_EmptyDesired_NonEmptyGroup_IsError_NoRemoval()
    {
        var h = new Harness();
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SnipeItLookup>());
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<LicenseSeat>()); // nobody licensed
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { EntraMember("obj-alice@x") });

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Error, result.Status);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Circuit breaker: more than 20 directory removals in one run halts the mapping, removes nothing.
    [Fact]
    public async Task Write_CircuitBreaker_Over20Removals_Halts()
    {
        var h = new Harness();
        h.Config.Setup(c => c.GetAsync(ConfigKeys.LicenseRemovalGraceSyncs, It.IsAny<CancellationToken>())).ReturnsAsync("1");
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Seat(1, 100) });
        var members = new List<EntraUser> { EntraMember("obj-alice@x") };
        for (int i = 0; i < 21; i++) members.Add(EntraMember("obj-stale-" + i));
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Halted, result.Status);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Repo.Verify(r => r.UpsertPendingRemovalAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Dry run: no directory writes, no state mutation, but the would-add/would-remove deltas surface.
    [Fact]
    public async Task Write_DryRun_NoWrites_SurfacesDeltas()
    {
        var h = new Harness();
        h.Config.Setup(c => c.GetAsync(ConfigKeys.LicenseRemovalGraceSyncs, It.IsAny<CancellationToken>())).ReturnsAsync("1");
        h.Snipe.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { User(100, "alice@x") });
        h.Snipe.Setup(s => s.GetLicenseSeatsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Seat(1, 100) });
        h.Entra.Setup(e => e.GetGroupMembersAsync("g1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { EntraMember("obj-bob") }); // alice missing -> add; bob stale -> remove

        var result = await h.Build().RunMappingAsync(WriteMapping(), dryRun: true);

        Assert.Equal(LicenseGroupRunStatus.Ok, result.Status);
        Assert.Equal(1, result.Added);   // would add alice
        Assert.Equal(1, result.Removed); // would remove bob (grace=1)
        h.Entra.Verify(e => e.AddGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Entra.Verify(e => e.RemoveGroupMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Repo.Verify(r => r.UpsertPendingRemovalAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
