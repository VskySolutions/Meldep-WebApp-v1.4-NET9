using System.Collections.Generic;
using System.Threading.Tasks;
using Vsky.Core;
using Vsky.Models;

namespace Vsky.Services.Requirements
{
    // Read-only additions for the requirement pages (v2). Every write still goes through
    // IRequirementService, so there is one set of save rules.
    public interface IRequirementServiceV2
    {
        /// <summary>One page of the requirement list: the current list's rows and visibility rules, with the v2 filters.</summary>
        Task<IPagedList<Requirement>> GetRequirementList(
            string SiteId,
            string LoggedUserId,
            string employeeId,
            RequirementListFilterV2 filter,
            string sortBy,
            Dictionary<string, string> sorts,
            bool descending,
            int page = 1,
            int pageSize = int.MaxValue);

        /// <summary>The counts of the strip above the list, over the same filters the list takes.</summary>
        Task<RequirementSummaryV2> GetRequirementSummary(string SiteId, string LoggedUserId, string employeeId, RequirementListFilterV2 filter);

        /// <summary>The quick search: the few requirements in the viewer's sight whose number, title, project or module match the text, best match first.</summary>
        Task<List<RequirementQuickSearchItemV2>> QuickSearchRequirements(string SiteId, string LoggedUserId, string employeeId, string text, int take = 8);

        Task<RequirementRelatedCountsV2> GetRequirementRelatedCounts(string siteId, string requirementId);
        Task<Requirement> GetRequirementDetailsForViewer(string id, string siteId, string loggedUserId, string employeeId);
        Task<RequirementLogThreadV2> GetRequirementLogThread(string siteId, string id, string beforeId, int take);
    }
}
