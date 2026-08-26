// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.EntityFrameworkCore;
using EricksonLopez.Auditing.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.UnitTests;

public sealed class EfCoreAuditStoreTests
{
    private static readonly string[] IndexPropTenantOccurredId = new[] { "TenantId", "OccurredAt", "Id" };
    private static readonly string[] IndexPropTenantCorr = new[] { "TenantId", "CorrelationId" };
    private static readonly string[] IndexPropTenantResource = new[] { "TenantId", "ResourceType", "ResourceId" };

    private static IDbContextFactory<AuditDbContext> CreateFactory(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AuditDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDbContextFactory<AuditDbContext>>();
    }

    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        Action act = () => _ = new EfCoreAuditStore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_PersistsRecord_AndQueriesBack()
    {
        var factory = CreateFactory(nameof(AppendAsync_PersistsRecord_AndQueriesBack));
        var store = new EfCoreAuditStore(factory);

        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-alpha")
            .WithActor(AuditActorType.User, "usr-1", "Bob")
            .WithAction(AuditAction.Update)
            .WithResource("Invoice", "inv-500")
            .WithOutcome(AuditOutcome.Success)
            .AddChange("Status", "Draft", "Approved")
            .AddRedactedChange("SecretKey")
            .Build();

        await store.AppendAsync(record);
        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-alpha" });

        result.Records.Should().HaveCount(1);
        var fetched = result.Records[0];
        fetched.Id.Should().Be(record.Id);
        fetched.Actor.Id.Should().Be("usr-1");
        fetched.Action.Code.Should().Be("Update");
        fetched.Resource.Type.Should().Be("Invoice");
        fetched.Changes.Should().NotBeNull();
        fetched.Changes!.Should().HaveCount(2);
        fetched.Changes[0].Field.Should().Be("Status");
        fetched.Changes[0].NewValue.Should().Be("Approved");
        fetched.Changes[1].IsRedacted.Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_WithTenantIsolation_DoesNotReturnOtherTenantRecords()
    {
        var factory = CreateFactory(nameof(QueryAsync_WithTenantIsolation_DoesNotReturnOtherTenantRecords));
        var store = new EfCoreAuditStore(factory);

        var record1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-A", resourceId: "acc-1");
        var record2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-B", resourceId: "acc-2");

        await store.AppendBatchAsync(new[] { record1, record2 });
        var queryResultA = await store.QueryAsync(new AuditQuery { TenantId = "tenant-A" });
        var queryResultB = await store.QueryAsync(new AuditQuery { TenantId = "tenant-B" });

        queryResultA.Records.Should().HaveCount(1);
        queryResultA.Records[0].Context.TenantId.Should().Be("tenant-A");

        queryResultB.Records.Should().HaveCount(1);
        queryResultB.Records[0].Context.TenantId.Should().Be("tenant-B");
    }

    [Fact]
    public async Task QueryAsync_FilterByAllProperties_MatchesCorrectRecords()
    {
        var factory = CreateFactory(nameof(QueryAsync_FilterByAllProperties_MatchesCorrectRecords));
        var store = new EfCoreAuditStore(factory);

        var tenant = "tenant-filters";
        var now = DateTimeOffset.UtcNow;

        var r1 = AuditRecordBuilder.Create()
            .WithTenant(tenant)
            .WithActor(AuditActorType.User, "alice", "Alice")
            .WithAction(AuditAction.Create)
            .WithResource("Order", "ord-1")
            .WithOutcome(AuditOutcome.Success)
            .WithCorrelationId("corr-1")
            .WithOccurredAt(now.AddHours(-2))
            .Build();

        var r2 = AuditRecordBuilder.Create()
            .WithTenant(tenant)
            .WithActor(AuditActorType.User, "bob", "Bob")
            .WithAction(AuditAction.Delete)
            .WithResource("Payment", "pay-2")
            .WithOutcome(AuditOutcome.Failure)
            .WithCorrelationId("corr-2")
            .WithOccurredAt(now.AddHours(-1))
            .Build();

        await store.AppendBatchAsync(new[] { r1, r2 });

        // Filter by ActorId
        var byActor = await store.QueryAsync(new AuditQuery { TenantId = tenant, ActorId = "alice" });
        byActor.Records.Should().HaveCount(1);
        byActor.Records[0].Id.Should().Be(r1.Id);

        // Filter by ActionCode
        var byAction = await store.QueryAsync(new AuditQuery { TenantId = tenant, ActionCode = "Delete" });
        byAction.Records.Should().HaveCount(1);
        byAction.Records[0].Id.Should().Be(r2.Id);

        // Filter by ResourceType
        var byType = await store.QueryAsync(new AuditQuery { TenantId = tenant, ResourceType = "Payment" });
        byType.Records.Should().HaveCount(1);
        byType.Records[0].Id.Should().Be(r2.Id);

        // Filter by ResourceId
        var byResId = await store.QueryAsync(new AuditQuery { TenantId = tenant, ResourceId = "ord-1" });
        byResId.Records.Should().HaveCount(1);
        byResId.Records[0].Id.Should().Be(r1.Id);

        // Filter by Outcome
        var byOutcome = await store.QueryAsync(new AuditQuery { TenantId = tenant, Outcome = AuditOutcome.Failure });
        byOutcome.Records.Should().HaveCount(1);
        byOutcome.Records[0].Id.Should().Be(r2.Id);

        // Filter by CorrelationId
        var byCorr = await store.QueryAsync(new AuditQuery { TenantId = tenant, CorrelationId = "corr-1" });
        byCorr.Records.Should().HaveCount(1);
        byCorr.Records[0].Id.Should().Be(r1.Id);

        // Filter by Date Range (From & To)
        var byDate = await store.QueryAsync(new AuditQuery { TenantId = tenant, From = now.AddMinutes(-90), To = now });
        byDate.Records.Should().HaveCount(1);
        byDate.Records[0].Id.Should().Be(r2.Id);
    }

    [Fact]
    public async Task QueryAsync_KeysetPagination_PagesCorrectly()
    {
        var factory = CreateFactory(nameof(QueryAsync_KeysetPagination_PagesCorrectly));
        var store = new EfCoreAuditStore(factory);
        var tenant = "tenant-keyset";

        var records = Enumerable.Range(1, 5)
            .Select(i => AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: $"res-{i}"))
            .OrderBy(r => r.Id)
            .ToList();

        await store.AppendBatchAsync(records);

        // Page 1
        var page1 = await store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 2 });
        page1.Records.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();
        page1.NextCursorId.Should().NotBeNull();

        // Page 2
        var page2 = await store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 2, AfterRecordId = page1.NextCursorId });
        page2.Records.Should().HaveCount(2);
        page2.HasMore.Should().BeTrue();
        page2.NextCursorId.Should().NotBeNull();

        // Page 3 (final)
        var page3 = await store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 2, AfterRecordId = page2.NextCursorId });
        page3.Records.Should().HaveCount(1);
        page3.HasMore.Should().BeFalse();
        page3.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyList_DoesNothing()
    {
        var factory = CreateFactory(nameof(AppendBatchAsync_EmptyList_DoesNothing));
        var store = new EfCoreAuditStore(factory);

        await store.AppendBatchAsync(Array.Empty<AuditRecord>());
        var result = await store.QueryAsync(new AuditQuery { TenantId = "any" });
        result.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task NullGuards_ThrowArgumentNullException()
    {
        var factory = CreateFactory(nameof(NullGuards_ThrowArgumentNullException));
        var store = new EfCoreAuditStore(factory);

        Func<Task> nullAppend = async () => await store.AppendAsync(null!);
        await nullAppend.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullAppendBatch = async () => await store.AppendBatchAsync(null!);
        await nullAppendBatch.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void AuditingEfCoreExtensions_AddEntityFrameworkCoreAuditStore_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkCoreAuditStore(builder =>
        {
            builder.UseInMemoryDatabase("TestDiDb");
        });

        var provider = services.BuildServiceProvider();
        provider.GetService<IDbContextFactory<AuditDbContext>>().Should().NotBeNull();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService<IAuditStore>().Should().BeOfType<EfCoreAuditStore>();
    }

    [Fact]
    public void AuditingEfCoreExtensions_NullGuards()
    {
        IServiceCollection services = null!;
        Action act1 = () => services.AddEntityFrameworkCoreAuditStore(_ => { });
        act1.Should().Throw<ArgumentNullException>();

        var validServices = new ServiceCollection();
        Action act2 = () => validServices.AddEntityFrameworkCoreAuditStore(null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuditDbContext_ProtectedConstructor_Instantiates()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase("InheritedDb")
            .Options;

        var context = new CustomAuditDbContext(options);
        context.Should().NotBeNull();
        context.AuditRecords.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryAsync_NullOrWhitespaceTenantId_Throws(string? tenantId)
    {
        var factory = CreateFactory(nameof(QueryAsync_NullOrWhitespaceTenantId_Throws));
        var store = new EfCoreAuditStore(factory);

        Func<Task> act = async () => await store.QueryAsync(new AuditQuery { TenantId = tenantId! });
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task QueryAsync_NullQuery_ThrowsArgumentNullException()
    {
        var factory = CreateFactory(nameof(QueryAsync_NullQuery_ThrowsArgumentNullException));
        var store = new EfCoreAuditStore(factory);

        Func<Task> act = async () => await store.QueryAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryAsync_PageSizeClamping_Works()
    {
        var factory = CreateFactory(nameof(QueryAsync_PageSizeClamping_Works));
        var store = new EfCoreAuditStore(factory);
        var tenant = "tenant-clamp";

        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        await store.AppendBatchAsync(new[] { r1, r2 });

        // PageSize = 0 clamps to 1
        var pageZero = await store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 0 });
        pageZero.Records.Should().HaveCount(1);
        pageZero.HasMore.Should().BeTrue();

        // PageSize = -5 clamps to 1
        var pageNegative = await store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = -5 });
        pageNegative.Records.Should().HaveCount(1);

        // PageSize = 2000 clamps to 1000
        var pageLarge = await store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 2000 });
        pageLarge.Records.Should().HaveCount(2);
        pageLarge.HasMore.Should().BeFalse();
    }

    [Fact]
    public void AddEntityFrameworkCoreAuditStore_NullServices_ThrowsArgumentNullException()
    {
        Action act = () => AuditingEfCoreExtensions.AddEntityFrameworkCoreAuditStore(null!, _ => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddEntityFrameworkCoreAuditStore_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Action act = () => services.AddEntityFrameworkCoreAuditStore(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configureDbContext");
    }

    [Fact]
    public void AddEntityFrameworkCoreAuditStore_ValidServices_RegistersDependencies()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkCoreAuditStore(options => options.UseInMemoryDatabase("TestDiDb"));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetService<IAuditStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<EfCoreAuditStore>();
    }

    private sealed class TestAuditDbContext : AuditDbContext
    {
        public TestAuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
        {
        }

        public void CallOnModelCreating(ModelBuilder modelBuilder) => OnModelCreating(modelBuilder);
    }

    [Fact]
    public void AuditDbContext_OnModelCreating_AppliesAuditRecordConfiguration()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var ctx = new TestAuditDbContext(options);
        var modelBuilder = new ModelBuilder();
        ctx.CallOnModelCreating(modelBuilder);

        var convModel = (Microsoft.EntityFrameworkCore.Metadata.IConventionModel)modelBuilder.Model;
        var entityType = convModel.FindEntityType(typeof(AuditRecordEntity));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("audit_records");
    }

    [Fact]
    public void AuditDbContext_ModelConfiguration_ConfiguresAllPropertiesAndIndexes()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyAuditRecordConfiguration();

        var convModel = (Microsoft.EntityFrameworkCore.Metadata.IConventionModel)modelBuilder.Model;
        var entityType = convModel.FindEntityType(typeof(AuditRecordEntity));
        entityType.Should().NotBeNull();

        entityType!.GetTableName().Should().Be("audit_records");
        var primaryKey = entityType.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Select(p => p.Name).Should().Equal("Id");
        primaryKey.GetConfigurationSource().Should().Be(Microsoft.EntityFrameworkCore.Metadata.ConfigurationSource.Explicit);

        var tenant = entityType.FindProperty(nameof(AuditRecordEntity.TenantId));
        tenant!.GetMaxLength().Should().Be(128);
        tenant.IsNullable.Should().BeFalse();

        var occurred = entityType.FindProperty(nameof(AuditRecordEntity.OccurredAt));
        occurred.Should().NotBeNull();
        occurred!.IsNullable.Should().BeFalse();

        var source = entityType.FindProperty(nameof(AuditRecordEntity.Source));
        source!.GetMaxLength().Should().Be(256);
        source.IsNullable.Should().BeFalse();

        var action = entityType.FindProperty(nameof(AuditRecordEntity.ActionCode));
        action!.GetMaxLength().Should().Be(128);
        action.IsNullable.Should().BeFalse();

        var resType = entityType.FindProperty(nameof(AuditRecordEntity.ResourceType));
        resType!.GetMaxLength().Should().Be(256);
        resType.IsNullable.Should().BeFalse();

        var resId = entityType.FindProperty(nameof(AuditRecordEntity.ResourceId));
        resId!.GetMaxLength().Should().Be(256);
        resId.IsNullable.Should().BeFalse();

        var actorId = entityType.FindProperty(nameof(AuditRecordEntity.ActorId));
        actorId!.GetMaxLength().Should().Be(256);
        actorId.IsNullable.Should().BeFalse();

        var actorName = entityType.FindProperty(nameof(AuditRecordEntity.ActorName));
        actorName!.GetMaxLength().Should().Be(256);
        actorName.IsNullable.Should().BeTrue();

        var corr = entityType.FindProperty(nameof(AuditRecordEntity.CorrelationId));
        corr!.GetMaxLength().Should().Be(128);
        corr.IsNullable.Should().BeTrue();

        var caus = entityType.FindProperty(nameof(AuditRecordEntity.CausationId));
        caus!.GetMaxLength().Should().Be(128);
        caus.IsNullable.Should().BeTrue();

        var err = entityType.FindProperty(nameof(AuditRecordEntity.ErrorCode));
        err!.GetMaxLength().Should().Be(128);
        err.IsNullable.Should().BeTrue();

        var integrity = entityType.FindProperty(nameof(AuditRecordEntity.IntegrityHash));
        integrity!.GetMaxLength().Should().Be(256);
        integrity.IsNullable.Should().BeTrue();

        var prev = entityType.FindProperty(nameof(AuditRecordEntity.PreviousHash));
        prev!.GetMaxLength().Should().Be(256);
        prev.IsNullable.Should().BeTrue();

        var indexes = entityType.GetIndexes().ToList();
        indexes.Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(IndexPropTenantOccurredId));
        indexes.Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(IndexPropTenantCorr));
        indexes.Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(IndexPropTenantResource));
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyList_ReturnsImmediatelyWithoutCallingFactory()
    {
        var store = new EfCoreAuditStore(new ThrowingDbContextFactory());
        await store.AppendBatchAsync(Array.Empty<AuditRecord>());
    }

    [Fact]
    public async Task QueryAsync_BoundaryTimestamp_MatchesExactFromAndTo()
    {
        var factory = CreateFactory(nameof(QueryAsync_BoundaryTimestamp_MatchesExactFromAndTo));
        var store = new EfCoreAuditStore(factory);
        var tenant = "tenant-bounds";

        var t1 = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var r1 = AuditRecordBuilder.Create().WithTenant(tenant).WithOccurredAt(t1).Build();
        var r2 = AuditRecordBuilder.Create().WithTenant(tenant).WithOccurredAt(t2).Build();

        await store.AppendBatchAsync(new[] { r1, r2 });

        var matchFrom = await store.QueryAsync(new AuditQuery { TenantId = tenant, From = t1, To = t1 });
        matchFrom.Records.Should().HaveCount(1);
        matchFrom.Records[0].Id.Should().Be(r1.Id);

        var matchTo = await store.QueryAsync(new AuditQuery { TenantId = tenant, From = t2, To = t2 });
        matchTo.Records.Should().HaveCount(1);
        matchTo.Records[0].Id.Should().Be(r2.Id);
    }

    [Fact]
    public async Task QueryAsync_ExactPageSizeMatchesListCount_HasMoreIsFalse()
    {
        var factory = CreateFactory(nameof(QueryAsync_ExactPageSizeMatchesListCount_HasMoreIsFalse));
        var store = new EfCoreAuditStore(factory);
        var tenant = "tenant-exact-page";

        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        await store.AppendBatchAsync(new[] { r1, r2 });

        var result = await store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 2 });
        result.Records.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task AppendAsync_EmptyChangesList_DoesNotSerializeChangesJson()
    {
        var factory = CreateFactory(nameof(AppendAsync_EmptyChangesList_DoesNotSerializeChangesJson));
        var store = new EfCoreAuditStore(factory);
        var tenant = "tenant-empty-changes";

        var record = AuditRecordBuilder.BuildDefault(tenantId: tenant) with { Changes = Array.Empty<AuditChange>() };
        await store.AppendAsync(record);

        var result = await store.QueryAsync(new AuditQuery { TenantId = tenant });
        result.Records.Should().HaveCount(1);
        result.Records[0].Changes.Should().BeNull();

        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.AuditRecords.FirstAsync(e => e.Id == record.Id);
        entity.ChangesJson.Should().BeNull();
    }

    [Fact]
    public void ApplyAuditRecordConfiguration_CustomTableName_ConfiguresTable()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyAuditRecordConfiguration("custom_audit_table");

        var entityType = modelBuilder.Model.FindEntityType(typeof(AuditRecordEntity));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("custom_audit_table");
    }

    [Fact]
    public void ApplyAuditRecordConfiguration_DefaultTableName_ConfiguresAuditRecords()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyAuditRecordConfiguration();

        var entityType = modelBuilder.Model.FindEntityType(typeof(AuditRecordEntity));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("audit_records");
    }

    private sealed class CustomAuditDbContext : AuditDbContext
    {
        public CustomAuditDbContext(DbContextOptions options) : base(options) { }
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() => throw new InvalidOperationException("Should not be called.");
        public Task<AuditDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default) => throw new InvalidOperationException("Should not be called.");
    }
}

