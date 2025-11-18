# NaturalCron Integration - Work in Progress

## Current State (Paused for Library Work)

### Completed:
1. ✅ **Migration Created**: `20251114070005_RenameBackgroundJobConfigCronExpressionToSchedule.cs`
   - Renames `CronExpression` → `Schedule` in JSONB column using PostgreSQL `jsonb_object_agg`
   - Migration tested successfully against PostgreSQL
   - Existing cron values preserved (e.g., `"0 0 0 * * ?"` now in `Schedule` property)

2. ✅ **Code Updated**: All references changed from `CronExpression` to `Schedule`
   - `BackgroundJobConfig.cs` - Model property renamed with updated documentation
   - `BackgroundJobConfigService.cs` - All references updated, default configs use natural language
   - `BackgroundJobs.razor` - UI references updated, old `_editCronExpression` field removed
   - `ServiceCollectionExtensions.cs` - Comment updated

3. ✅ **NaturalCron References Added**:
   - Main app: `dotnet add TelegramGroupsAdmin/TelegramGroupsAdmin.csproj reference NaturalCron/NaturalCron.csproj`
   - BackgroundJobs: `dotnet add TelegramGroupsAdmin.BackgroundJobs/TelegramGroupsAdmin.BackgroundJobs.csproj reference NaturalCron/NaturalCron.csproj`

4. ✅ **DI Registration**: `Program.cs` line 117: `builder.Services.AddNaturalCron();`

5. ✅ **NaturalCron Formatter Added** (Just Created):
   - `NaturalCron/Abstractions/IScheduleFormatter.cs` - Interface for bidirectional conversion
   - `NaturalCron/Formatting/NaturalLanguageFormatter.cs` - Implementation
   - Registered in `ServiceCollectionExtensions.AddNaturalCron()`

### Default Job Schedules (Natural Language):
- ScheduledBackup: `"1d at 2am"`
- MessageCleanup: `"1d"` (midnight)
- UserPhotoRefresh: `"1d at 3am"`
- BlocklistSync: `"1w on sunday at 3am"`
- DatabaseMaintenance: `"1w on sunday at 4am"`
- ChatHealthCheck: `"30m"`

### Next Steps (When We Resume):
1. Update `QuartzSchedulingSyncService.cs`:
   - Add `IScheduleFormatter` to constructor parameters
   - Update `CreateOrUpdateTriggerAsync()` to:
     - Parse natural language first (hot path) → return early
     - Fall back to legacy cron parsing
     - Use `IScheduleFormatter.Format()` to convert legacy cron to natural language
     - Save converted natural language back to database

2. Test the full integration:
   - Verify existing cron schedules auto-convert to natural language on first run
   - Verify natural language schedules work correctly
   - Check Quartz creates proper triggers (CronSchedule vs CalendarIntervalSchedule)

### Files Modified:
- `TelegramGroupsAdmin.Core/Models/BackgroundJobConfig.cs`
- `TelegramGroupsAdmin.BackgroundJobs/Services/BackgroundJobConfigService.cs`
- `TelegramGroupsAdmin.BackgroundJobs/Services/QuartzSchedulingSyncService.cs` (reverted - needs proper update)
- `TelegramGroupsAdmin.BackgroundJobs/Extensions/ServiceCollectionExtensions.cs`
- `TelegramGroupsAdmin/Components/Shared/Settings/BackgroundJobs.razor`
- `TelegramGroupsAdmin/Program.cs`
- `TelegramGroupsAdmin.Data/Migrations/20251114070005_RenameBackgroundJobConfigCronExpressionToSchedule.cs` (new)
- `NaturalCron/Abstractions/IScheduleFormatter.cs` (new)
- `NaturalCron/Formatting/NaturalLanguageFormatter.cs` (new)
- `NaturalCron/DependencyInjection/ServiceCollectionExtensions.cs`

## Pausing Point: Library Validation Needed

User wants to pivot back to NaturalCron library to ensure everything is correct before integrating into the application.
