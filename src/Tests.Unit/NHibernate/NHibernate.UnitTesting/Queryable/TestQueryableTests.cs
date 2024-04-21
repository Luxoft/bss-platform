using Bss.Platform.NHibernate.UnitTesting.Queryable;

using FluentAssertions;

using NHibernate.Linq;

using Xunit;

namespace Tests.Unit.NHibernate.NHibernate.UnitTesting.Queryable;

public class TestQueryableTests
{
    [Fact]
    public async Task NonGenericMethod_NotThrowException()
    {
        // Arrange
        var domainObject = new DomainObject { Name = nameof(DomainObject) };
        var queryable = new TestQueryable<DomainObject>(domainObject)
            .Where(x => x.Name == nameof(DomainObject));

        // Act
        var result = await queryable.ToListAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GenericMethod_NotThrowException()
    {
        // Arrange
        var domainObject = new DomainObject { Name = nameof(DomainObject), Parent = new DomainObject { Name = "Parent" } };
        var queryable = new TestQueryable<DomainObject>(domainObject)
                        .Where(x => x.Name == nameof(DomainObject))
                        .Fetch(x => x.Parent);

        // Act
        var result = await queryable.ToListAsync();

        // Assert
        result.Should().NotBeNull();
    }

    private class DomainObject
    {
        public string Name { get; init; } = default!;

        public DomainObject? Parent { get; init; }
    }
}
