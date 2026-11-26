/*
    Time In - Out with breaks: brings a database to what the application expects. Safe to run more than once.

    1. dbo.TimeInTimeOut gets the optional time-out note.
    2. dbo.TimeInTimeOutBreakDetail keeps BreakOut, BreakIn and TotalBreak as text (nvarchar(50)): the application
       stores a break as two full UTC instants ("2026-11-26 12:33:07.4576917") and its length ("00:45:00").
       Some databases still have these three columns as "time" (a time of day, with no date), which fails with
           System.InvalidCastException: Unable to cast object of type 'System.TimeSpan' to type 'System.String'
       Those columns are changed to text here, and any rows already in them are rewritten from a time of day to
       the instant it stood for, using the date of the session they belong to.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- 1. The time-out note.
IF COL_LENGTH('dbo.TimeInTimeOut', 'TimeOutNote') IS NULL
    ALTER TABLE dbo.TimeInTimeOut ADD TimeOutNote nvarchar(500) NULL;

-- 2. The three break columns, when they are still "time".
DECLARE @changed int = 0;
DECLARE @column sysname, @sql nvarchar(max);

DECLARE columnCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT c.name
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.TimeInTimeOutBreakDetail')
      AND c.name IN ('BreakOut', 'BreakIn', 'TotalBreak')
      AND t.name = 'time';

OPEN columnCursor;
FETCH NEXT FROM columnCursor INTO @column;
WHILE @@FETCH_STATUS = 0
BEGIN
    -- A default on the column would block the change of type.
    SET @sql = NULL;
    SELECT @sql = COALESCE(@sql, N'') + N'ALTER TABLE dbo.TimeInTimeOutBreakDetail DROP CONSTRAINT ' + QUOTENAME(d.name) + N';'
    FROM sys.default_constraints d
    JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
    WHERE d.parent_object_id = OBJECT_ID('dbo.TimeInTimeOutBreakDetail') AND c.name = @column;
    IF @sql IS NOT NULL EXEC sp_executesql @sql;

    SET @sql = N'ALTER TABLE dbo.TimeInTimeOutBreakDetail ALTER COLUMN ' + QUOTENAME(@column) + N' nvarchar(50) NULL;';
    EXEC sp_executesql @sql;

    SET @changed += 1;
    FETCH NEXT FROM columnCursor INTO @column;
END
CLOSE columnCursor;
DEALLOCATE columnCursor;

-- 3. Rows written while the columns were "time" now read "13:05:00.0000000": a time of day. They become instants.
--    BreakOut is when the employee stepped out (the break starts), BreakIn when they came back. A time earlier
--    than the session's time-in belongs to the next day; 00:00:00 meant "not set".
--    (Dynamic SQL, because the columns only became text in the batch above.)
EXEC sp_executesql N'
    ;WITH old AS (
        SELECT b.Id,
               CAST(CAST(s.TimeInDate AS date) AS datetime2(0)) AS SessionDay,
               s.TimeIn,
               NULLIF(TRY_CONVERT(time(0), b.BreakOut), ''00:00:00'') AS OutTime,
               NULLIF(TRY_CONVERT(time(0), b.BreakIn), ''00:00:00'') AS InTime
        FROM dbo.TimeInTimeOutBreakDetail b
        LEFT JOIN dbo.TimeInTimeOut s ON s.Id = b.TimeInTimeOutId
        -- Only rows that are still a bare time of day: an instant starts with its date.
        WHERE (b.BreakOut IS NULL OR b.BreakOut NOT LIKE ''[0-9][0-9][0-9][0-9]-%'')
          AND (b.BreakIn IS NULL OR b.BreakIn NOT LIKE ''[0-9][0-9][0-9][0-9]-%'')
          AND (b.BreakOut LIKE ''[0-9][0-9]:[0-9][0-9]:%'' OR b.BreakIn LIKE ''[0-9][0-9]:[0-9][0-9]:%'' OR b.TotalBreak LIKE ''[0-9][0-9]:[0-9][0-9]:%.%'')
    ),
    started AS (
        SELECT Id, OutTime, InTime,
               CASE WHEN OutTime IS NULL OR SessionDay IS NULL THEN NULL
                    ELSE DATEADD(second, DATEDIFF(second, ''00:00:00'', OutTime),
                                 DATEADD(day, CASE WHEN OutTime < CAST(TimeIn AS time(0)) THEN 1 ELSE 0 END, SessionDay))
               END AS StartAt
        FROM old
    ),
    ended AS (
        SELECT Id, StartAt,
               CASE WHEN StartAt IS NULL OR InTime IS NULL THEN NULL
                    ELSE DATEADD(second, DATEDIFF(second, ''00:00:00'', InTime),
                                 DATEADD(day, CASE WHEN InTime < OutTime THEN 1 ELSE 0 END, CAST(CAST(StartAt AS date) AS datetime2(0))))
               END AS EndAt
        FROM started
    )
    UPDATE b
    SET BreakOut = CONVERT(varchar(27), CAST(e.StartAt AS datetime2(7)), 121),
        BreakIn = CONVERT(varchar(27), CAST(e.EndAt AS datetime2(7)), 121),
        TotalBreak = CASE WHEN e.EndAt IS NULL THEN NULL
                          ELSE CONVERT(varchar(8), DATEADD(second, DATEDIFF(second, e.StartAt, e.EndAt), CAST(''00:00:00'' AS time(0))), 108)
                     END
    FROM dbo.TimeInTimeOutBreakDetail b
    JOIN ended e ON e.Id = b.Id;

    SELECT @@ROWCOUNT AS BreakRowsRewritten;';

COMMIT TRANSACTION;

SELECT @changed AS BreakColumnsChangedToText;

-- What the application now expects: three nvarchar columns, and TimeOutNote.
SELECT c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE (c.TABLE_NAME = 'TimeInTimeOutBreakDetail' AND c.COLUMN_NAME IN ('BreakOut', 'BreakIn', 'TotalBreak'))
   OR (c.TABLE_NAME = 'TimeInTimeOut' AND c.COLUMN_NAME = 'TimeOutNote')
ORDER BY c.TABLE_NAME, c.COLUMN_NAME;
