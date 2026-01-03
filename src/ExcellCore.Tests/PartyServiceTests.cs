using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Xunit;

namespace ExcellCore.Tests;

public sealed class PartyServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly PartyService _service;

    public PartyServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _service = new PartyService(_factory, new SequentialGuidGenerator());
    }

    [Fact]
    public async Task LookupAsync_WithShortSearchTerm_ReturnsFirstTwentyAlphabetically()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            for (var i = 0; i < 25; i++)
            {
                var partyId = Guid.NewGuid();
                await context.Parties.AddAsync(new Party
                {
                    PartyId = partyId,
                    DisplayName = $"Party {i:000}",
                    PartyType = i % 2 == 0 ? "Clinic" : "Retail",
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "Core.Identity" }
                });
            }

            await context.SaveChangesAsync();
        }

        var results = await _service.LookupAsync("A");

        Assert.Equal(20, results.Count);
        Assert.True(results.SequenceEqual(results.OrderBy(r => r.DisplayName)));
        Assert.All(results, r => Assert.Null(r.PrimaryIdentifier));
    }

    [Fact]
    public async Task LookupAsync_WithIdentifierMatch_ReturnsContextualDetails()
    {
        var nationalId = "NID-001";
        var taxId = "TAX-ALPHA";
        var nationalPartyId = Guid.NewGuid();
        var taxPartyId = Guid.NewGuid();

        await using (var context = await _factory.CreateDbContextAsync())
        {
            await context.Parties.AddRangeAsync(
                new Party
                {
                    PartyId = nationalPartyId,
                    DisplayName = "Alpha Pharmacy",
                    PartyType = "Pharmacy",
                    NationalId = nationalId,
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "Core.Identity" }
                },
                new Party
                {
                    PartyId = taxPartyId,
                    DisplayName = "Beta Clinic",
                    PartyType = "Clinic",
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "Core.Identity" }
                },
                new Party
                {
                    PartyId = Guid.NewGuid(),
                    DisplayName = "Gamma Hospital",
                    PartyType = "Hospital",
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "Core.Identity" }
                });

            await context.PartyIdentifiers.AddAsync(new PartyIdentifier
            {
                PartyIdentifierId = Guid.NewGuid(),
                PartyId = taxPartyId,
                Scheme = "TAX",
                Value = taxId,
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "Core.Identity" }
            });

            await context.SaveChangesAsync();
        }

        var nationalResults = await _service.LookupAsync(nationalId);
        var taxResults = await _service.LookupAsync("tax");

        var nationalMatch = Assert.Single(nationalResults);
        Assert.Equal(nationalPartyId, nationalMatch.PartyId);
        Assert.Equal(nationalId, nationalMatch.PrimaryIdentifier);
        Assert.Equal("Pharmacy · National ID", nationalMatch.RelationshipContext);

        var taxMatch = Assert.Single(taxResults);
        Assert.Equal(taxPartyId, taxMatch.PartyId);
        Assert.Equal(taxId, taxMatch.PrimaryIdentifier);
        Assert.Equal("Clinic", taxMatch.RelationshipContext);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
