using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Vsky.Api.Services.RequirementExport;
using Vsky.Models;
using Xunit;
using static Vsky.Api.Tests.RequirementExport.ExportFixture;

namespace Vsky.Api.Tests.RequirementExport
{
    public class RequirementExportServiceTests
    {
        private static readonly string[] AllModuleSheets = { "Requirement Log", "Requirement files", "Requirement Status tracking", "Tasks", "Test Cases", "Issues", "Timesheets", "Q&A", "Action Items", "Mappings", "Notes" };

        private static async Task<OfficeOpenXml.ExcelPackage> Export(ExportFixture fx, string id = ReqA)
        {
            var file = await fx.Service().ExportAsync(SiteId, UserId, id);
            return Open(file.Content);
        }

        [Fact]
        public async Task Workbook_starts_with_requirement_info_then_every_named_module_in_order()
        {
            using var package = await Export(new ExportFixture());

            var names = package.Workbook.Worksheets.Select(w => w.Name).ToList();
            Assert.Equal(new[] { "Requirement Info" }.Concat(AllModuleSheets), names);
        }

        [Fact]
        public async Task Requirement_info_lists_basic_fields_with_readable_labels()
        {
            using var package = await Export(new ExportFixture());
            var info = package.Workbook.Worksheets[0];

            Assert.Equal("Field", info.Cells[1, 1].Value);
            Assert.Equal("Value", info.Cells[1, 2].Value);
            var map = Enumerable.Range(2, info.Dimension.End.Row - 1)
                .ToDictionary(r => (string)info.Cells[r, 1].Value, r => info.Cells[r, 2].Value);

            Assert.Equal(42d, Convert.ToDouble(map["Requirement #"]));
            Assert.Equal("Login revamp", map["Title"]);
            Assert.Equal("Alpha", map["Project"]);
            Assert.Equal("Auth", map["Module"]);
            Assert.Equal("New", map["Status"]);
            Assert.Equal("Ria Owner", map["Requirement Owner"]);
            Assert.Equal(new DateTime(2026, 11, 1), DateTime.FromOADate(Convert.ToDouble(map["Identified Date"])));
            Assert.Equal("First line\n\n- one\n- two", map["Description"]);
            Assert.Null(map["Close Date"]);
        }

        [Fact]
        public async Task Requirement_info_labels_and_values_are_all_left_aligned()
        {
            using var package = await Export(new ExportFixture());
            var info = package.Workbook.Worksheets[0];

            for (var r = 1; r <= info.Dimension.End.Row; r++)
                for (var c = 1; c <= 2; c++)
                    Assert.Equal(OfficeOpenXml.Style.ExcelHorizontalAlignment.Left, info.Cells[r, c].Style.HorizontalAlignment);
        }

        [Fact]
        public async Task Every_sheet_contains_only_the_selected_requirements_records()
        {
            using var package = await Export(new ExportFixture());

            var text = AllText(package).ToList();
            Assert.DoesNotContain(text, t => t.StartsWith("B-") || t.StartsWith("X-"));
            Assert.Contains("A-Build login", text);
            Assert.Contains("A-Requirement note", text);
        }

        [Fact]
        public async Task Module_sheets_include_all_matching_records_but_not_deleted_ones()
        {
            using var package = await Export(new ExportFixture());
            var tasks = package.Workbook.Worksheets["Tasks"];

            Assert.Equal(new[] { "A-Design login", "A-Build login" }, Column(tasks, "Task Name")); // ordered by number
            var timesheets = package.Workbook.Worksheets["Timesheets"];
            Assert.Equal(new[] { "A-worked" }, Column(timesheets, "Description"));
        }

        [Fact]
        public async Task Requirement_status_tracking_sheet_includes_every_non_deleted_row_newest_first()
        {
            var fx = new ExportFixture();
            fx.Q.StatusTrackingList.Add(new RequirementStatusTracking
            {
                Id = "A-rst-2", RequirementId = ReqA, ProjectId = ProjectId, TrackingDate = new DateTime(2026, 11, 9),
                CurrentPercent = 75m, TimeSpentAsOfToday = 5.5m, EstimatedRemainingTime = 0.3m, EstimatedTimeForCompletion = 1m,
                Scope = "In testing", Roadblock = "<p>Waiting on <b>QA</b></p>", CreatedOnUtc = new DateTime(2026, 11, 9, 10, 0, 0)
            });

            using var package = await Export(fx);
            var sheet = package.Workbook.Worksheets["Requirement Status tracking"];

            Assert.Equal(3, package.Workbook.Worksheets.Select(w => w.Name).ToList().IndexOf("Requirement Status tracking")); // after Requirement files
            Assert.Equal(new[] { "In testing", "A-In dev" }, Column(sheet, "Current Status")); // newest tracking date first
            Assert.Equal(new[] { "Waiting on QA", "" }, Column(sheet, "Roadblock"));
            Assert.Equal(new[] { "75", "40" }, Column(sheet, "Current Completion (%)"));

            // Hour fields are HH:MM durations (fraction of a day): 5.5h = 330 min, 2.6h = 156 min, 0.3h = 18 min.
            double Minutes(string header, int row)
            {
                var col = Enumerable.Range(1, sheet.Dimension.End.Column).First(c => (string)sheet.Cells[1, c].Value == header);
                Assert.Equal("[hh]:mm", sheet.Cells[row, col].Style.Numberformat.Format);
                return Math.Round((double)sheet.Cells[row, col].Value * 1440);
            }
            Assert.Equal(330, Minutes("Time Spent as of Today (Hrs)", 2));
            Assert.Equal(156, Minutes("Time Spent as of Today (Hrs)", 3));
            Assert.Equal(18, Minutes("Estimated Remaining Time (Hrs)", 2));
            Assert.DoesNotContain(AllText(package), t => t.Contains("deleted tracking"));
        }

        [Fact]
        public async Task Requirement_files_sheet_includes_every_non_deleted_file_of_the_requirement()
        {
            var fx = new ExportFixture();
            fx.Q.FileList.Add(new FilePathDetails
            {
                Id = "A-file-2", ModuleId = ReqA, FilePath = "=HYPERLINK(\"http://evil\")", FileName = "A-formula-looking", CreatedOnUtc = new DateTime(2026, 11, 5)
            });

            using var package = await Export(fx);
            var sheet = package.Workbook.Worksheets["Requirement files"];

            Assert.Equal(2, package.Workbook.Worksheets.Select(w => w.Name).ToList().IndexOf("Requirement files")); // after Requirement Log
            Assert.Equal(new[] { "A-https://docs/spec", "=HYPERLINK(\"http://evil\")" }, Column(sheet, "File Path")); // oldest first
            Assert.Equal(new[] { "A-spec", "A-formula-looking" }, Column(sheet, "File Name"));
            Assert.Equal(new[] { "A-refer this", "" }, Column(sheet, "Notes"));
            Assert.True(string.IsNullOrEmpty(sheet.Cells[3, 1].Formula)); // stored as literal text, not a formula
            Assert.DoesNotContain(AllText(package), t => t.Contains("deleted file"));
        }

        [Fact]
        public async Task Requirement_log_sheet_includes_every_non_deleted_change_log_newest_first()
        {
            var fx = new ExportFixture();
            fx.Q.ChangeLogList.Add(new RequirementChangeLog
            {
                Id = "A-log-2", RequirementId = ReqA, RequirementLogDate = new DateTime(2026, 11, 6), RequirementName = "A-Login",
                Description = "<p>Added <b>SSO</b></p>", Employee = Emp("Cara", "Lee"), CreatedOnUtc = new DateTime(2026, 11, 6, 14, 0, 0)
            });
            fx.Q.ChangeLogList.Add(new RequirementChangeLog
            {
                Id = "A-log-3", RequirementId = ReqA, RequirementLogDate = new DateTime(2026, 11, 7), RequirementName = "A-Login",
                Description = "Dropped MFA", CreatedOnUtc = new DateTime(2026, 11, 7, 8, 0, 0)
            });

            using var package = await Export(fx);
            var sheet = package.Workbook.Worksheets["Requirement Log"];

            Assert.Equal(1, package.Workbook.Worksheets.Select(w => w.Name).ToList().IndexOf("Requirement Log")); // right after Requirement Info
            Assert.Equal(new[] { "Dropped MFA", "Added SSO", "A-Changed scope" }, Column(sheet, "Description"));
            Assert.Equal(new[] { "", "Cara Lee", "" }, Column(sheet, "Change By"));
            Assert.DoesNotContain(AllText(package), t => t.Contains("deleted log"));
        }

        [Fact]
        public async Task Timesheets_sheet_exports_every_live_line_for_a_date_but_not_superseded_ones()
        {
            var fx = new ExportFixture(populateA: false);
            fx.Q.TaskList.Add(new ProjectTask { Id = "t1", SiteId = SiteId, RequirementId = ReqA, ProjectTaskNumber = 1, Name = "Build" });
            fx.Q.TaskList.Add(new ProjectTask { Id = "t2", SiteId = SiteId, RequirementId = ReqA, ProjectTaskNumber = 2, Name = "Test" });
            var date = new DateTime(2026, 11, 11);
            // Re-saving a day soft-deletes the old header but leaves its lines un-deleted (superseded versions).
            fx.Q.TimesheetList.Add(new Timesheet { Id = "ts-old", SiteId = SiteId, TimesheetDate = date, Deleted = true });
            fx.Q.TimesheetList.Add(new Timesheet { Id = "ts-live", SiteId = SiteId, TimesheetDate = date });
            fx.Q.TimesheetLineList.Add(new TimesheetLines { Id = "old", TimesheetId = "ts-old", ProjectTaskId = "t1", Hours = 0.5m, Description = "old version", CreatedOnUtc = date.AddHours(9) });
            fx.Q.TimesheetLineList.Add(new TimesheetLines { Id = "l1", TimesheetId = "ts-live", ProjectTaskId = "t1", Hours = 0.25m, Description = "first", CreatedOnUtc = date.AddHours(10) });
            fx.Q.TimesheetLineList.Add(new TimesheetLines { Id = "l2", TimesheetId = "ts-live", ProjectTaskId = "t2", Hours = 0.5m, Description = "second", CreatedOnUtc = date.AddHours(11) });
            fx.Q.TimesheetLineList.Add(new TimesheetLines { Id = "l3", TimesheetId = "ts-live", ProjectTaskId = "t1", Hours = 1m, Description = "third", CreatedOnUtc = date.AddHours(12) });
            fx.Q.TimesheetLineList.Add(new TimesheetLines { Id = "l-del", TimesheetId = "ts-live", ProjectTaskId = "t1", Hours = 8m, Description = "deleted line", Deleted = true });

            using var package = await Export(fx);
            var sheet = package.Workbook.Worksheets["Timesheets"];

            Assert.Equal(new[] { "first", "second", "third" }, Column(sheet, "Description"));
            // Hours are Excel durations (fraction of a day): 0.25h + 0.5h + 1h = 105 minutes.
            Assert.Equal(105, Math.Round(Column(sheet, "Hours").Sum(h => double.Parse(h, System.Globalization.CultureInfo.InvariantCulture)) * 1440));
        }

        [Theory]
        [InlineData(2.6, 156, "02:36")]   // user entered 02:36
        [InlineData(0.3, 18, "00:18")]    // user entered 00:18
        [InlineData(0.33, 20, "00:20")]   // stored with 2 decimals; rounds to the minute like HoursConverter
        [InlineData(1.0, 60, "01:00")]
        public async Task Hours_are_written_as_hh_mm_durations_not_decimals(double storedHours, int expectedMinutes, string appDisplay)
        {
            Assert.Equal(appDisplay, Vsky.Core.HoursConverter.ConvertDecimalHoursToTime((decimal)storedHours));

            var fx = new ExportFixture(populateA: false);
            fx.Q.TaskList.Add(new ProjectTask { Id = "t1", SiteId = SiteId, RequirementId = ReqA, ProjectTaskNumber = 1, Name = "Build" });
            fx.Q.TimesheetList.Add(new Timesheet { Id = "ts", SiteId = SiteId, TimesheetDate = new DateTime(2026, 11, 11) });
            fx.Q.TimesheetLineList.Add(new TimesheetLines { Id = "l", TimesheetId = "ts", ProjectTaskId = "t1", Hours = (decimal)storedHours, BillableHours = (decimal)storedHours, Description = "x" });

            using var package = await Export(fx);
            var ws = package.Workbook.Worksheets["Timesheets"];
            var col = Enumerable.Range(1, ws.Dimension.End.Column).First(c => (string)ws.Cells[1, c].Value == "Hours");
            var cell = ws.Cells[2, col];

            Assert.Equal("[hh]:mm", cell.Style.Numberformat.Format);
            // (EPPlus's cell.Text formatter mis-renders "[hh]:mm"; Excel shows appDisplay. Assert the value.)
            Assert.Equal(expectedMinutes, Math.Round((double)cell.Value * 1440));
        }

        [Fact]
        public async Task Questions_sheet_includes_every_non_deleted_answer_log_newest_first()
        {
            var fx = new ExportFixture();
            var question = fx.Q.QuestionList.Single(x => x.Id == "A-qa");
            question.Description = "<p>Original description</p>";
            question.ProjectQuestionsAnswersResponseLog.Add(new ProjectQuestionsAnswersResponseLog { Id = "log-2", Description = "<p>Second answer</p>", CreatedOnUtc = new DateTime(2026, 11, 12, 9, 30, 0) });
            question.ProjectQuestionsAnswersResponseLog.Add(new ProjectQuestionsAnswersResponseLog { Id = "log-1", Description = "First answer", CreatedOnUtc = new DateTime(2026, 11, 11, 8, 0, 0) });
            question.ProjectQuestionsAnswersResponseLog.Add(new ProjectQuestionsAnswersResponseLog { Id = "log-del", Description = "Deleted answer", CreatedOnUtc = new DateTime(2026, 11, 13), Deleted = true });

            using var package = await Export(fx);
            var qa = package.Workbook.Worksheets["Q&A"];

            Assert.Equal(new[] { "Original description" }, Column(qa, "Description"));
            Assert.Equal(
                new[] { "Answer 2 (2026-11-12 09:30 UTC):\nSecond answer\n\nAnswer 1 (2026-11-11 08:00 UTC):\nFirst answer" },
                Column(qa, "Answers"));
        }

        [Fact]
        public async Task Exports_every_record_not_just_the_first_page()
        {
            var fx = new ExportFixture();
            for (var i = 0; i < 1200; i++)
                fx.Q.TaskList.Add(new ProjectTask { Id = "bulk-" + i, SiteId = SiteId, RequirementId = ReqA, ProjectTaskNumber = 100 + i, Name = "bulk " + i });

            using var package = await Export(fx);

            Assert.Equal(1202, package.Workbook.Worksheets["Tasks"].Dimension.End.Row - 1);
        }

        [Fact]
        public async Task Modules_without_records_for_this_requirement_are_omitted()
        {
            var fx = new ExportFixture(populateA: false);
            fx.Q.IssueList.Add(new Issue { Id = "only-issue", SiteId = SiteId, RequirementId = ReqA, IssueNumber = 1, Name = "A-only issue" });

            using var package = await Export(fx);

            Assert.Equal(new[] { "Requirement Info", "Issues" }, package.Workbook.Worksheets.Select(w => w.Name));
        }

        [Fact]
        public async Task Requirement_with_no_linked_data_exports_only_requirement_info()
        {
            using var package = await Export(new ExportFixture(populateA: false));

            Assert.Equal(new[] { "Requirement Info" }, package.Workbook.Worksheets.Select(w => w.Name));
        }

        [Theory]
        [InlineData(ReqX)]       // other site
        [InlineData("req-del")]  // soft-deleted
        [InlineData("missing")]
        [InlineData("")]
        public async Task Unknown_deleted_or_other_site_requirement_is_not_found(string id)
        {
            var ex = await Assert.ThrowsAsync<RequirementExportException>(() => new ExportFixture().Service().ExportAsync(SiteId, UserId, id));
            Assert.Equal(RequirementExportFailure.NotFound, ex.Failure);
        }

        [Fact]
        public async Task Requirement_outside_users_project_scope_is_not_found()
        {
            var fx = new ExportFixture();
            fx.Authorizer.RequirementAllowed = false;

            var ex = await Assert.ThrowsAsync<RequirementExportException>(() => fx.Service().ExportAsync(SiteId, UserId, ReqA));
            Assert.Equal(RequirementExportFailure.NotFound, ex.Failure);
        }

        [Fact]
        public async Task Missing_module_permission_rejects_the_whole_export_before_reading_module_data()
        {
            var fx = new ExportFixture();
            fx.Authorizer.DeniedLinks.Add(Authorization.MenuLinks.Issue);
            var source = new Mock<IRequirementExportDataSource>(MockBehavior.Strict);
            source.Setup(x => x.GetRequirementAsync(SiteId, ReqA, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequirementInfoRow { Id = ReqA, SiteId = SiteId });

            var ex = await Assert.ThrowsAsync<RequirementExportException>(() => fx.Service(source.Object).ExportAsync(SiteId, UserId, ReqA));

            Assert.Equal(RequirementExportFailure.Forbidden, ex.Failure);
            Assert.Contains("Issues", ex.Message);
            source.VerifyAll(); // strict mock: no module read was attempted
        }

        [Fact]
        public async Task Retrieval_failure_is_an_error_not_an_empty_sheet()
        {
            var fx = new ExportFixture();
            var source = new Mock<IRequirementExportDataSource>();
            source.Setup(x => x.GetRequirementAsync(SiteId, ReqA, It.IsAny<CancellationToken>())).ReturnsAsync(new RequirementInfoRow { Id = ReqA, SiteId = SiteId });
            source.Setup(x => x.GetChangeLogsAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementChangeLogRow>());
            source.Setup(x => x.GetFilesAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementFileRow>());
            source.Setup(x => x.GetStatusTrackingAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementStatusTrackingExportRow>());
            source.Setup(x => x.GetTasksAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementTaskRow>());
            source.Setup(x => x.GetTestCasesAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("db timeout"));

            await Assert.ThrowsAsync<TimeoutException>(() => fx.Service(source.Object).ExportAsync(SiteId, UserId, ReqA));
        }

        [Fact]
        public async Task Scope_passed_to_module_reads_is_server_derived()
        {
            var fx = new ExportFixture();
            var captured = new List<RequirementExportScope>();
            var inner = new RequirementExportDataSource(fx.Q);
            var source = new Mock<IRequirementExportDataSource>();
            source.Setup(x => x.GetRequirementAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string s, string r, CancellationToken c) => inner.GetRequirementAsync(s, r, c));
            source.Setup(x => x.GetNotesAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>()))
                .Callback((RequirementExportScope s, CancellationToken _) => captured.Add(s))
                .ReturnsAsync(new List<RequirementNoteRow>());
            foreach (var setup in new Action[]
            {
                () => source.Setup(x => x.GetTasksAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementTaskRow>()),
                () => source.Setup(x => x.GetTestCasesAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementTestCaseRow>()),
                () => source.Setup(x => x.GetIssuesAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementIssueRow>()),
                () => source.Setup(x => x.GetTimesheetsAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementTimesheetRow>()),
                () => source.Setup(x => x.GetQuestionsAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementQuestionRow>()),
                () => source.Setup(x => x.GetActionItemsAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementActionItemRow>()),
                () => source.Setup(x => x.GetMappingsAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementMappingRow>()),
                () => source.Setup(x => x.GetChangeLogsAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementChangeLogRow>()),
                () => source.Setup(x => x.GetFilesAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementFileRow>()),
                () => source.Setup(x => x.GetStatusTrackingAsync(It.IsAny<RequirementExportScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementStatusTrackingExportRow>()),
            }) setup();

            await fx.Service(source.Object).ExportAsync(SiteId, UserId, "  " + ReqA + " ");

            var scope = Assert.Single(captured);
            Assert.Equal(SiteId, scope.SiteId);
            Assert.Equal(ReqA, scope.RequirementId);
            Assert.Equal(ProjectId, scope.ProjectId);
        }

        [Fact]
        public async Task File_name_uses_the_requirement_number()
        {
            var file = await new ExportFixture().Service().ExportAsync(SiteId, UserId, ReqA);

            Assert.Equal("Requirement-42.xlsx", file.FileName);
            Assert.Equal("Requirement-req_A_.xlsx", RequirementExportService.BuildFileName(0, "req/A?"));
        }
    }
}
