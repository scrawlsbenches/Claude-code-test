using FluentAssertions;
using HotSwap.KnowledgeGraph.Domain.Enums;

namespace HotSwap.KnowledgeGraph.Tests.Domain;

public class EnumsTests
{
    [Fact]
    public void Direction_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<Direction>().Should().HaveCount(3);
        Enum.IsDefined(typeof(Direction), Direction.Outgoing).Should().BeTrue();
        Enum.IsDefined(typeof(Direction), Direction.Incoming).Should().BeTrue();
        Enum.IsDefined(typeof(Direction), Direction.Both).Should().BeTrue();
    }

    [Fact]
    public void IndexType_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<IndexType>().Should().HaveCount(5);
        Enum.IsDefined(typeof(IndexType), IndexType.BTree).Should().BeTrue();
        Enum.IsDefined(typeof(IndexType), IndexType.Hash).Should().BeTrue();
        Enum.IsDefined(typeof(IndexType), IndexType.GIN).Should().BeTrue();
        Enum.IsDefined(typeof(IndexType), IndexType.GiST).Should().BeTrue();
        Enum.IsDefined(typeof(IndexType), IndexType.FullText).Should().BeTrue();
    }

    [Fact]
    public void PropertyType_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<PropertyType>().Should().HaveCount(10);
        Enum.IsDefined(typeof(PropertyType), PropertyType.String).Should().BeTrue();
        Enum.IsDefined(typeof(PropertyType), PropertyType.Integer).Should().BeTrue();
        Enum.IsDefined(typeof(PropertyType), PropertyType.Double).Should().BeTrue();
        Enum.IsDefined(typeof(PropertyType), PropertyType.Boolean).Should().BeTrue();
        Enum.IsDefined(typeof(PropertyType), PropertyType.DateTime).Should().BeTrue();
    }

    [Fact]
    public void QueryOperator_HasExpectedValues()
    {
        // Assert
        var operators = Enum.GetValues<QueryOperator>();
        operators.Should().HaveCount(15);
        operators.Should().Contain(QueryOperator.Equals);
        operators.Should().Contain(QueryOperator.NotEquals);
        operators.Should().Contain(QueryOperator.GreaterThan);
        operators.Should().Contain(QueryOperator.Contains);
        operators.Should().Contain(QueryOperator.IsNull);
    }

    [Fact]
    public void Direction_CanBeUsedInSwitchStatement()
    {
        // Arrange
        var direction = Direction.Outgoing;

        // Act
        var result = direction switch
        {
            Direction.Outgoing => "outgoing",
            Direction.Incoming => "incoming",
            Direction.Both => "both",
            _ => throw new ArgumentException()
        };

        // Assert
        result.Should().Be("outgoing");
    }
}
