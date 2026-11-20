using System;
using System.Collections.Generic;
using Vsky.Models;

namespace Vsky.Api.Models
{
    // What the requirement list (v2) and its summary strip are asked with: the current list's search model
    // plus the filters the v2 list adds. Every added filter is optional; nothing sent means no filter.
    public record RequirementSearchModelV2 : RequirementSearchModel
    {
        public List<string> PriorityIds { get; set; }
        public List<string> ApprovalStatusIds { get; set; }
        public List<string> RequirementOwnerIds { get; set; }
        public List<string> CustomerOwnerIds { get; set; }
        public List<string> AreaIds { get; set; }
        public List<string> WorkspaceIds { get; set; }
        public DateTime? PlannedEndFromDate { get; set; }
        public DateTime? PlannedEndToDate { get; set; }
        public DateTime? UpdatedFromDate { get; set; }
        public DateTime? UpdatedToDate { get; set; }
        public bool? Overdue { get; set; }
        public bool? PinnedOnly { get; set; }
        public bool? MyRequirements { get; set; }
        public bool? HasTasks { get; set; }
        public bool? NotApproved { get; set; }
        public bool? DueSoon { get; set; }

        /// <summary>Every filter in one object, as the service takes them. `today` is the site's calendar date.</summary>
        public RequirementListFilterV2 ToListFilter(DateTime today) => new RequirementListFilterV2
        {
            SearchText = SearchText,
            RequirementNumber = RequirementNumber,
            ProjectIds = ProjectIds,
            ProjectModuleIds = ProjectModuleIds,
            RequirementIds = RequirementIds,
            RequirementGroupIds = RequirementGroupIds,
            Name = Name,
            StatusIds = StatusIds,
            RequirementTypeIds = RequirementTypeIds,
            IdentifiedUserTypeId = identifiedUserTypeId,
            IdentifiedCustomerIds = identifiedCustomerIds,
            IdentifiedEmployeeIds = identifiedEmployeeIds,
            RequirementTagIds = RequirementTagIds,
            FromDate = FromDate,
            ToDate = ToDate,
            PriorityIds = PriorityIds,
            ApprovalStatusIds = ApprovalStatusIds,
            RequirementOwnerIds = RequirementOwnerIds,
            CustomerOwnerIds = CustomerOwnerIds,
            AreaIds = AreaIds,
            WorkspaceIds = WorkspaceIds,
            PlannedEndFromDate = PlannedEndFromDate,
            PlannedEndToDate = PlannedEndToDate,
            UpdatedFromDate = UpdatedFromDate,
            UpdatedToDate = UpdatedToDate,
            Overdue = Overdue,
            PinnedOnly = PinnedOnly,
            MyRequirements = MyRequirements,
            HasTasks = HasTasks,
            NotApproved = NotApproved,
            DueSoon = DueSoon,
            Today = today
        };
    }
}
