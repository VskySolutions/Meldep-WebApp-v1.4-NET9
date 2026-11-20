using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vsky.Core;
using Vsky.Data;
using Vsky.Models;
using Vsky.Services.ApplicationUserRoles;
using Vsky.Services.Common;

namespace Vsky.Services.Requirements
{
    // Read-only additions for the requirement pages (v2): the list with its extra filters and the summary
    // strip's counts, the linked-work counts, the record as the signed-in reader sees it, and the log read
    // a page at a time. No schema change.
    public class RequirementServiceV2 : IRequirementServiceV2
    {
        #region Define Services
        private readonly IRequirementService _requirementService;
        private readonly ICommonService _commonService;
        private readonly IRepository<Requirement> _requirementRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Notes> _notesRepository;
        private readonly IRepository<ProjectTask> _projectTaskRepository;
        private readonly IRepository<Issue> _issueRepository;
        private readonly IRepository<TestCase> _testCaseRepository;
        private readonly IRepository<ProjectQuestionsAnswers> _projectQuestionsAnswersRepository;
        private readonly IRepository<ProjectActionItems> _projectActionItemsRepository;
        private readonly IRepository<TimesheetLines> _timesheetLinesRepository;
        private readonly IRepository<RequirementChangeLog> _requirementChangeLogRepository;
        private readonly IRepository<FilePathDetails> _filePathDetailsRepository;
        private readonly IRepository<RequirementDataMapping> _requirementDataMappingRepository;
        private readonly IRepository<SitesModifiedLogs> _sitesModifiedLogsRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IApplicationUserRoleService _applicationUserRoleService;
        #endregion

        #region Services Initializations
        public RequirementServiceV2(
            IRequirementService requirementService,
            ICommonService commonService,
            IRepository<Requirement> requirementRepository,
            IRepository<Project> projectRepository,
            IRepository<Notes> notesRepository,
            IRepository<ProjectTask> projectTaskRepository,
            IRepository<Issue> issueRepository,
            IRepository<TestCase> testCaseRepository,
            IRepository<ProjectQuestionsAnswers> projectQuestionsAnswersRepository,
            IRepository<ProjectActionItems> projectActionItemsRepository,
            IRepository<TimesheetLines> timesheetLinesRepository,
            IRepository<RequirementChangeLog> requirementChangeLogRepository,
            IRepository<FilePathDetails> filePathDetailsRepository,
            IRepository<RequirementDataMapping> requirementDataMappingRepository,
            IRepository<SitesModifiedLogs> sitesModifiedLogsRepository,
            UserManager<ApplicationUser> userManager,
            IApplicationUserRoleService applicationUserRoleService
        )
        {
            _requirementService = requirementService;
            _commonService = commonService;
            _requirementRepository = requirementRepository;
            _projectRepository = projectRepository;
            _notesRepository = notesRepository;
            _projectTaskRepository = projectTaskRepository;
            _issueRepository = issueRepository;
            _testCaseRepository = testCaseRepository;
            _projectQuestionsAnswersRepository = projectQuestionsAnswersRepository;
            _projectActionItemsRepository = projectActionItemsRepository;
            _timesheetLinesRepository = timesheetLinesRepository;
            _requirementChangeLogRepository = requirementChangeLogRepository;
            _filePathDetailsRepository = filePathDetailsRepository;
            _requirementDataMappingRepository = requirementDataMappingRepository;
            _sitesModifiedLogsRepository = sitesModifiedLogsRepository;
            _userManager = userManager;
            _applicationUserRoleService = applicationUserRoleService;
        }
        #endregion

        #region GetRequirementList
        // The requirement list (v2): the rows the current list returns (same visibility rules, same columns,
        // pinned rows first), narrowed by the current list's filters and the ones the v2 list adds.
        public async Task<IPagedList<Requirement>> GetRequirementList(
            string SiteId,
            string LoggedUserId,
            string employeeId,
            RequirementListFilterV2 filter,
            string sortBy,
            Dictionary<string, string> sorts,
            bool descending,
            int page = 1,
            int pageSize = int.MaxValue)
        {
            bool IsAdmin = await IsCurrentUserAdmin(LoggedUserId, SiteId);
            var visibleProjectIds = IsAdmin ? null : await GetVisibleProjectIds(LoggedUserId, employeeId);

            // The filters live in BuildListQuery so the list and its summary strip can never disagree.
            var query = BuildListQuery(SiteId, LoggedUserId, employeeId, IsAdmin, visibleProjectIds, filter);

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                var orderBy = $"{sortBy} {(descending ? "desc" : "asc")}";

                query = query
                    .OrderByDescending(x => x.RequirementPinned
                        .Any(p => p.AspNetUserId == LoggedUserId && p.IsPinned))
                    .ThenBy(orderBy);
            }
            else
            {
                query = query
                    .OrderByDescending(x => x.RequirementPinned
                        .Any(p => p.AspNetUserId == LoggedUserId && p.IsPinned))
                    .ThenByDescending(x => x.CreatedOnUtc);
            }

            // Apply multi-level dictionary sorting
            if (sorts != null && sorts.Count > 0)
            {
                query = _commonService.ApplySorting(query, sorts);
            }

            var notesQuery = _notesRepository.TableNoTracking.Where(n => !n.Deleted && n.Type == "Requirement");
            query = query.Select(x => new Requirement
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                ProjectModuleId = x.ProjectModuleId,
                Title = x.Title,
                StatusId = x.StatusId,
                IdentifiedUserType = x.IdentifiedUserType,
                RequirementNumber = x.RequirementNumber,
                IdentifiedDate = x.IdentifiedDate,
                PriorityId = x.PriorityId,
                PlannedStartDate = x.PlannedStartDate,
                PlannedEndDate = x.PlannedEndDate,
                ActualStartDate = x.ActualStartDate,
                ActualEndDate = x.ActualEndDate,
                SourceLabel = x.SourceLabel,
                TargetLabel = x.TargetLabel,
                CreatedOnUtc = x.CreatedOnUtc,
                UpdatedOnUtc = x.UpdatedOnUtc,
                Employee = new Employee
                {
                    Person = new Person
                    {
                        Id = x.Employee.Person.Id,
                        FullName = x.Employee.Person.FirstName + " " + x.Employee.Person.LastName,
                    }
                },
                Customer = new Person
                {
                    Id = x.Customer.Id,
                    FullName = x.Customer.FirstName + " " + x.Customer.LastName,
                },
                Project = new Project
                {
                    Id = x.Project.Id,
                    Name = x.Project.Name,
                    CurrentUserManage =
                    IsAdmin ||
                    x.Project.CreatedById == LoggedUserId ||
                    x.CreatedById == LoggedUserId ||
                    x.Project.ProjectEmployeeMappings
                        .Where(m =>
                            !m.Deleted &&
                            m.EmployeeId == employeeId)
                        .Any(m =>
                            m.ProjectEmployeeRoleMappings
                                .Where(r => !r.Deleted)
                                .Any(r =>
                                    r.SitesProjectRoles
                                        .SitesProjectRolesPermissions
                                        .Any(p =>
                                            !p.Deleted &&
                                            p.FullAccess))),

                    CurrentUserNotes =
                    x.Project.ProjectEmployeeMappings
                        .Where(m =>
                            !m.Deleted &&
                            m.EmployeeId == employeeId)
                        .Any(m =>
                            m.ProjectEmployeeRoleMappings
                                .Where(r => !r.Deleted)
                                .Any(r =>
                                    r.SitesProjectRoles
                                        .SitesProjectRolesPermissions
                                        .Any(p =>
                                            !p.Deleted &&
                                            p.Notes)))
                },
                ProjectModule = new ProjectModule
                {
                    Id = x.ProjectModule.Id,
                    Name = x.ProjectModule.Name
                },
                Status = new DropDown
                {
                    Id = x.Status.Id,
                    DropDownValue = x.Status.DropDownValue
                },
                UserType = new DropDown
                {
                    Id = x.UserType.Id,
                    DropDownValue = x.UserType.DropDownValue
                },
                Priority = new DropDown
                {
                    Id = x.Priority.Id,
                    DropDownValue = x.Priority.DropDownValue
                },
                Area = new DropDown
                {
                    Id = x.Area.Id,
                    DropDownValue = x.Area.DropDownValue
                },
                Workspace = new DropDown
                {
                    Id = x.Workspace.Id,
                    DropDownValue = x.Workspace.DropDownValue
                },
                RequirementEntered = new Employee
                {
                    Person = new Person
                    {
                        Id = x.RequirementEntered.Person.Id,
                        FullName = x.RequirementEntered.Person.FirstName + " " + x.RequirementEntered.Person.LastName,
                    }
                },
                ConfirmedBy = new Employee
                {
                    Person = new Person
                    {
                        Id = x.ConfirmedBy.Person.Id,
                        FullName = x.ConfirmedBy.Person.FirstName + " " + x.ConfirmedBy.Person.LastName,
                    }
                },
                ApprovedBy = new Employee
                {
                    Person = new Person
                    {
                        Id = x.ApprovedBy.Person.Id,
                        FullName = x.ApprovedBy.Person.FirstName + " " + x.ApprovedBy.Person.LastName,
                    }
                },
                RequirementOwner = new Employee
                {
                    Person = new Person
                    {
                        Id = x.RequirementOwner.Person.Id,
                        FullName = x.RequirementOwner.Person.FirstName + " " + x.RequirementOwner.Person.LastName,
                    }
                },
                CustomerOwner = new Person
                {
                    Id = x.CustomerOwner.Id,
                    FullName = x.CustomerOwner.FirstName + " " + x.CustomerOwner.LastName,
                },
                RequirementType = new DropDown
                {
                    Id = x.RequirementType.Id,
                    DropDownValue = x.RequirementType.DropDownValue
                },
                CreatedBy = new ApplicationUser
                {
                    Person = new Person
                    {
                        Id = x.CreatedBy.PersonId,
                        FullName = x.CreatedBy.Person.FirstName + " " + x.CreatedBy.Person.LastName,
                    }
                },
                UpdatedBy = new ApplicationUser
                {
                    Person = new Person
                    {
                        Id = x.UpdatedBy.PersonId,
                        FullName = x.UpdatedBy.Person.FirstName + " " + x.UpdatedBy.Person.LastName,
                    }
                },
                ApprovalStatusDropDown = new DropDown
                {
                    Id = x.ApprovalStatusDropDown.Id,
                    DropDownValue = x.ApprovalStatusDropDown.DropDownValue
                },
                IsPinned = x.RequirementPinned.Any(p => p.AspNetUserId == LoggedUserId && p.IsPinned),
                RequirementColor = x.RequirementColors.Where(c => c.AspNetUserId == LoggedUserId && !c.Deleted).Select(c => c.Color).FirstOrDefault(),
                RequirementTags = x.RequirementTags.Where(t => !t.Deleted && !t.Tags.Deleted && t.AspNetUserId == LoggedUserId).OrderBy(t => t.Tags.Name).Select(t => new RequirementTags
                {
                    Id = t.Id,
                    TagId = t.TagId,
                    RequirementId = t.RequirementId,
                    Tags = new Tags { Id = t.Tags.Id, Name = t.Tags.Name, Color = t.Tags.Color, BgColor = t.Tags.BgColor }
                }).ToList(),
                ProjectTaskRelatedMappings = x.ProjectTaskRelatedMappings.Where(m => m.RequirementId == x.Id && !m.Deleted && m.RequirementId != null)
                .Select(m => new ProjectTask
                {
                    Id = m.Id,
                    ProjectTaskNumber = m.ProjectTaskNumber,
                    Status = new DropDown { Id = m.Status.Id, DropDownValue = m.Status.DropDownValue }
                }).ToList(),
                RequirementNotesCount = notesQuery.Where(m => m.SubModuleId == x.Id).Count()
            });

            // No rich text in the list query: the short description and the latest note are editor HTML
            // (nvarchar(max), a note can be hundreds of KB with pasted images), and carrying them through
            // the sorted, paged query is what made it ask for a large memory grant. The page's rows get a
            // short plain-text preview of each instead, read separately for those rows only.
            var list = new PagedList<Requirement>(query, page, pageSize);
            await FillListPreviews(list);
            await FillChangeLogColumns(SiteId, list);
            return list;
        }

        // The fields whose change log the list can open, under the names the log is kept by.
        private static readonly string[] ListChangeLogColumns =
        {
            "Requirement Status", "Requirement Priority",
            "Planned Start Date", "Planned End Date", "Actual Start Date", "Actual End Date"
        };

        // Title: FillChangeLogColumns
        // Description: For the rows of one page, which of those fields have at least one change-log entry, so
        // the list shows a "change log" button only where there is something to open. One query per page.
        private async Task FillChangeLogColumns(string siteId, IList<Requirement> rows)
        {
            foreach (var row in rows)
                row.ChangeLogColumns = new List<string>();
            if (rows.Count == 0)
                return;

            var ids = rows.Select(x => x.Id).ToList();
            var logged = await _sitesModifiedLogsRepository.TableNoTracking
                .Where(x => !x.Deleted && x.SiteId == siteId && ids.Contains(x.SubModuleId) && ListChangeLogColumns.Contains(x.ColumnName))
                .Select(x => new { x.SubModuleId, x.ColumnName })
                .Distinct()
                .ToListAsync();

            // Ids are compared as the database compares them: without regard to letter case.
            var byRequirement = logged.ToLookup(x => x.SubModuleId, x => x.ColumnName, StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
                row.ChangeLogColumns = byRequirement[row.Id].Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        #endregion

        #region FillListPreviews
        /// <summary>Characters of editor HTML read per row to make a preview; the rest never leaves the database.</summary>
        private const int PreviewSourceLength = 4000;
        /// <summary>Longest preview sent to the list.</summary>
        private const int PreviewLength = 300;
        private const int PreviewBatchSize = 500;

        // Sets ShortDescription and LastNote of the listed rows to a plain-text preview (tags removed, at
        // most PreviewLength characters). The list shows both as one line of text with a tooltip.
        private async Task FillListPreviews(List<Requirement> rows)
        {
            for (var start = 0; start < rows.Count; start += PreviewBatchSize)
            {
                var batch = rows.Skip(start).Take(PreviewBatchSize).ToList();
                var ids = batch.Select(r => r.Id).ToList();

                var descriptions = await _requirementRepository.TableNoTracking
                    .Where(r => ids.Contains(r.Id) && r.ShortDescription != null && r.ShortDescription != "")
                    .Select(r => new { r.Id, Text = r.ShortDescription.Substring(0, PreviewSourceLength) })
                    .ToListAsync();

                var notes = await _notesRepository.TableNoTracking
                    .Where(n => !n.Deleted && n.Type == "Requirement" && ids.Contains(n.SubModuleId))
                    .Select(n => new { n.SubModuleId, n.CreatedOnUtc, Text = n.Note.Substring(0, PreviewSourceLength) })
                    .ToListAsync();

                var descriptionById = descriptions.ToDictionary(d => d.Id, d => d.Text);
                var latestNoteById = notes
                    .GroupBy(n => n.SubModuleId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(n => n.CreatedOnUtc).First().Text);

                foreach (var row in batch)
                {
                    row.ShortDescription = descriptionById.TryGetValue(row.Id, out var description) ? PlainPreview(description) : null;
                    row.LastNote = latestNoteById.TryGetValue(row.Id, out var note) ? PlainPreview(note) : null;
                }
            }
        }

        // Editor HTML reduced to one line of text. Entities are left as they are (the list decodes them).
        // The source may be cut in the middle of a tag (a pasted image is one very long tag), so an
        // unfinished tag at the end goes too; a note that is only an image reads "[image]".
        private static string PlainPreview(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;

            var hasImage = html.IndexOf("<img", StringComparison.OrdinalIgnoreCase) >= 0;
            var text = Regex.Replace(html, @"<(style|script)\b[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            text = Regex.Replace(text, @"<[^>]*>", " ");
            text = Regex.Replace(text, @"<[^>]*$", " ");
            text = Regex.Replace(text.Replace("&nbsp;", " "), @"\s+", " ").Trim();

            if (text.Length == 0) return hasImage ? "[image]" : null;
            return text.Length > PreviewLength ? text.Substring(0, PreviewLength).TrimEnd() + "…" : text;
        }
        #endregion

        #region GetVisibleProjectIds
        // The projects whose requirements a viewer who is not an admin may see: the ones they created,
        // and the ones on which they hold a project role granting full access, view or notes. The rule
        // is the current list's. It is resolved once per request (a viewer's projects are few), so the
        // list and each count of the summary strip filter on a plain list of ids instead of working the
        // membership out again for every requirement.
        private async Task<List<string>> GetVisibleProjectIds(string LoggedUserId, string employeeId)
        {
            return await _projectRepository.TableNoTracking
                .Where(p => !p.Deleted && !p.IsArchived && (
                    p.CreatedById == LoggedUserId ||
                    p.ProjectEmployeeMappings.Any(m =>
                        !m.Deleted &&
                        m.EmployeeId == employeeId &&
                        m.ProjectEmployeeRoleMappings.Any(r =>
                            !r.Deleted &&
                            r.SitesProjectRoles.SitesProjectRolesPermissions.Any(perm =>
                                !perm.Deleted &&
                                (perm.FullAccess || perm.ViewOnly || perm.Notes)
                            )
                        )
                    )
                ))
                .Select(p => p.Id)
                .ToListAsync();
        }
        #endregion

        #region BuildListQuery (shared by the list and its summary)
        // Every visibility rule and filter of the requirement list, in one place. `exclude` names the facet
        // whose own filter is left out, so a summary tile's count answers "how many if I clicked this"
        // rather than "how many already match it".
        private IQueryable<Requirement> BuildListQuery(
            string SiteId,
            string LoggedUserId,
            string employeeId,
            bool IsAdmin,
            List<string> visibleProjectIds,
            RequirementListFilterV2 filter,
            RequirementFacetV2 exclude = RequirementFacetV2.None)
        {
            filter ??= new RequirementListFilterV2();

            var query = _requirementRepository.TableNoTracking.Where(x => !x.Deleted && x.SiteId == SiteId && !x.Project.Deleted && !x.Project.IsArchived);

            if (!IsAdmin)
            {
                // The current list's rule: a requirement is in sight when its project is (see
                // GetVisibleProjectIds) or when the viewer created the requirement.
                visibleProjectIds ??= new List<string>();
                query = query.Where(x => visibleProjectIds.Contains(x.ProjectId) || x.CreatedById == LoggedUserId);
            }

            // ---- The filters the current list takes ----
            var requirementNumber = filter.RequirementNumber;
            if (requirementNumber != 0)
                query = query.Where(x => x.RequirementNumber == requirementNumber);

            var projectIds = filter.ProjectIds;
            if (projectIds != null && projectIds.Any())
                query = query.Where(x => projectIds.Contains(x.ProjectId));

            var projectModuleIds = filter.ProjectModuleIds;
            if (projectModuleIds != null && projectModuleIds.Any())
                query = query.Where(x => projectModuleIds.Contains(x.ProjectModuleId));

            var requirementIds = filter.RequirementIds;
            if (requirementIds != null && requirementIds.Any())
                query = query.Where(x => requirementIds.Contains(x.Id));

            var requirementGroupIds = filter.RequirementGroupIds;
            if (requirementGroupIds != null && requirementGroupIds.Any())
                query = query.Where(x => requirementGroupIds.Contains(x.RequirementGroupId));

            var statusIds = filter.StatusIds;
            if (exclude != RequirementFacetV2.Status && statusIds != null && statusIds.Any())
                query = query.Where(x => statusIds.Contains(x.StatusId));

            var requirementTypeIds = filter.RequirementTypeIds;
            if (requirementTypeIds != null && requirementTypeIds.Any())
                query = query.Where(x => requirementTypeIds.Contains(x.RequirementTypeId));

            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                var name = filter.Name.Trim().ToLower();
                query = query.Where(x => x.Title.ToLower().Contains(name));
            }

            var identifiedUserTypeId = filter.IdentifiedUserTypeId;
            if (!string.IsNullOrWhiteSpace(identifiedUserTypeId))
                query = query.Where(x => x.IdentifiedUserType == identifiedUserTypeId);

            var identifiedCustomerIds = filter.IdentifiedCustomerIds;
            if (identifiedCustomerIds != null && identifiedCustomerIds.Any())
                query = query.Where(x => identifiedCustomerIds.Contains(x.IdentifiedCustomerId));

            var identifiedEmployeeIds = filter.IdentifiedEmployeeIds;
            if (identifiedEmployeeIds != null && identifiedEmployeeIds.Any())
                query = query.Where(x => identifiedEmployeeIds.Contains(x.IdentifiedEmployeeId));

            var requirementTagIds = filter.RequirementTagIds;
            if (requirementTagIds?.Any() == true)
                query = query.Where(x => x.RequirementTags.Any(t => !t.Deleted && t.AspNetUserId == LoggedUserId && requirementTagIds.Contains(t.Tags.Id)));

            // Created between
            var fromDate = filter.FromDate;
            var toDate = filter.ToDate;
            if (fromDate != null)
                query = query.Where(x => x.CreatedOnUtc >= fromDate);
            if (toDate != null)
                query = query.Where(a => a.CreatedOnUtc <= toDate);

            // ---- The filters the v2 list adds ----
            var today = (filter.Today ?? DateTime.UtcNow).Date;

            var priorityIds = filter.PriorityIds;
            if (exclude != RequirementFacetV2.Priority && priorityIds != null && priorityIds.Any())
                query = query.Where(x => priorityIds.Contains(x.PriorityId));

            var approvalStatusIds = filter.ApprovalStatusIds;
            if (approvalStatusIds != null && approvalStatusIds.Any())
                query = query.Where(x => approvalStatusIds.Contains(x.ApprovalStatus));

            var requirementOwnerIds = filter.RequirementOwnerIds;
            if (requirementOwnerIds != null && requirementOwnerIds.Any())
                query = query.Where(x => requirementOwnerIds.Contains(x.RequirementOwnerId));

            var customerOwnerIds = filter.CustomerOwnerIds;
            if (customerOwnerIds != null && customerOwnerIds.Any())
                query = query.Where(x => customerOwnerIds.Contains(x.CustomerOwnerId));

            var areaIds = filter.AreaIds;
            if (areaIds != null && areaIds.Any())
                query = query.Where(x => areaIds.Contains(x.AreaId));

            var workspaceIds = filter.WorkspaceIds;
            if (workspaceIds != null && workspaceIds.Any())
                query = query.Where(x => workspaceIds.Contains(x.WorkspaceId));

            var plannedEndFromDate = filter.PlannedEndFromDate;
            var plannedEndToDate = filter.PlannedEndToDate;
            if (plannedEndFromDate != null)
                query = query.Where(x => x.PlannedEndDate >= plannedEndFromDate);
            if (plannedEndToDate != null)
                query = query.Where(x => x.PlannedEndDate <= plannedEndToDate);

            var updatedFromDate = filter.UpdatedFromDate;
            var updatedToDate = filter.UpdatedToDate;
            if (updatedFromDate != null)
                query = query.Where(x => x.UpdatedOnUtc >= updatedFromDate);
            if (updatedToDate != null)
                query = query.Where(x => x.UpdatedOnUtc <= updatedToDate);

            if (exclude != RequirementFacetV2.HasTasks)
            {
                if (filter.HasTasks == true)
                    query = query.Where(x => x.ProjectTaskRelatedMappings.Any(t => !t.Deleted));
                else if (filter.HasTasks == false)
                    query = query.Where(x => !x.ProjectTaskRelatedMappings.Any(t => !t.Deleted));
            }

            if (exclude != RequirementFacetV2.NotApproved && filter.NotApproved == true)
                query = query.Where(x => x.ApprovalStatusDropDown == null || x.ApprovalStatusDropDown.DropDownValue != "Approved");

            if (exclude != RequirementFacetV2.DueSoon && filter.DueSoon == true)
            {
                var weekEnd = today.AddDays(7);
                query = query.Where(x => x.PlannedEndDate != null && x.PlannedEndDate >= today && x.PlannedEndDate <= weekEnd && x.Status.DropDownValue != "Close");
            }

            if (exclude != RequirementFacetV2.Overdue && filter.Overdue == true)
                query = query.Where(x => x.PlannedEndDate != null && x.PlannedEndDate < today && x.Status.DropDownValue != "Close");

            if (exclude != RequirementFacetV2.Pinned && filter.PinnedOnly == true)
                query = query.Where(x => x.RequirementPinned.Any(p => p.AspNetUserId == LoggedUserId && p.IsPinned));

            if (exclude != RequirementFacetV2.Mine && filter.MyRequirements == true)
                query = query.Where(x =>
                    x.RequirementOwnerId == employeeId ||
                    x.IdentifiedEmployeeId == employeeId ||
                    x.RequirementEnteredBy == employeeId ||
                    x.CreatedById == LoggedUserId);

            // ---- The search box (the columns the current list searches) ----
            var SearchText = filter.SearchText;
            if (!string.IsNullOrEmpty(SearchText))
            {
                DateTime.TryParse(SearchText, out var parsedDate);
                query = query.Where(m =>
                       m.RequirementNumber.ToString().Contains(SearchText.ToLower()) ||
                       m.Project.Name.ToLower().Contains(SearchText.ToLower()) ||
                       m.ProjectModule.Name.ToLower().Contains(SearchText.ToLower()) ||
                       m.Title.ToLower().Contains(SearchText.ToLower()) ||
                       m.Status.DropDownValue.ToLower().Contains(SearchText.ToLower()) ||
                       m.Priority.DropDownValue.ToLower().Contains(SearchText.ToLower()) ||
                       m.UserType.DropDownValue.ToLower().Contains(SearchText.ToLower()) ||
                       (m.Employee.Person.FirstName + " " + m.Employee.Person.LastName).ToLower().Contains(SearchText.ToLower()) ||
                       m.CreatedOnUtc.Date == parsedDate.Date ||
                       m.RequirementTags.Any(t => t.Tags.Name.ToLower().Contains(SearchText.ToLower())) ||
                       m.Area.DropDownValue.ToLower().Contains(SearchText.ToLower()) ||
                       m.Workspace.DropDownValue.ToLower().Contains(SearchText.ToLower()) ||
                       m.RequirementType.DropDownValue.ToLower().Contains(SearchText.ToLower()) ||
                       m.SourceLabel.ToLower().Contains(SearchText.ToLower()) ||
                       m.TargetLabel.ToLower().Contains(SearchText.ToLower()) ||
                       m.IdentifiedDate == parsedDate.Date ||
                       m.PlannedStartDate == parsedDate.Date ||
                       m.PlannedEndDate == parsedDate.Date ||
                       m.ActualStartDate == parsedDate.Date ||
                       m.ActualEndDate == parsedDate.Date ||
                       (m.RequirementEntered.Person.FirstName + " " + m.RequirementEntered.Person.LastName).ToLower().Contains(SearchText.ToLower()) ||
                       (m.RequirementOwner.Person.FirstName + " " + m.RequirementOwner.Person.LastName).ToLower().Contains(SearchText.ToLower()) ||
                       (m.CustomerOwner.FirstName + " " + m.CustomerOwner.LastName).ToLower().Contains(SearchText.ToLower()) ||
                       m.ApprovalStatusDropDown.DropDownValue.ToLower().Contains(SearchText.ToLower()) ||
                       (m.CreatedBy.Person.FirstName + " " + m.CreatedBy.Person.LastName).ToLower().Contains(SearchText.ToLower()) ||
                       (m.UpdatedBy.Person.FirstName + " " + m.UpdatedBy.Person.LastName).ToLower().Contains(SearchText.ToLower()) ||
                       (m.CreatedOnUtc.ToString().Contains(SearchText.ToLower()))
                );
            }

            return query;
        }
        #endregion

        #region QuickSearchRequirements
        // The quick search of the requirement pages: the few requirements in the viewer's sight that match
        // what was typed, best match first. Every word typed must appear in the requirement's number, its
        // title, its project or its module. It runs as the reader types, so it takes the list's visibility
        // rules (BuildListQuery) and none of the list's columns.
        public async Task<List<RequirementQuickSearchItemV2>> QuickSearchRequirements(string SiteId, string LoggedUserId, string employeeId, string text, int take = 8)
        {
            text = (text ?? "").Trim();
            if (text.Length > 100) text = text.Substring(0, 100);

            // "#128" and "0128" both mean the number 128.
            var words = text.ToLowerInvariant()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.TrimStart('#'))
                .Select(w => w.Length > 1 && w.All(c => c >= '0' && c <= '9') ? w.TrimStart('0') : w)
                .Where(w => w.Length > 0)
                .Distinct()
                .Take(6)
                .ToList();
            if (words.Count == 0) return new List<RequirementQuickSearchItemV2>();
            take = Math.Clamp(take, 1, 25);

            bool IsAdmin = await IsCurrentUserAdmin(LoggedUserId, SiteId);
            var visibleProjectIds = IsAdmin ? null : await GetVisibleProjectIds(LoggedUserId, employeeId);
            var query = BuildListQuery(SiteId, LoggedUserId, employeeId, IsAdmin, visibleProjectIds, null);

            foreach (var word in words)
            {
                query = query.Where(x =>
                    x.Title.ToLower().Contains(word) ||
                    x.RequirementNumber.ToString().Contains(word) ||
                    x.Project.Name.ToLower().Contains(word) ||
                    x.ProjectModule.Name.ToLower().Contains(word));
            }

            // Best first: the number typed exactly, then a number beginning with it, then a title beginning
            // with what was typed, then a title containing it as typed; within each, the latest touched.
            var phrase = string.Join(" ", words);
            bool isNumber = words.Count == 1 && phrase.Length <= 9 && phrase.All(c => c >= '0' && c <= '9');
            int number = isNumber ? int.Parse(phrase) : 0;

            return await query
                .OrderByDescending(x => isNumber && x.RequirementNumber == number)
                .ThenByDescending(x => isNumber && x.RequirementNumber.ToString().StartsWith(phrase))
                .ThenByDescending(x => x.Title.ToLower().StartsWith(phrase))
                .ThenByDescending(x => x.Title.ToLower().Contains(phrase))
                .ThenByDescending(x => x.UpdatedOnUtc ?? x.CreatedOnUtc)
                .ThenByDescending(x => x.RequirementNumber)
                .ThenBy(x => x.Id)
                .Select(x => new RequirementQuickSearchItemV2
                {
                    Id = x.Id,
                    RequirementNumber = x.RequirementNumber,
                    Title = x.Title,
                    ProjectName = x.Project.Name,
                    ProjectModuleName = x.ProjectModule.Name,
                    StatusLabel = x.Status.DropDownValue,
                    StatusColor = x.Status.Color,
                    StatusBgColor = x.Status.BgColor
                })
                .Take(take)
                .ToListAsync();
        }
        #endregion

        #region GetRequirementSummary
        // The counts of the strip above the list: per status, per priority and per attention flag, over the
        // same filters as the list. Each count leaves its own filter out (see BuildListQuery).
        public async Task<RequirementSummaryV2> GetRequirementSummary(string SiteId, string LoggedUserId, string employeeId, RequirementListFilterV2 filter)
        {
            bool IsAdmin = await IsCurrentUserAdmin(LoggedUserId, SiteId);
            var visibleProjectIds = IsAdmin ? null : await GetVisibleProjectIds(LoggedUserId, employeeId);
            filter ??= new RequirementListFilterV2();
            var today = (filter.Today ?? DateTime.UtcNow).Date;
            var weekEnd = today.AddDays(7);
            var weekAgo = today.AddDays(-7);

            IQueryable<Requirement> Facet(RequirementFacetV2 exclude) =>
                BuildListQuery(SiteId, LoggedUserId, employeeId, IsAdmin, visibleProjectIds, filter, exclude);

            var summary = new RequirementSummaryV2
            {
                Total = await Facet(RequirementFacetV2.None).CountAsync(),

                // Status and priority are optional: a requirement without one joins to NULLs, so the
                // non-nullable SortOrder is read as int? (reading it as int throws on the first such row).
                ByStatus = await Facet(RequirementFacetV2.Status)
                    .GroupBy(x => new { x.StatusId, x.Status.DropDownValue, x.Status.Color, x.Status.BgColor, SortOrder = (int?)x.Status.SortOrder })
                    .Select(g => new RequirementFacetCountV2
                    {
                        Id = g.Key.StatusId,
                        Label = g.Key.DropDownValue,
                        Color = g.Key.Color,
                        BgColor = g.Key.BgColor,
                        SortOrder = g.Key.SortOrder ?? 0,
                        Count = g.Count()
                    })
                    .ToListAsync(),

                ByPriority = await Facet(RequirementFacetV2.Priority)
                    .GroupBy(x => new { x.PriorityId, x.Priority.DropDownValue, x.Priority.Color, x.Priority.BgColor, SortOrder = (int?)x.Priority.SortOrder })
                    .Select(g => new RequirementFacetCountV2
                    {
                        Id = g.Key.PriorityId,
                        Label = g.Key.DropDownValue,
                        Color = g.Key.Color,
                        BgColor = g.Key.BgColor,
                        SortOrder = g.Key.SortOrder ?? 0,
                        Count = g.Count()
                    })
                    .ToListAsync(),

                Overdue = await Facet(RequirementFacetV2.Overdue)
                    .CountAsync(x => x.PlannedEndDate != null && x.PlannedEndDate < today && x.Status.DropDownValue != "Close"),

                DueThisWeek = await Facet(RequirementFacetV2.DueSoon)
                    .CountAsync(x => x.PlannedEndDate != null && x.PlannedEndDate >= today && x.PlannedEndDate <= weekEnd && x.Status.DropDownValue != "Close"),

                // Anything not explicitly approved still needs a decision, including requirements with no
                // approval state at all.
                NotApproved = await Facet(RequirementFacetV2.NotApproved)
                    .CountAsync(x => x.ApprovalStatusDropDown == null || x.ApprovalStatusDropDown.DropDownValue != "Approved"),

                Mine = await Facet(RequirementFacetV2.Mine)
                    .CountAsync(x =>
                        x.RequirementOwnerId == employeeId ||
                        x.IdentifiedEmployeeId == employeeId ||
                        x.RequirementEnteredBy == employeeId ||
                        x.CreatedById == LoggedUserId),

                Pinned = await Facet(RequirementFacetV2.Pinned)
                    .CountAsync(x => x.RequirementPinned.Any(p => p.AspNetUserId == LoggedUserId && p.IsPinned)),

                UpdatedLast7Days = await Facet(RequirementFacetV2.None)
                    .CountAsync(x => x.UpdatedOnUtc >= weekAgo),

                WithoutTasks = await Facet(RequirementFacetV2.HasTasks)
                    .CountAsync(x => !x.ProjectTaskRelatedMappings.Any(t => !t.Deleted))
            };

            summary.ByStatus = summary.ByStatus.OrderBy(s => s.SortOrder).ThenBy(s => s.Label).ToList();
            summary.ByPriority = summary.ByPriority.OrderBy(s => s.SortOrder).ThenBy(s => s.Label).ToList();
            return summary;
        }
        #endregion

        #region GetRequirementRelatedCounts
        public async Task<RequirementRelatedCountsV2> GetRequirementRelatedCounts(string siteId, string requirementId)
        {
            // "Open" is anything not in a terminal state; the labels are the site's own dropdown values.
            var closedLabels = new[] { "Close", "Closed", "Completed", "Cancelled", "Done" };
            var passedLabels = new[] { "Pass", "Passed", "Resolved" };

            var tasks = _projectTaskRepository.TableNoTracking.Where(t => !t.Deleted && t.SiteId == siteId && t.RequirementId == requirementId);
            var issues = _issueRepository.TableNoTracking.Where(i => !i.Deleted && i.SiteId == siteId && i.RequirementId == requirementId);
            var testCases = _testCaseRepository.TableNoTracking.Where(t => !t.Deleted && t.SiteId == siteId && t.RequirementId == requirementId);
            var questions = _projectQuestionsAnswersRepository.TableNoTracking.Where(q => !q.Deleted && q.SiteId == siteId && q.RequirementId == requirementId);
            var actionItems = _projectActionItemsRepository.TableNoTracking.Where(a => !a.Deleted && a.SiteId == siteId && a.RequirementId == requirementId);
            var lines = _timesheetLinesRepository.TableNoTracking.Where(l => !l.Deleted && l.Timesheet.SiteId == siteId && l.Task.RequirementId == requirementId);

            return new RequirementRelatedCountsV2
            {
                Tasks = await tasks.CountAsync(),
                OpenTasks = await tasks.CountAsync(t => t.Status == null || !closedLabels.Contains(t.Status.DropDownValue)),
                Issues = await issues.CountAsync(),
                OpenIssues = await issues.CountAsync(i => i.Status == null || !closedLabels.Contains(i.Status.DropDownValue)),
                TestCases = await testCases.CountAsync(),
                PassedTestCases = await testCases.CountAsync(t => t.Status != null && passedLabels.Contains(t.Status.DropDownValue)),
                QuestionsAnswers = await questions.CountAsync(),
                ActionItems = await actionItems.CountAsync(),
                TimesheetHours = await lines.SumAsync(l => (decimal?)l.Hours) ?? 0m,
                Notes = await _notesRepository.TableNoTracking.CountAsync(n => !n.Deleted && n.Type == "Requirement" && n.SubModuleId == requirementId),
                LogEntries = await _requirementChangeLogRepository.TableNoTracking.CountAsync(l => !l.Deleted && l.RequirementId == requirementId),
                Documents = await _filePathDetailsRepository.TableNoTracking.CountAsync(f => !f.Deleted && f.ModuleId == requirementId),
                DataMappings = await _requirementDataMappingRepository.TableNoTracking.CountAsync(m => !m.Deleted && m.RequirementId == requirementId)
            };
        }
        #endregion

        #region GetRequirementDetailsForViewer
        // The record as one reader sees it: the shared details (the same read the current screens use, so
        // every field added there arrives here too), then the reader's own rights and marks in a second,
        // small query.
        public async Task<Requirement> GetRequirementDetailsForViewer(string id, string siteId, string loggedUserId, string employeeId)
        {
            var item = await _requirementService.GetRequirementDetailsById(id);
            if (item == null) return null;

            bool isAdmin = await IsCurrentUserAdmin(loggedUserId, siteId);

            var viewer = await _requirementRepository.TableNoTracking
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    CanManage = isAdmin ||
                        x.Project.CreatedById == loggedUserId ||
                        x.CreatedById == loggedUserId ||
                        x.Project.ProjectEmployeeMappings
                            .Where(m => !m.Deleted && m.EmployeeId == employeeId)
                            .Any(m => m.ProjectEmployeeRoleMappings
                                .Where(r => !r.Deleted)
                                .Any(r => r.SitesProjectRoles.SitesProjectRolesPermissions.Any(p => !p.Deleted && p.FullAccess))),
                    CanNotes = x.Project.ProjectEmployeeMappings
                        .Where(m => !m.Deleted && m.EmployeeId == employeeId)
                        .Any(m => m.ProjectEmployeeRoleMappings
                            .Where(r => !r.Deleted)
                            .Any(r => r.SitesProjectRoles.SitesProjectRolesPermissions.Any(p => !p.Deleted && p.Notes))),
                    IsPinned = x.RequirementPinned.Any(p => p.AspNetUserId == loggedUserId && p.IsPinned),
                    Color = x.RequirementColors.Where(c => c.AspNetUserId == loggedUserId && !c.Deleted).Select(c => c.Color).FirstOrDefault(),
                    Tags = x.RequirementTags
                        .Where(t => !t.Deleted && !t.Tags.Deleted && t.AspNetUserId == loggedUserId)
                        .OrderBy(t => t.Tags.Name)
                        .Select(t => new RequirementTags
                        {
                            Id = t.Id,
                            TagId = t.TagId,
                            RequirementId = t.RequirementId,
                            Tags = new Tags { Id = t.Tags.Id, Name = t.Tags.Name, Color = t.Tags.Color, BgColor = t.Tags.BgColor }
                        }).ToList(),
                    NotesCount = _notesRepository.TableNoTracking.Count(n => !n.Deleted && n.Type == "Requirement" && n.SubModuleId == x.Id)
                })
                .FirstOrDefaultAsync();

            if (viewer != null)
            {
                item.Project ??= new Project();
                item.Project.CurrentUserManage = viewer.CanManage;
                item.Project.CurrentUserNotes = viewer.CanNotes;
                item.IsPinned = viewer.IsPinned;
                item.RequirementColor = viewer.Color;
                item.RequirementTags = viewer.Tags;
                item.RequirementNotesCount = viewer.NotesCount;
            }
            return item;
        }
        #endregion

        #region GetRequirementLogThread
        // The log read like a chat: the newest page first, older pages as the reader scrolls up, the
        // requirement's own description as the oldest entry of all. The cursor is the id of the oldest entry
        // already shown (the requirement's id once the description has been reached).
        public async Task<RequirementLogThreadV2> GetRequirementLogThread(string siteId, string id, string beforeId, int take)
        {
            take = Math.Clamp(take, 1, 100);

            var requirement = await _requirementRepository.TableNoTracking
                .Where(x => !x.Deleted && x.SiteId == siteId && x.Id == id)
                .Select(x => new RequirementLogEntryV2
                {
                    Id = x.Id,
                    Description = x.Description,
                    CreatedOnUtc = x.CreatedOnUtc,
                    CreatedById = x.CreatedById,
                    CreatedByUserName = x.CreatedBy.UserName,
                    CreatedByName = x.CreatedBy.Person.FirstName + " " + x.CreatedBy.Person.LastName,
                    IsRequirementDescription = true
                })
                .FirstOrDefaultAsync();

            var thread = new RequirementLogThreadV2();
            if (requirement == null) return thread;

            var hasDescription = HasRichText(requirement.Description);
            var logs = _requirementChangeLogRepository.TableNoTracking.Where(l => !l.Deleted && l.RequirementId == id);

            thread.HasDescription = hasDescription;
            thread.Total = await logs.CountAsync() + (hasDescription ? 1 : 0);

            // The description is the last page; nothing lies beyond it.
            if (beforeId == requirement.Id) return thread;

            if (!string.IsNullOrEmpty(beforeId))
            {
                var cursor = await logs.Where(l => l.Id == beforeId).Select(l => new { l.CreatedOnUtc, l.Id }).FirstOrDefaultAsync();
                if (cursor != null)
                {
                    logs = logs.Where(l =>
                        l.CreatedOnUtc < cursor.CreatedOnUtc ||
                        (l.CreatedOnUtc == cursor.CreatedOnUtc && string.Compare(l.Id, cursor.Id) < 0));
                }
            }

            // One more than asked tells whether another page exists without a second count.
            var items = await logs
                .OrderByDescending(l => l.CreatedOnUtc)
                .ThenByDescending(l => l.Id)
                .Take(take + 1)
                .Select(l => new RequirementLogEntryV2
                {
                    Id = l.Id,
                    Description = l.Description,
                    CreatedOnUtc = l.CreatedOnUtc,
                    CreatedById = l.CreatedById,
                    CreatedByUserName = l.CreatedBy.UserName,
                    CreatedByName = l.CreatedBy.Person.FirstName + " " + l.CreatedBy.Person.LastName,
                    IsRequirementDescription = false
                })
                .ToListAsync();

            if (items.Count > take)
            {
                items.RemoveAt(items.Count - 1);
                thread.HasMore = true;
            }
            else if (hasDescription)
            {
                // The change log is exhausted: the description closes this page when there is room,
                // otherwise it is the next (and last) page.
                if (items.Count < take) items.Add(requirement);
                else thread.HasMore = true;
            }

            thread.Items = items;
            return thread;
        }

        /// <summary>Whether editor markup says anything once its tags and blanks are stripped.</summary>
        private static bool HasRichText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return false;
            if (html.Contains("<img", StringComparison.OrdinalIgnoreCase)) return true;
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ").Replace("&nbsp;", " ");
            return !string.IsNullOrWhiteSpace(text);
        }
        #endregion

        private async Task<bool> IsCurrentUserAdmin(string userId, string siteId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;
            var roles = await _applicationUserRoleService.GetRoleNamesByUserAndSite(user.Id, siteId);
            return roles.Contains("Admin") || roles.Contains("Site Super Admin") || roles.Contains("System Super Admin") || roles.Contains("Project Admin");
        }
    }
}
