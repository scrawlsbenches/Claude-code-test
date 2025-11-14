using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class ModuleDescriptorTests
{
    [Fact]
    public void Validate_WithValidDescriptor_ShouldNotThrow()
    {
        // Arrange
        var descriptor = new ModuleDescriptor
        {
            Name = "test-module",
            Version = new Version(1, 0, 0),
            Description = "Test module",
            Author = "Test Author",
            ResourceRequirements = new ResourceRequirements
            {
                MemoryMB = 512,
                CpuCores = 1.0,
                DiskMB = 100
            }
        };

        // Act
        Action act = () => descriptor.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldThrow()
    {
        // Arrange
        var descriptor = new ModuleDescriptor
        {
            Name = "",
            Version = new Version(1, 0, 0)
        };

        // Act
        Action act = () => descriptor.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public void Validate_WithZeroMemory_ShouldThrow()
    {
        // Arrange
        var descriptor = new ModuleDescriptor
        {
            Name = "test-module",
            Version = new Version(1, 0, 0),
            ResourceRequirements = new ResourceRequirements
            {
                MemoryMB = 0,
                CpuCores = 1.0
            }
        };

        // Act
        Action act = () => descriptor.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Memory*");
    }

    [Fact]
    public void Validate_WithZeroCpu_ShouldThrow()
    {
        // Arrange
        var descriptor = new ModuleDescriptor
        {
            Name = "test-module",
            Version = new Version(1, 0, 0),
            ResourceRequirements = new ResourceRequirements
            {
                MemoryMB = 512,
                CpuCores = 0
            }
        };

        // Act
        Action act = () => descriptor.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*CPU*");
    }
}
