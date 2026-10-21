/*
  Flowza: create the development and test databases (run once, by the owner or a DBA).

  How to run (SQLCMD mode is needed for the :setvar line):
    - SSMS: Query menu > SQLCMD Mode, then Execute.
    - Command line:  sqlcmd -S . -E -i create-databases.sql
      (-E = Windows login of the person running it; that person needs permission to create databases.)

  Before running: change AppLogin below to the SQL login the application will use.
  This script does NOT create the login and contains NO password. Create the login separately, e.g.
      CREATE LOGIN [flowza_app] WITH PASSWORD = '<strong password>', CHECK_POLICY = ON;
  and keep that password only in .NET user secrets (never in git or chat).

  Safe to run twice: existing databases and users are left as they are.
  Compatibility level 150 = SQL Server 2019, so nothing newer is used by accident (constitution 6.3).
*/
:setvar AppLogin "flowza_app"

SET NOCOUNT ON;

DECLARE @databases TABLE (Name sysname);
INSERT INTO @databases (Name) VALUES
    (N'flowza_identity'), (N'flowza_design'), (N'flowza_runtime'), (N'flowza_worker'), (N'flowza_messaging'),
    (N'flowza_test_identity'), (N'flowza_test_design'), (N'flowza_test_runtime'), (N'flowza_test_worker'), (N'flowza_test_messaging');

IF SUSER_ID(N'$(AppLogin)') IS NULL
BEGIN
    RAISERROR(N'Login $(AppLogin) does not exist. Create it first (see the comment at the top).', 16, 1);
    RETURN;
END;

DECLARE @name sysname, @sql nvarchar(max);
DECLARE db_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @databases;
OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DB_ID(@name) IS NULL
    BEGIN
        SET @sql = N'CREATE DATABASE ' + QUOTENAME(@name) + N';';
        EXEC (@sql);
        PRINT N'Created database ' + @name;
    END
    ELSE
        PRINT N'Database ' + @name + N' already exists';

    SET @sql = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET COMPATIBILITY_LEVEL = 150;'
             + N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;';
    EXEC (@sql);

    SET @sql = N'USE ' + QUOTENAME(@name) + N';
        IF USER_ID(N''$(AppLogin)'') IS NULL
            CREATE USER ' + QUOTENAME(N'$(AppLogin)') + N' FOR LOGIN ' + QUOTENAME(N'$(AppLogin)') + N';
        ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(N'$(AppLogin)') + N';';
    EXEC (@sql);

    FETCH NEXT FROM db_cursor INTO @name;
END;
CLOSE db_cursor;
DEALLOCATE db_cursor;

PRINT N'Done. Login $(AppLogin) is db_owner on all Flowza databases.';
