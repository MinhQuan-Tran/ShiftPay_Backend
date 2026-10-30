using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using ShiftPay_Backend.Data;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

public class WorkInfoModelTests
{
	private const string ProfileId = "6b6b7284-5591-4fcb-8641-c8347775b10a";
	private const string OtherProfileId = "474d53b2-44b4-48d8-8c65-7fed1bd584ea";
	private const string StandardRateId = "5bbf7cd9-4eb5-43bd-8427-cf8222dcbd16";
	private const string OvernightRateId = "a2dbdc82-8843-4358-820a-204554b66c30";

	[Fact]
	public void LegacyRates_HaveStableCultureIndependentIdentities()
	{
		var workInfo = LegacyWorkInfo(25.500m, 30m);
		var originalCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			var firstRead = workInfo.ToDTO();
			var secondRead = workInfo.ToDTO();
			var profile = Assert.Single(firstRead.Profiles!);

			Assert.Equal("legacy", profile.Id);
			Assert.Equal(new[] { "legacy-25.5", "legacy-30" }, profile.PayRates.Select(rate => rate.Id));
			Assert.Equal(profile.PayRates.Select(rate => rate.Id), secondRead.Profiles![0].PayRates.Select(rate => rate.Id));
			Assert.Equal(new[] { 25.500m, 30m }, profile.PayRates.Select(rate => rate.HourlyRate));
			Assert.All(profile.PayRates, rate => Assert.Empty(rate.Name));
			Assert.Null(workInfo.Profiles);
		}
		finally
		{
			CultureInfo.CurrentCulture = originalCulture;
		}
	}

	[Fact]
	public void LegacyDocument_WithMaterializedEmptyCollection_StillExposesNumericRates()
	{
		var workInfo = LegacyWorkInfo(25m, 30m);
		workInfo.Profiles = [];
		workInfo.HasProfiles = false;

		var dto = workInfo.ToDTO();

		Assert.Equal(new[] { 25m, 30m }, dto.PayRates);
		Assert.Equal("legacy", Assert.Single(dto.Profiles!).Id);
		Assert.Equal(2, dto.Profiles![0].PayRates.Count);
		Assert.Null(typeof(WorkInfoDTO).GetProperty("HasProfiles"));
	}

	[Fact]
	public void EqualAmounts_WithDifferentNames_RemainSeparateChoices()
	{
		var workInfo = WorkInfo.FromDTO(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Weekend", 35m), Rate(OvernightRateId, "Overnight", 35m))]
		}, "user-a");

		var dto = workInfo.ToDTO();

		Assert.Equal(2, Assert.Single(dto.Profiles!).PayRates.Count);
		Assert.Equal(new[] { "Weekend", "Overnight" }, dto.Profiles![0].PayRates.Select(rate => rate.Name));
		Assert.Equal(new[] { 35m }, dto.PayRates);
	}

	[Fact]
	public void StructuredPayload_DoesNotTreatNumericProjectionAsExtraLegacyChoices()
	{
		var workInfo = WorkInfo.FromDTO(new WorkInfoDTO
		{
			Workplace = "Hospital",
			PayRates = [25m, 30m],
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Standard", 25m))]
		}, "user-a");

		var dto = workInfo.ToDTO();
		Assert.Equal(ProfileId, Assert.Single(dto.Profiles!).Id);
		Assert.Equal(new[] { 25m }, dto.PayRates);
	}

	[Fact]
	public void MergeByIdentity_AddsNewRates_WithoutOverwritingExistingChoices()
	{
		var workInfo = WorkInfo.FromDTO(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles =
			[
				Profile(ProfileId, Rate(StandardRateId, "Standard", 25m), Rate(OvernightRateId, "Overnight", 30m)),
				Profile(OtherProfileId, Rate(Guid.NewGuid().ToString(), "Standard", 28m))
			]
		}, "user-a");

		workInfo.MergeFrom(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Stale imported name", 27m), Rate(Guid.NewGuid().ToString(), "Holiday", 50m))]
		});

		var profiles = workInfo.ToDTO().Profiles!;
		Assert.Equal(2, profiles.Count);
		var updatedProfile = Assert.Single(profiles, profile => profile.Id == ProfileId);
		Assert.Equal(3, updatedProfile.PayRates.Count);
		var updatedRate = Assert.Single(updatedProfile.PayRates, rate => rate.Id == StandardRateId);
		Assert.Equal("Standard", updatedRate.Name);
		Assert.Equal(25m, updatedRate.HourlyRate);
		Assert.Equal(30m, Assert.Single(updatedProfile.PayRates, rate => rate.Id == OvernightRateId).HourlyRate);
		Assert.Contains(updatedProfile.PayRates, rate => rate.Name == "Holiday" && rate.HourlyRate == 50m);
		Assert.Contains(profiles, profile => profile.Id == OtherProfileId);
	}

	[Fact]
	public void ReplaceProfiles_RemovesRates_WithoutRestoringLegacyProjection()
	{
		var workInfo = LegacyWorkInfo(25m, 30m);

		workInfo.ReplaceProfiles(new WorkInfoDTO
		{
			Workplace = "Hospital",
			PayRates = [25m, 30m],
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Standard", 26m))]
		});

		var dto = workInfo.ToDTO();
		Assert.Equal(new[] { 26m }, dto.PayRates);
		Assert.Equal(26m, Assert.Single(Assert.Single(dto.Profiles!).PayRates).HourlyRate);
		Assert.DoesNotContain(dto.Profiles!, profile => profile.Id == "legacy");
	}

	[Fact]
	public void ReplaceProfiles_WithEmptyList_KeepsRecordEmptyOnLaterReads()
	{
		var workInfo = LegacyWorkInfo(25m);
		workInfo.ReplaceProfiles(new WorkInfoDTO { Workplace = "Hospital", Profiles = [] });

		Assert.Empty(workInfo.ToDTO().Profiles!);
		Assert.Empty(workInfo.ToDTO().PayRates);
	}

	[Fact]
	public void LegacyPost_MergesNumericRatesWithoutDuplicates()
	{
		var workInfo = LegacyWorkInfo(25m, 30m);

		workInfo.MergeFrom(new WorkInfoDTO { Workplace = "Hospital", PayRates = [30m, 35m] });

		Assert.Equal(new[] { 25m, 30m, 35m }, workInfo.ToDTO().PayRates.Order());
	}

	[Fact]
	public void LegacyPost_AfterEditingMigratedAmount_AddsOriginalAmountWithoutIdentityCollision()
	{
		var workInfo = LegacyWorkInfo(25m);
		workInfo.ReplaceProfiles(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile("legacy", Rate("legacy-25", "", 28m))]
		});

		workInfo.MergeFrom(new WorkInfoDTO { Workplace = "Hospital", PayRates = [25m] });

		var rates = Assert.Single(workInfo.ToDTO().Profiles!).PayRates;
		Assert.Equal(28m, Assert.Single(rates, rate => rate.Id == "legacy-25").HourlyRate);
		var added = Assert.Single(rates, rate => rate.HourlyRate == 25m);
		Assert.Equal("Custom", added.Name);
		Assert.True(Guid.TryParse(added.Id, out _));
		Assert.Equal(new[] { 25m, 28m }, workInfo.ToDTO().PayRates.Order());
	}

	[Fact]
	public void LegacyPost_WithAmountAlreadyNamedElsewhere_DoesNotAddGenericDuplicate()
	{
		var workInfo = WorkInfo.FromDTO(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Standard", 25m))]
		}, "user-a");

		workInfo.MergeFrom(new WorkInfoDTO { Workplace = "Hospital", PayRates = [25m] });

		var profile = Assert.Single(workInfo.ToDTO().Profiles!);
		Assert.Equal(ProfileId, profile.Id);
		Assert.Equal(StandardRateId, Assert.Single(profile.PayRates).Id);
	}

	[Fact]
	public void LegacyDeletion_DoesNotRemoveNamedRateWithSameAmount()
	{
		var workInfo = LegacyWorkInfo(30m);
		workInfo.MergeFrom(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Weekend", 30m))]
		});

		workInfo.RemoveLegacyPayRate(30m);

		var dto = workInfo.ToDTO();
		Assert.Equal(30m, Assert.Single(Assert.Single(dto.Profiles!, profile => profile.Id == ProfileId).PayRates).HourlyRate);
		Assert.DoesNotContain(dto.Profiles!.Where(profile => profile.Id == "legacy").SelectMany(profile => profile.PayRates), rate => rate.HourlyRate == 30m);
		Assert.Contains(30m, dto.PayRates);
	}

	[Fact]
	public void LegacyDeletion_RemovesUnnamedMigratedRateAcrossProfiles_ButKeepsRenamedChoice()
	{
		var workInfo = WorkInfo.FromDTO(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles =
			[
				Profile(ProfileId, Rate("legacy-30", "", 30m)),
				Profile(OtherProfileId, Rate("legacy-30", "Weekend", 30m))
			]
		}, "user-a");

		workInfo.RemoveLegacyPayRate(30m);

		var profiles = workInfo.ToDTO().Profiles!;
		Assert.Empty(Assert.Single(profiles, profile => profile.Id == ProfileId).PayRates);
		var retained = Assert.Single(Assert.Single(profiles, profile => profile.Id == OtherProfileId).PayRates);
		Assert.Equal("Weekend", retained.Name);
		Assert.Equal(30m, retained.HourlyRate);
	}

	[Fact]
	public void ReplaceProfiles_RequiresExplicitList_AndLeavesExistingRatesUntouched()
	{
		var workInfo = LegacyWorkInfo(25m);

		Assert.Throws<InvalidOperationException>(() => workInfo.ReplaceProfiles(new WorkInfoDTO { Workplace = "Hospital" }));

		Assert.Equal(new[] { 25m }, workInfo.ToDTO().PayRates);
	}

	[Fact]
	public void ToDto_DoesNotExposeMutableStoredProfiles()
	{
		var workInfo = WorkInfo.FromDTO(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Standard", 25m))]
		}, "user-a");

		var dto = workInfo.ToDTO();
		dto.Profiles![0].Position = "Changed";
		dto.Profiles[0].PayRates[0].HourlyRate = 999m;

		var stored = workInfo.ToDTO().Profiles![0];
		Assert.Equal("Nurse", stored.Position);
		Assert.Equal(25m, stored.PayRates[0].HourlyRate);
	}

	[Theory]
	[InlineData("profile-id")]
	[InlineData("rate-id")]
	[InlineData("name")]
	[InlineData("negative")]
	[InlineData("duplicate-profile")]
	[InlineData("duplicate-rate")]
	[InlineData("null-profile")]
	[InlineData("null-rate")]
	[InlineData("null-rates")]
	public void InvalidProfiles_AreRejectedBeforeStorage(string invalidField)
	{
		var profile = Profile(ProfileId, Rate(StandardRateId, "Standard", 25m));
		var dto = new WorkInfoDTO { Workplace = "Hospital", Profiles = [profile] };
		switch (invalidField)
		{
			case "profile-id": profile.Id = " "; break;
			case "rate-id": profile.PayRates[0].Id = " "; break;
			case "name": profile.PayRates[0].Name = " "; break;
			case "negative": profile.PayRates[0].HourlyRate = -1m; break;
			case "duplicate-profile": dto.Profiles.Add(Profile(ProfileId)); break;
			case "duplicate-rate": profile.PayRates.Add(Rate(StandardRateId, "Other", 30m)); break;
			case "null-profile": dto.Profiles.Add(null!); break;
			case "null-rate": profile.PayRates.Add(null!); break;
			case "null-rates": profile.PayRates = null!; break;
		}

		var exception = Record.Exception(() => WorkInfo.FromDTO(dto, "user-a"));
		Assert.True(exception is ArgumentException or InvalidOperationException, exception?.ToString() ?? "Expected validation to reject the payload.");
	}

	[Fact]
	public void CosmosModel_EmbedsProfilesAndRates_WithClientSuppliedIds()
	{
		using var context = CreateModelContext();
		var workInfo = context.Model.FindEntityType(typeof(WorkInfo))!;
		var profile = context.Model.FindEntityType(typeof(WorkProfile))!;
		var rate = context.Model.FindEntityType(typeof(NamedPayRate))!;

		Assert.Equal("WorkInfos", workInfo.GetContainer());
		Assert.True(profile.IsOwned());
		Assert.Equal(typeof(WorkInfo), profile.FindOwnership()!.PrincipalEntityType.ClrType);
		Assert.Equal("Profiles", profile.GetContainingPropertyName());
		Assert.True(rate.IsOwned());
		Assert.Equal(typeof(WorkProfile), rate.FindOwnership()!.PrincipalEntityType.ClrType);
		Assert.Equal("PayRates", rate.GetContainingPropertyName());
		Assert.Equal(ValueGenerated.Never, profile.FindProperty(nameof(WorkProfile.Id))!.ValueGenerated);
		Assert.Equal(ValueGenerated.Never, rate.FindProperty(nameof(NamedPayRate.Id))!.ValueGenerated);
		Assert.False(string.IsNullOrEmpty(CosmosPropertyExtensions.GetJsonPropertyName(profile.FindProperty(nameof(WorkProfile.Id))!)));
		Assert.False(string.IsNullOrEmpty(CosmosPropertyExtensions.GetJsonPropertyName(rate.FindProperty(nameof(NamedPayRate.Id))!)));
	}

	[Fact]
	public void ReplacingTrackedProfiles_WithSameIds_TracksUpdatedValuesWithoutIdentityConflicts()
	{
		using var context = CreateModelContext();
		var workInfo = WorkInfo.FromDTO(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Standard", 25m), Rate(OvernightRateId, "Overnight", 30m))]
		}, "user-a");
		context.Attach(workInfo);

		workInfo.ReplaceProfiles(new WorkInfoDTO
		{
			Workplace = "Hospital",
			Profiles = [Profile(ProfileId, Rate(StandardRateId, "Updated standard", 27m))]
		});
		context.ChangeTracker.DetectChanges();

		var profiles = context.ChangeTracker.Entries<WorkProfile>().Where(entry => entry.State != EntityState.Deleted).ToList();
		var rates = context.ChangeTracker.Entries<NamedPayRate>().Where(entry => entry.State != EntityState.Deleted).ToList();
		Assert.Equal(ProfileId, Assert.Single(profiles).Entity.Id);
		var rate = Assert.Single(rates).Entity;
		Assert.Equal(StandardRateId, rate.Id);
		Assert.Equal("Updated standard", rate.Name);
		Assert.Equal(27m, rate.HourlyRate);
	}

	private static ShiftPay_BackendContext CreateModelContext()
	{
		var options = new DbContextOptionsBuilder<ShiftPay_BackendContext>()
			.UseCosmos("https://localhost:8081", Convert.ToBase64String(new byte[64]), "ModelOnly")
			.Options;
		return new ShiftPay_BackendContext(options, new ConfigurationBuilder().Build());
	}

	private static WorkInfo LegacyWorkInfo(params decimal[] rates) => new()
	{
		Id = Guid.NewGuid(),
		UserId = "user-a",
		Workplace = "Hospital",
		PayRates = [.. rates]
	};

	private static WorkProfile Profile(string id, params NamedPayRate[] rates) => new()
	{
		Id = id,
		Position = "Nurse",
		Location = "City campus",
		PayRates = [.. rates]
	};

	private static NamedPayRate Rate(string id, string name, decimal amount) => new()
	{
		Id = id,
		Name = name,
		HourlyRate = amount
	};
}
