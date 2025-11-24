using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using Xunit;

namespace HotSwap.Distributed.Tests.Domain;

public class ResourceQuotaTests
{
    [Fact]
    public void CreateDefault_ForFreeTier_ShouldReturnCorrectQuota()
    {
        var quota = ResourceQuota.CreateDefault(SubscriptionTier.Free);

        quota.Should().NotBeNull();
        quota.MaxWebsites.Should().Be(1);
        quota.StorageQuotaGB.Should().Be(1);
        quota.BandwidthQuotaGB.Should().Be(10);
        quota.MaxConcurrentDeployments.Should().Be(1);
        quota.MaxCustomDomains.Should().Be(0);
    }

    [Fact]
    public void CreateDefault_ForStarterTier_ShouldReturnCorrectQuota()
    {
        var quota = ResourceQuota.CreateDefault(SubscriptionTier.Starter);

        quota.Should().NotBeNull();
        quota.MaxWebsites.Should().Be(5);
        quota.StorageQuotaGB.Should().Be(10);
        quota.BandwidthQuotaGB.Should().Be(100);
        quota.MaxConcurrentDeployments.Should().Be(3);
        quota.MaxCustomDomains.Should().Be(5);
    }

    [Fact]
    public void CreateDefault_ForProfessionalTier_ShouldReturnCorrectQuota()
    {
        var quota = ResourceQuota.CreateDefault(SubscriptionTier.Professional);

        quota.Should().NotBeNull();
        quota.MaxWebsites.Should().Be(25);
        quota.StorageQuotaGB.Should().Be(50);
        quota.BandwidthQuotaGB.Should().Be(500);
        quota.MaxConcurrentDeployments.Should().Be(10);
        quota.MaxCustomDomains.Should().Be(25);
    }

    [Fact]
    public void CreateDefault_ForEnterpriseTier_ShouldReturnCorrectQuota()
    {
        var quota = ResourceQuota.CreateDefault(SubscriptionTier.Enterprise);

        quota.Should().NotBeNull();
        quota.MaxWebsites.Should().Be(100);
        quota.StorageQuotaGB.Should().Be(500);
        quota.BandwidthQuotaGB.Should().Be(5000);
        quota.MaxConcurrentDeployments.Should().Be(50);
        quota.MaxCustomDomains.Should().Be(100);
    }

    [Fact]
    public void CreateDefault_ForCustomTier_ShouldReturnUnlimitedQuota()
    {
        var quota = ResourceQuota.CreateDefault(SubscriptionTier.Custom);

        quota.Should().NotBeNull();
        quota.MaxWebsites.Should().Be(int.MaxValue);
        quota.StorageQuotaGB.Should().Be(long.MaxValue);
        quota.BandwidthQuotaGB.Should().Be(long.MaxValue);
        quota.MaxConcurrentDeployments.Should().Be(int.MaxValue);
        quota.MaxCustomDomains.Should().Be(int.MaxValue);
    }

    [Fact]
    public void ResourceQuota_Properties_ShouldBeSettable()
    {
        var quota = new ResourceQuota
        {
            MaxWebsites = 10,
            StorageQuotaGB = 50,
            BandwidthQuotaGB = 200,
            MaxConcurrentDeployments = 5,
            MaxCustomDomains = 15
        };

        quota.MaxWebsites.Should().Be(10);
        quota.StorageQuotaGB.Should().Be(50);
        quota.BandwidthQuotaGB.Should().Be(200);
        quota.MaxConcurrentDeployments.Should().Be(5);
        quota.MaxCustomDomains.Should().Be(15);
    }
}
