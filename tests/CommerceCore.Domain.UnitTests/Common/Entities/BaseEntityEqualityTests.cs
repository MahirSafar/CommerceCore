using CommerceCore.Domain.Common.Entities;

namespace CommerceCore.Domain.UnitTests.Common.Entities;

public class BaseEntityEqualityTests
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id)
    {
    }

    [Fact]
    public void Entities_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        Assert.True(entity1.Equals(entity2));
        Assert.True(entity1 == entity2);
        Assert.Equal(entity1.GetHashCode(), entity2.GetHashCode());
    }
}
