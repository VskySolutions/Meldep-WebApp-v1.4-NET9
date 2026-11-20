using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Vsky.Api.ApiErrors;
using Vsky.Api.Extensions;
using Vsky.Api.Models;
using Vsky.Models;
using Vsky.Services.Common;
using Vsky.Services.Requirements;
using Vsky.Services.Sites;

namespace Vsky.Api.Controllers
{
    // The requirement pages (v2): the list with its summary strip, and the details page. Read-only: every
    // write on those pages still goes to RequirementController, so the current screens and v2 share one
    // set of save rules.
    [Route("requirement-v2")]
    [RequireMenu(MenuLinks.Requirement)]
    public class RequirementControllerV2 : BaseController
    {
        #region Define Services
        private readonly GlobalVariable _globalVariable;
        private readonly IMapper _mapper;
        private readonly ICommonService _commonService;
        private readonly ISiteService _siteService;
        private readonly IRequirementServiceV2 _requirementServiceV2;
        #endregion

        #region Services Initializations
        public RequirementControllerV2(
            GlobalVariable globalVariable,
            IMapper mapper,
            ICommonService commonService,
            ISiteService siteService,
            IRequirementServiceV2 requirementServiceV2
        )
        {
            _globalVariable = globalVariable;
            _mapper = mapper;
            _commonService = commonService;
            _siteService = siteService;
            _requirementServiceV2 = requirementServiceV2;
        }
        #endregion

        #region GetRequirementList
        // Title: Get Requirement List (v2)
        // Description: One page of the requirements the signed-in user may see: the rows of the current
        // list, narrowed by its filters and the ones the v2 list adds (priority, approval, owner, area,
        // workspace, due and updated ranges, and the overdue / due soon / not approved / mine / pinned /
        // converted toggles).
        [HttpPost("list")]
        [ReadAccess]
        public async Task<IActionResult> GetRequirementList(RequirementSearchModelV2 searchModel)
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var employeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);
                var today = await SiteToday(SiteId);

                var list = await _requirementServiceV2.GetRequirementList(
                    SiteId,
                    LoggedUserId,
                    employeeId,
                    searchModel.ToListFilter(today),
                    searchModel.SortBy,
                    searchModel.Sorts,
                    searchModel.Descending,
                    searchModel.Page,
                    searchModel.PageSize
                );

                var model = new RequirementListModel
                {
                    Data = _mapper.Map<IList<RequirementModel>>(list),
                    Total = list.TotalCount
                };

                return Ok(model);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        #endregion

        #region GetRequirementSummary
        // Title: Get Requirement Summary
        // Description: The counts of the strip above the list (by status, by priority, overdue, due this
        // week, not approved, mine, pinned, not converted, updated in the last seven days) over the same
        // filters as the list.
        [HttpPost("summary")]
        [ReadAccess]
        public async Task<IActionResult> GetRequirementSummary(RequirementSearchModelV2 searchModel)
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var employeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);
                var today = await SiteToday(SiteId);

                var summary = await _requirementServiceV2.GetRequirementSummary(SiteId, LoggedUserId, employeeId, searchModel.ToListFilter(today));
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        #endregion

        #region QuickSearchRequirements
        // Title: Quick Search Requirements
        // Description: The suggestions of the quick search on the requirement page: the few requirements the
        // signed-in user may see whose number, title, project or module match what was typed, best match first.
        [HttpGet("quick-search")]
        public async Task<IActionResult> QuickSearchRequirements(string text, int take = 8)
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var employeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var items = await _requirementServiceV2.QuickSearchRequirements(SiteId, LoggedUserId, employeeId, text, take);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        #endregion

        #region private: site calendar date
        // The site's own "today", for the overdue and due-soon reckonings (the server clock may be elsewhere).
        private async Task<DateTime> SiteToday(string siteId)
        {
            var siteData = await _siteService.GetById(siteId);
            return _siteService.GetDateTime(siteData.TimeZone).Date;
        }
        #endregion

        #region GetRequirementDetailsById
        // Title: Get Requirement Details (v2)
        // Description: The requirement's details plus what the signed-in reader may do on it
        // (project.currentUserManage / currentUserNotes) and their own pin, colour and tags.
        [HttpGet("details/{id}")]
        [SiteOwned(typeof(Vsky.Models.Requirement))]
        [RecordScope(typeof(Vsky.Models.Requirement))]
        public async Task<IActionResult> GetRequirementDetailsById(string id)
        {
            try
            {
                var LoggedUserId = User.GetLoggedInUserId<string>();
                var SiteId = _globalVariable.SiteId;
                var employeeId = _commonService.GetEmployeeIdByUserIdAndEmail(SiteId, LoggedUserId);

                var entity = await _requirementServiceV2.GetRequirementDetailsForViewer(id, SiteId, LoggedUserId, employeeId);
                if (entity == null)
                    return BadRequest(new BadRequestError("No Requirement found with the specified id."));

                var model = _mapper.Map<RequirementModel>(entity);
                return Ok(model);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        #endregion

        #region GetRequirementRelatedCounts
        // Title: Get Requirement Related Counts
        // Description: How many tasks, issues, test cases, Q&A, action items, hours, notes, log entries, documents
        // and data mappings a requirement has: the figures its header and Related tab show.
        [HttpGet("related-counts/{id}")]
        [SiteOwned(typeof(Vsky.Models.Requirement))]
        [RecordScope(typeof(Vsky.Models.Requirement))]
        public async Task<IActionResult> GetRequirementRelatedCounts(string id)
        {
            try
            {
                var SiteId = _globalVariable.SiteId;
                var counts = await _requirementServiceV2.GetRequirementRelatedCounts(SiteId, id);
                return Ok(counts);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        #endregion

        #region GetRequirementLogThread
        // Title: Get Requirement Log Thread
        // Description: One page of the requirement log read like a chat (the newest `take` entries, or those
        // older than `beforeId`), with the original description as the oldest entry of all.
        [HttpGet("requirement-change-log/thread")]
        [SiteOwned(typeof(Vsky.Models.Requirement))]
        [RecordScope(typeof(Vsky.Models.Requirement))]
        public async Task<IActionResult> GetRequirementLogThread(string id, string beforeId = null, int take = 10)
        {
            try
            {
                var SiteId = _globalVariable.SiteId;
                var thread = await _requirementServiceV2.GetRequirementLogThread(SiteId, id, beforeId, take);
                return Ok(thread);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        #endregion
    }
}
