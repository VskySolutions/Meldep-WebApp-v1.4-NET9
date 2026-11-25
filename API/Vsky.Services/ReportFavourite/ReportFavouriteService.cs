using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Vsky.Data;
using Vsky.Models;

namespace Vsky.Services.ReportFavourite
{
    public class ReportFavouriteService : IReportFavouriteService
    {
        #region Define services
        private readonly IRepository<Models.ReportFavourite> _reportFavouriteRepository;

        public ReportFavouriteService(IRepository<Models.ReportFavourite> reportFavouriteRepository)
        {
            _reportFavouriteRepository = reportFavouriteRepository;
        }
        #endregion

        #region
        // Title: GetReportFavouriteByUser
        // Description: This method retrieves a ReportFavourite record for a specific report and user.
        // It queries the ReportFavourite table and returns the first matching record if the user has pinned the report, or null if no record exists.
        public async Task<Models.ReportFavourite> GetReportFavouriteByUser(string reportSettingsDetailId, string LoggedUserId)
        {
            var query = _reportFavouriteRepository.TableNoTracking.Where(x => x.ReportSettingsDetailId == reportSettingsDetailId && x.AspNetUserId == LoggedUserId);

            var item = await query.FirstOrDefaultAsync();
            return item;
        }
        #endregion

        #region InsertReportFavourite
        public void InsertReportFavourite(Models.ReportFavourite entity)
        {
            _reportFavouriteRepository.Insert(entity);
        }
        #endregion
    }
}
