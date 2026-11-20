using System;
using System.Collections.Generic;

namespace Vsky.Models
{
    // Read-side shapes of the requirement list (v2): every filter the list and its summary strip accept,
    // and the faceted counts the strip draws. None of these is mapped to a table.

    /// <summary>The filters of the requirement list (v2). Nothing set means no filter.</summary>
    public class RequirementListFilterV2
    {
        // ---- The filters the current list takes ----
        public string SearchText { get; set; }
        public int RequirementNumber { get; set; }
        public List<string> ProjectIds { get; set; }
        public List<string> ProjectModuleIds { get; set; }
        public List<string> RequirementIds { get; set; }
        public List<string> RequirementGroupIds { get; set; }
        public string Name { get; set; }
        public List<string> StatusIds { get; set; }
        public List<string> RequirementTypeIds { get; set; }
        public string IdentifiedUserTypeId { get; set; }
        public List<string> IdentifiedCustomerIds { get; set; }
        public List<string> IdentifiedEmployeeIds { get; set; }
        public List<string> RequirementTagIds { get; set; }

        /// <summary>Created on or after / on or before.</summary>
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // ---- The filters the v2 list adds ----
        public List<string> PriorityIds { get; set; }
        public List<string> ApprovalStatusIds { get; set; }
        public List<string> RequirementOwnerIds { get; set; }
        public List<string> CustomerOwnerIds { get; set; }
        public List<string> AreaIds { get; set; }
        public List<string> WorkspaceIds { get; set; }

        /// <summary>Planned end date on or after / on or before.</summary>
        public DateTime? PlannedEndFromDate { get; set; }
        public DateTime? PlannedEndToDate { get; set; }

        /// <summary>Last updated on or after / on or before.</summary>
        public DateTime? UpdatedFromDate { get; set; }
        public DateTime? UpdatedToDate { get; set; }

        /// <summary>Planned end date in the past and the requirement not closed.</summary>
        public bool? Overdue { get; set; }
        /// <summary>Only requirements the signed-in user pinned.</summary>
        public bool? PinnedOnly { get; set; }
        /// <summary>Owned by, identified by, entered by or created by the signed-in user.</summary>
        public bool? MyRequirements { get; set; }
        /// <summary>true: converted into at least one task; false: no task yet.</summary>
        public bool? HasTasks { get; set; }
        /// <summary>Approval state missing or anything other than "Approved".</summary>
        public bool? NotApproved { get; set; }
        /// <summary>Planned end within the next seven days and not closed.</summary>
        public bool? DueSoon { get; set; }

        /// <summary>The site's calendar "today", used for the overdue and due-soon reckonings.</summary>
        public DateTime? Today { get; set; }
    }

    /// <summary>A facet whose own filter is left out when its counts are computed (faceted-search counts).</summary>
    public enum RequirementFacetV2
    {
        None,
        Status,
        Priority,
        Overdue,
        Pinned,
        Mine,
        NotApproved,
        DueSoon,
        HasTasks
    }

    public class RequirementFacetCountV2
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Color { get; set; }
        public string BgColor { get; set; }
        public int SortOrder { get; set; }
        public int Count { get; set; }
    }

    /// <summary>The counts the strip above the list shows, over the same filters as the list.</summary>
    public class RequirementSummaryV2
    {
        public int Total { get; set; }
        public int Overdue { get; set; }
        public int DueThisWeek { get; set; }
        public int NotApproved { get; set; }
        public int Mine { get; set; }
        public int Pinned { get; set; }
        public int UpdatedLast7Days { get; set; }
        public int WithoutTasks { get; set; }
        public List<RequirementFacetCountV2> ByStatus { get; set; } = new List<RequirementFacetCountV2>();
        public List<RequirementFacetCountV2> ByPriority { get; set; } = new List<RequirementFacetCountV2>();
    }

    /// <summary>One suggestion of the quick search on the requirement pages (v2): what it takes to recognise a requirement and open it.</summary>
    public class RequirementQuickSearchItemV2
    {
        public string Id { get; set; }
        public int RequirementNumber { get; set; }
        public string Title { get; set; }
        public string ProjectName { get; set; }
        public string ProjectModuleName { get; set; }

        /// <summary>The status as its badge reads: the label and the site's colours for it.</summary>
        public string StatusLabel { get; set; }
        public string StatusColor { get; set; }
        public string StatusBgColor { get; set; }
    }
}
