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

    // Read-only OFF (write direction) is deferred to Phase 2 — must be skipped, never act.
    [Fact]
    public async Task WriteDirectionMapping_IsSkipped_Phase2()
    {
        var h = new Harness();
        var mapping = Mapping();
        mapping.ReadOnly = false;

        var result = await h.Build().RunMappingAsync(mapping, dryRun: false);

        Assert.Equal(LicenseGroupRunStatus.Skipped, result.Status);
        h.Entra.Verify(e => e.GetGroupMembersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Snipe.Verify(s => s.CheckinSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
