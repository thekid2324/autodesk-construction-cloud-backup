using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.Reflection;
using ACC.ApiClient;
using FluentAssertions;
using Library.Logger;
using Polly.Retry;
using Xunit;

namespace ACC.Backup.UnitTests;

public class BackupUnitTests
{
    private const string BackupDirectory = @"C:\ACCBackup";
    private const string ClientId = "DRO4zxzt71HCkL34cn2tAUSRS0OQGaRT";
    private const string ClientSecret = "tFRHKhuIrGQUi5d3";
    private const string AccountId = "9b3cc923-d920-4fee-ae91-5e9c8e4040rb";
    private const string Projects = "Test Project, 7468066c-53e6-4acf-8086-6b5fce0048d6";
    private const int BackupsToRotate = 5;
    private const string SmtpHost = "smtp.yourlocalnetwork.com";
    private const int SmtpPort = 587;
    private const string SmtpUsername = "username";
    private const string SmtpPassword = "password";
    private const string SmtpFromAddress = "noreply@yourlocalnetwork.com";
    private const string SmtpFromName = "ACCBackup";
    private const string SmtpToAddress = "example@example.com";
    private const int RetryAttempts = 5;
    private const int InitialRetryInSeconds = 3;
    private const int MaxDegreeOfParallelism = 4;

    private readonly string[] _args =
    {
        "--backupdirectory", BackupDirectory, "--clientid", ClientId, "--clientsecret", ClientSecret, "--accountid",
        AccountId, "--hubid", AccountId, "--projectstobackup", Projects, "--projectstoexclude", Projects,
        "--backupstorotate",
        $"{BackupsToRotate}", "--smtphost", SmtpHost, "--smtpport", $"{SmtpPort}", "--smtpusername", SmtpUsername,
        "--smtppassword",
        SmtpPassword, "--smtpfromaddress", SmtpFromAddress, "--smtpenablessl", "--smtpfromname",
        SmtpFromName, "--smtptoaddress", SmtpToAddress, "--debuglogging", "--tracelogging", "--dryrun",
        "--retryattempts", $"{RetryAttempts}", "--initialretryinseconds", $"{InitialRetryInSeconds}",
        "--maxdegreeofparallelism", $"{MaxDegreeOfParallelism}"
    };

    private readonly List<string> _projectsList = new() { "Test Project", "7468066c-53e6-4acf-8086-6b5fce0048d6" };

    [Fact]
    public void BackupConfiguration_Should_Parse_CommandLineArgs()
    {
        // Act
        var sut = new BackupConfiguration(_args);

        // Assert   
        sut.BackupDirectory.Should().Be(@$"{BackupDirectory}\");
        sut.ClientId.Should().Be(ClientId);
        sut.ClientSecret.Should().Be(ClientSecret);
        sut.AccountId.Should().Be(AccountId);
        sut.HubId.Should().Be(AccountId);
        sut.ProjectsToBackup.Count.Should().Be(2);
        sut.ProjectsToBackup[0].Should().Be(_projectsList[0]);
        sut.ProjectsToBackup[1].Should().Be(_projectsList[1]);
        sut.ProjectsToExclude.Count.Should().Be(2);
        sut.ProjectsToExclude[0].Should().Be(_projectsList[0]);
        sut.ProjectsToExclude[1].Should().Be(_projectsList[1]);
        sut.BackupsToRotate.Should().Be(5);
        sut.SmtpHost.Should().Be(SmtpHost);
        sut.SmtpPort.Should().Be(SmtpPort);
        sut.SmtpUsername.Should().Be(SmtpUsername);
        sut.SmtpPassword.Should().Be(SmtpPassword);
        sut.SmtpFromAddress.Should().Be(SmtpFromAddress);
        sut.SmtpFromName.Should().Be(SmtpFromName);
        sut.SmtpToAddress.Should().Be(SmtpToAddress);
        sut.DebugLogging.Should().BeTrue();
        sut.TraceLogging.Should().BeTrue();
        sut.DryRun.Should().BeTrue();
        sut.RetryAttempts.Should().Be(RetryAttempts);
        sut.InitialRetryInSeconds.Should().Be(InitialRetryInSeconds);
        sut.MaxDegreeOfParallelism.Should().Be(MaxDegreeOfParallelism);
    }

    [Fact]
    public void Backup_Should_Map_BackupConfiguration_to_ApiClient()
    {
        // Arrange
        var backupConfiguration = new BackupConfiguration(_args);

        // Act
        var sut = new Backup(backupConfiguration);

        // Assert
        sut.ApiClient.Config.AccountId.Should().Be(AccountId);
        sut.ApiClient.Config.ClientId.Should().Be(ClientId);
        sut.ApiClient.Config.ClientSecret.Should().Be(ClientSecret);
        sut.ApiClient.Config.HubId.Should().Be(AccountId);
        sut.ApiClient.Config.DryRun.Should().BeTrue();
        sut.ApiClient.Config.RetryAttempts.Should().Be(RetryAttempts);
        sut.ApiClient.Config.InitialRetryInSeconds.Should().Be(InitialRetryInSeconds);
        sut.ApiClient.Config.MaxDegreeOfParallelism.Should().Be(MaxDegreeOfParallelism);
        sut.ApiClient.Config.RetryPolicy.Should().BeOfType<AsyncRetryPolicy>();
        sut.ApiClient.Config.HttpClient.Should().BeOfType<HttpClient>();
        sut.ApiClient.Config.Logger.Should().BeOfType<NLogLogger>();
        sut.ApiClient.Config.Logger.Should().NotBeNull();
        sut.ApiClient.Config.Logger!.Config.LogLevel.Should().Be(LogLevel.Trace);
    }

    [Fact]
    public void GetBackupSummaryLine_Should_Report_Correct_Counts()
    {
        // Arrange
        ApiClient.ApiClient apiClient = TwoLeggedApiClient
            .Configure()
            .WithClientId("id")
            .AndClientSecret("secret")
            .ForAccount("account")
            .Create();

        var rootFolder = new ACC.ApiClient.Entities.Folder(apiClient)
        {
            FolderId = "root",
            ProjectId = "proj",
            ParentFolderId = "parent"
        };

        var subfolderCreated = new ACC.ApiClient.Entities.Folder(apiClient)
        {
            FolderId = "sub1",
            ProjectId = "proj",
            ParentFolderId = "root",
            ParentFolder = rootFolder,
            DirectoryInfo = new DirectoryInfo(Path.GetTempPath())
        };

        var subfolderNotCreated = new ACC.ApiClient.Entities.Folder(apiClient)
        {
            FolderId = "sub2",
            ProjectId = "proj",
            ParentFolderId = "root",
            ParentFolder = rootFolder
        };

        rootFolder.Subfolders = new List<ACC.ApiClient.Entities.Folder>
        {
            subfolderCreated,
            subfolderNotCreated
        };

        var fileDownloaded = new ACC.ApiClient.Entities.File
        {
            FileId = "f1",
            ProjectId = "proj",
            ParentFolder = rootFolder,
            StorageSize = 1024 * 1024,
            FileInfo = new FileInfo(Path.GetTempFileName()),
            FileSizeOnDisk = 1024 * 1024
        };

        var fileNotDownloaded = new ACC.ApiClient.Entities.File
        {
            FileId = "f2",
            ProjectId = "proj",
            ParentFolder = rootFolder,
            StorageSize = 1024 * 1024
        };

        rootFolder.Files = new List<ACC.ApiClient.Entities.File>
        {
            fileDownloaded,
            fileNotDownloaded
        };

        var subFileDownloaded = new ACC.ApiClient.Entities.File
        {
            FileId = "f3",
            ProjectId = "proj",
            ParentFolder = subfolderCreated,
            StorageSize = 1024 * 1024,
            FileInfo = new FileInfo(Path.GetTempFileName()),
            FileSizeOnDisk = 1024 * 1024
        };

        subfolderCreated.Files = new List<ACC.ApiClient.Entities.File> { subFileDownloaded };

        var project = new ACC.Backup.Entities.ProjectBackup(apiClient)
        {
            ProjectId = "proj",
            Name = "Test",
            BackupStartedAt = DateTime.Now,
            BackupFinishedAt = DateTime.Now.AddMinutes(1),
            RootFolder = rootFolder
        };

        // Act
        MethodInfo? method = typeof(Backup).GetMethod("GetBackupSummaryLine", BindingFlags.NonPublic | BindingFlags.Static);
        string? summary = method?.Invoke(null, new object[] { project }) as string;

        // Assert
        summary.Should().NotBeNull();
        summary!.Should().Contain("2/3 files backed up in 1/2 folders");
    }}