using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

/// <summary>
/// Tests that verify the Cosmos DB database infrastructure is set up correctly.
/// These tests run first to ensure containers have proper unique key constraints.
/// </summary>
[Collection("CosmosDb")]
public class DatabaseInfrastructureTests
{
	private readonly CosmosDbTestFixture _fixture;

	public DatabaseInfrastructureTests(CosmosDbTestFixture fixture)
	{
		_fixture = fixture;
	}

	#region Container Existence Tests

	[Fact]
	public async Task Database_ShiftsContainer_Exists()
	{
		// Arrange
		await using var context = _fixture.CreateContext();

		// Act & Assert - Should not throw
		var shifts = await context.Shifts.Take(1).ToListAsync();
		Assert.NotNull(shifts);
	}

	[Fact]
	public async Task Database_WorkInfosContainer_Exists()
	{
		// Arrange
		await using var context = _fixture.CreateContext();

		// Act & Assert - Should not throw
		var workInfos = await context.WorkInfos.Take(1).ToListAsync();
		Assert.NotNull(workInfos);
	}

	[Fact]
	public async Task Database_ShiftTemplatesContainer_Exists()
	{
		// Arrange
		await using var context = _fixture.CreateContext();

		// Act & Assert - Should not throw
		var templates = await context.ShiftTemplates.Take(1).ToListAsync();
		Assert.NotNull(templates);
	}

	#endregion

	#region Unique Key Constraint Tests - ShiftTemplates

	[Fact]
	public async Task ShiftTemplates_DuplicateTemplateName_SameUser_ThrowsConflict()
	{
		// Arrange - Unique key on /TemplateName within partition (userId)
		var userId = $"db-unique-test-{Guid.NewGuid():N}";
		await using var context = _fixture.CreateContext();

		var template1 = new ShiftTemplate
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			TemplateName = "Duplicate Template Name",
			Workplace = "Workplace 1",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var template2 = new ShiftTemplate
		{
			Id = Guid.NewGuid(), // Different ID
			UserId = userId,     // Same user (partition)
			TemplateName = "Duplicate Template Name", // Same name - should violate unique key
			Workplace = "Workplace 2",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		context.ShiftTemplates.Add(template1);
		await context.SaveChangesAsync();

		context.ShiftTemplates.Add(template2);

		// Assert - Should throw due to unique key constraint violation
		var exception = await Assert.ThrowsAsync<DbUpdateException>(
			() => context.SaveChangesAsync());

		// Verify it's a Cosmos conflict (409) error - check StatusCode, not message string
		AssertCosmosConflict(exception);
	}

	[Fact]
	public async Task ShiftTemplates_DuplicateTemplateName_DifferentUsers_Succeeds()
	{
		// Arrange - Unique key is scoped to partition, so different users can have same template name
		var userId1 = $"db-unique-user1-{Guid.NewGuid():N}";
		var userId2 = $"db-unique-user2-{Guid.NewGuid():N}";
		await using var context = _fixture.CreateContext();

		var template1 = new ShiftTemplate
		{
			Id = Guid.NewGuid(),
			UserId = userId1,
			TemplateName = "Shared Template Name",
			Workplace = "Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var template2 = new ShiftTemplate
		{
			Id = Guid.NewGuid(),
			UserId = userId2, // Different user (different partition)
			TemplateName = "Shared Template Name", // Same name - OK in different partition
			Workplace = "Workplace",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act & Assert - Should succeed
		context.ShiftTemplates.Add(template1);
		await context.SaveChangesAsync();

		context.ShiftTemplates.Add(template2);
		await context.SaveChangesAsync(); // Should not throw

		// Verify both exist
		var count1 = await context.ShiftTemplates.CountAsync(t => t.UserId == userId1);
		var count2 = await context.ShiftTemplates.CountAsync(t => t.UserId == userId2);
		Assert.Equal(1, count1);
		Assert.Equal(1, count2);
	}

	#endregion

	#region Unique Key Constraint Tests - WorkInfos

	[Fact]
	public async Task WorkInfos_DuplicateWorkplace_SameUser_ThrowsConflict()
	{
		// Arrange - Unique key on /Workplace within partition (userId)
		var userId = $"db-workinfo-unique-{Guid.NewGuid():N}";
		await using var context = _fixture.CreateContext();

		var workInfo1 = new WorkInfo
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Workplace = "Duplicate Workplace",
			PayRates = [15.00m]
		};

		var workInfo2 = new WorkInfo
		{
			Id = Guid.NewGuid(), // Different ID
			UserId = userId,     // Same user (partition)
			Workplace = "Duplicate Workplace", // Same workplace - should violate unique key
			PayRates = [20.00m]
		};

		// Act
		context.WorkInfos.Add(workInfo1);
		await context.SaveChangesAsync();

		context.WorkInfos.Add(workInfo2);

		// Assert - Should throw due to unique key constraint violation
		var exception = await Assert.ThrowsAsync<DbUpdateException>(
			() => context.SaveChangesAsync());

		// Verify it's a Cosmos conflict (409) error - check StatusCode, not message string
		AssertCosmosConflict(exception);
	}

	[Fact]
	public async Task WorkInfos_DuplicateWorkplace_DifferentUsers_Succeeds()
	{
		// Arrange - Unique key is scoped to partition, so different users can have same workplace
		var userId1 = $"db-workinfo-user1-{Guid.NewGuid():N}";
		var userId2 = $"db-workinfo-user2-{Guid.NewGuid():N}";
		await using var context = _fixture.CreateContext();

		var workInfo1 = new WorkInfo
		{
			Id = Guid.NewGuid(),
			UserId = userId1,
			Workplace = "Shared Workplace",
			PayRates = [15.00m]
		};

		var workInfo2 = new WorkInfo
		{
			Id = Guid.NewGuid(),
			UserId = userId2, // Different user (different partition)
			Workplace = "Shared Workplace", // Same workplace - OK in different partition
			PayRates = [20.00m]
		};

		// Act & Assert - Should succeed
		context.WorkInfos.Add(workInfo1);
		await context.SaveChangesAsync();

		context.WorkInfos.Add(workInfo2);
		await context.SaveChangesAsync(); // Should not throw

		// Verify both exist
		var count1 = await context.WorkInfos.CountAsync(w => w.UserId == userId1);
		var count2 = await context.WorkInfos.CountAsync(w => w.UserId == userId2);
		Assert.Equal(1, count1);
		Assert.Equal(1, count2);
	}

	#endregion

	#region Partition Key Tests

	[Fact]
	public async Task Shifts_HierarchicalPartitionKey_WorksCorrectly()
	{
		// Arrange - Shifts use hierarchical partition key: /UserId, /YearMonth, /Day
		var userId = $"db-partition-test-{Guid.NewGuid():N}";
		await using var context = _fixture.CreateContext();

		var shift1 = new Shift
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Workplace = "Test",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 6, 15, 9, 0, 0, DateTimeKind.Utc), // June 15
			EndTime = new DateTime(2024, 6, 15, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var shift2 = new Shift
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Workplace = "Test",
			PayRate = 22.00m,
			StartTime = new DateTime(2024, 7, 20, 9, 0, 0, DateTimeKind.Utc), // July 20 (different partition)
			EndTime = new DateTime(2024, 7, 20, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		context.Shifts.Add(shift1);
		context.Shifts.Add(shift2);
		await context.SaveChangesAsync();

		// Assert - Both should be queryable
		var allShifts = await context.Shifts
			.Where(s => s.UserId == userId)
			.ToListAsync();

		Assert.Equal(2, allShifts.Count);
		Assert.Contains(allShifts, s => s.YearMonth == "2024-06" && s.Day == 15);
		Assert.Contains(allShifts, s => s.YearMonth == "2024-07" && s.Day == 20);
	}

	#endregion

	#region ETag Concurrency Tests

	[Fact]
	public async Task Shifts_ETagConcurrency_IsEnabled()
	{
		// Arrange - This test verifies that EF Core concurrency control with ETags is working
		// The ETag property may not be directly visible but concurrency checks should still work
		var userId = $"db-etag-test-{Guid.NewGuid():N}";
		var shiftId = Guid.NewGuid();
		var startTime = new DateTime(2024, 8, 1, 9, 0, 0, DateTimeKind.Utc);

		// Create the shift
		await using (var createContext = _fixture.CreateContext())
		{
			var shift = new Shift
			{
				Id = shiftId,
				UserId = userId,
				Workplace = "ETag Test",
				PayRate = 20.00m,
				StartTime = startTime,
				EndTime = new DateTime(2024, 8, 1, 17, 0, 0, DateTimeKind.Utc),
				UnpaidBreaks = []
			};

			createContext.Shifts.Add(shift);
			await createContext.SaveChangesAsync();
		}

		// Load in context1 (fresh context)
		await using var context1 = _fixture.CreateContext();
		var loaded1 = await context1.Shifts
			.Where(s => s.Id == shiftId && s.UserId == userId)
			.FirstOrDefaultAsync();
		Assert.NotNull(loaded1);

		// Load in context2
		await using var context2 = _fixture.CreateContext();
		var loaded2 = await context2.Shifts
			.Where(s => s.Id == shiftId && s.UserId == userId)
			.FirstOrDefaultAsync();
		Assert.NotNull(loaded2);

		// Modify in context2 first - this changes the ETag in Cosmos DB
		loaded2.PayRate = 25.00m;
		await context2.SaveChangesAsync();

		// Now try to modify in context1 (has stale ETag from when it was loaded)
		loaded1.PayRate = 30.00m;

		// Assert - Should throw DbUpdateConcurrencyException due to ETag mismatch
		// This proves that UseETagConcurrency() is working
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
			() => context1.SaveChangesAsync());
	}

	#endregion

	#region Helper Methods

	/// <summary>
	/// Asserts that the exception is caused by a Cosmos DB conflict (HTTP 409).
	/// Checks the actual StatusCode rather than relying on brittle string matching.
	/// </summary>
	private static void AssertCosmosConflict(DbUpdateException exception)
	{
		// Walk the exception chain to find CosmosException
		var innerException = exception.InnerException;
		while (innerException != null)
		{
			if (innerException is CosmosException cosmosException)
			{
				Assert.Equal(System.Net.HttpStatusCode.Conflict, cosmosException.StatusCode);
				return;
			}
			innerException = innerException.InnerException;
		}

		// If no CosmosException found, fail with helpful message
		Assert.Fail($"Expected CosmosException with Conflict status, but inner exception was: {exception.InnerException?.GetType().Name ?? "null"}");
	}

	#endregion
}
