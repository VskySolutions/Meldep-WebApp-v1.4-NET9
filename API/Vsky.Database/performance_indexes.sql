/*
    Performance indexes (20 Nov 2026).

    Where they come from: SQL Server's own missing-index suggestions on a restored copy of the live
    database, after every list and dropdown endpoint of the API had been called. Each index answers a
    filter the application uses on almost every query (SiteId + Deleted, or a parent id + Deleted) on
    one of the larger tables.

    How to run:
      - Safe to run more than once: an index that already exists is skipped.
      - SQL Server Express cannot build an index online. While one is being built, writes to that table
        wait. On the copy of live data the whole script took well under a minute, but run it outside
        working hours.
      - Nothing is dropped or changed; the script only adds indexes and refreshes statistics.

    To undo: DROP INDEX <name> ON <table>; for the names below.
*/
SET NOCOUNT ON;

DECLARE @indexes TABLE (name sysname, tbl sysname, sql nvarchar(max));
INSERT INTO @indexes (name, tbl, sql) VALUES
-- Timesheet lines: totals of hours per task / project read every line that is not deleted.
(N'IX_TimesheetLines_Deleted_Hours', N'TimesheetLines',
 N'CREATE NONCLUSTERED INDEX IX_TimesheetLines_Deleted_Hours ON dbo.TimesheetLines (Deleted) INCLUDE (TimesheetId, ProjectId, ProjectModuleId, ProjectTaskId, Hours)'),
-- Timesheets of a site for a day or a range of days.
(N'IX_Timesheet_SiteId_TimesheetDate', N'Timesheet',
 N'CREATE NONCLUSTERED INDEX IX_Timesheet_SiteId_TimesheetDate ON dbo.Timesheet (SiteId, TimesheetDate) INCLUDE (EmployeeId, CreatedById, Deleted)'),
-- Activities of a site (lists, dropdowns) and of a project (estimates).
(N'IX_ProjectActivities_SiteId_Deleted', N'ProjectActivities',
 N'CREATE NONCLUSTERED INDEX IX_ProjectActivities_SiteId_Deleted ON dbo.ProjectActivities (SiteId, Deleted) INCLUDE (Name, ProjectId, ProjectModuleId, TaskId, ActivityStatusId)'),
(N'IX_ProjectActivities_ProjectId_Deleted', N'ProjectActivities',
 N'CREATE NONCLUSTERED INDEX IX_ProjectActivities_ProjectId_Deleted ON dbo.ProjectActivities (ProjectId, Deleted) INCLUDE (EstimateHours, ProjectModuleId, TaskId)'),
-- Tasks of a requirement, and the tasks of a site that have not been moved.
(N'IX_ProjectTasks_RequirementId_Deleted', N'ProjectTasks',
 N'CREATE NONCLUSTERED INDEX IX_ProjectTasks_RequirementId_Deleted ON dbo.ProjectTasks (RequirementId, Deleted)'),
(N'IX_ProjectTasks_SiteId_IsMoved_Deleted', N'ProjectTasks',
 N'CREATE NONCLUSTERED INDEX IX_ProjectTasks_SiteId_IsMoved_Deleted ON dbo.ProjectTasks (SiteId, IsMoved, Deleted) INCLUDE (ProjectId, ProjectModuleId, StatusId, ProjectTaskNumber, Name)'),
-- Test cases, issues and requirements of a site.
(N'IX_TestCase_SiteId_Deleted', N'TestCase',
 N'CREATE NONCLUSTERED INDEX IX_TestCase_SiteId_Deleted ON dbo.TestCase (SiteId, Deleted) INCLUDE (Name, PlanId, ProjectId)'),
(N'IX_Issue_SiteId_Deleted', N'Issue',
 N'CREATE NONCLUSTERED INDEX IX_Issue_SiteId_Deleted ON dbo.Issue (SiteId, Deleted) INCLUDE (ProjectId, StatusId)'),
(N'IX_Requirement_SiteId_Deleted', N'Requirement',
 N'CREATE NONCLUSTERED INDEX IX_Requirement_SiteId_Deleted ON dbo.Requirement (SiteId, Deleted) INCLUDE (ProjectId, ProjectModuleId, StatusId, PriorityId, PlannedEndDate, UpdatedOnUtc)'),
-- The unread-notification count, asked for on every page.
(N'IX_NotificationDetails_ToUserId_IsRead', N'NotificationDetails',
 N'CREATE NONCLUSTERED INDEX IX_NotificationDetails_ToUserId_IsRead ON dbo.NotificationDetails (ToUserId, IsRead) INCLUDE (NotificationId)'),
(N'IX_NotificationDetails_NotificationId', N'NotificationDetails',
 N'CREATE NONCLUSTERED INDEX IX_NotificationDetails_NotificationId ON dbo.NotificationDetails (NotificationId)'),
-- Menu permissions of a site, read when the menu is built.
(N'IX_SitesModulesMenusPermissions_SiteId_Deleted', N'SitesModulesMenusPermissions',
 N'CREATE NONCLUSTERED INDEX IX_SitesModulesMenusPermissions_SiteId_Deleted ON dbo.SitesModulesMenusPermissions (SiteId, Deleted) INCLUDE (SiteRoleId, SiteModuleMenuId, IsShowMenu, IsManage, IsView)'),
-- Planner lines, movement register lines and change-log rows by their parent.
(N'IX_DailyPlannerLines_Deleted', N'DailyPlannerLines',
 N'CREATE NONCLUSTERED INDEX IX_DailyPlannerLines_Deleted ON dbo.DailyPlannerLines (Deleted) INCLUDE (DailyPlannerId)'),
(N'IX_MovementRegisterDetails_MomentRegisterId_Deleted', N'MovementRegisterDetails',
 N'CREATE NONCLUSTERED INDEX IX_MovementRegisterDetails_MomentRegisterId_Deleted ON dbo.MovementRegisterDetails (MomentRegisterId, Deleted)'),
(N'IX_SitesModifiedLogs_SubModuleId_Deleted', N'SitesModifiedLogs',
 N'CREATE NONCLUSTERED INDEX IX_SitesModifiedLogs_SubModuleId_Deleted ON dbo.SitesModifiedLogs (SubModuleId, Deleted)');

DECLARE @name sysname, @tbl sysname, @sql nvarchar(max), @started datetime2;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT name, tbl, sql FROM @indexes;
OPEN c;
FETCH NEXT FROM c INTO @name, @tbl, @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @tbl, N'U') IS NULL
        PRINT 'skipped (no such table): ' + @name;
    ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.' + @tbl) AND name = @name)
        PRINT 'already there: ' + @name;
    ELSE
    BEGIN
        SET @started = SYSDATETIME();
        EXEC sys.sp_executesql @sql;
        PRINT 'created: ' + @name + ' (' + CAST(DATEDIFF(MILLISECOND, @started, SYSDATETIME()) AS varchar(12)) + ' ms)';
    END
    FETCH NEXT FROM c INTO @name, @tbl, @sql;
END
CLOSE c;
DEALLOCATE c;

-- Fresh statistics on the tables touched, so the new indexes are costed on current data.
DECLARE t CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT tbl FROM @indexes WHERE OBJECT_ID(N'dbo.' + tbl, N'U') IS NOT NULL;
OPEN t;
FETCH NEXT FROM t INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'UPDATE STATISTICS dbo.' + QUOTENAME(@tbl) + N';';
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM t INTO @tbl;
END
CLOSE t;
DEALLOCATE t;
PRINT 'statistics updated';
